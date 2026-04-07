#!/usr/bin/env node
import fs from "node:fs/promises";
import path from "node:path";

function parseArgs(argv) {
  const args = {
    config: "tools/perf/perf-gates.config.json",
    protocolDir: "artifacts/remote-protocol-bench/results",
    hostloadDir: "artifacts/remote-hostload",
    webMetrics: "artifacts/remote-webclient-perf/metrics.json",
    summaryJson: "artifacts/perf-gates/summary.json",
    summaryMarkdown: "artifacts/perf-gates/summary.md",
    historyFile: "",
    recordHistory: false,
    failOnRegression: true,
  };

  for (let i = 0; i < argv.length; i += 1) {
    const token = argv[i];
    if (token === "--config" && i + 1 < argv.length) {
      args.config = argv[++i];
      continue;
    }
    if (token === "--protocol-dir" && i + 1 < argv.length) {
      args.protocolDir = argv[++i];
      continue;
    }
    if (token === "--hostload-dir" && i + 1 < argv.length) {
      args.hostloadDir = argv[++i];
      continue;
    }
    if (token === "--web-metrics" && i + 1 < argv.length) {
      args.webMetrics = argv[++i];
      continue;
    }
    if (token === "--summary-json" && i + 1 < argv.length) {
      args.summaryJson = argv[++i];
      continue;
    }
    if (token === "--summary-markdown" && i + 1 < argv.length) {
      args.summaryMarkdown = argv[++i];
      continue;
    }
    if (token === "--history-file" && i + 1 < argv.length) {
      args.historyFile = argv[++i];
      continue;
    }
    if (token === "--record-history") {
      args.recordHistory = true;
      continue;
    }
    if (token === "--no-fail") {
      args.failOnRegression = false;
      continue;
    }
  }

  return args;
}

function csvSplit(line) {
  const output = [];
  let current = "";
  let inQuotes = false;
  for (let i = 0; i < line.length; i += 1) {
    const char = line[i];
    if (char === '"') {
      if (inQuotes && line[i + 1] === '"') {
        current += '"';
        i += 1;
      } else {
        inQuotes = !inQuotes;
      }
      continue;
    }

    if (char === "," && !inQuotes) {
      output.push(current);
      current = "";
      continue;
    }

    current += char;
  }

  output.push(current);
  return output;
}

async function readCsvObjects(filePath) {
  const content = await fs.readFile(filePath, "utf8");
  const lines = content
    .split(/\r?\n/)
    .map((line) => line.trimEnd())
    .filter((line) => line.length > 0);

  if (lines.length < 2) {
    return [];
  }

  const header = csvSplit(lines[0]);
  const rows = [];
  for (let i = 1; i < lines.length; i += 1) {
    const values = csvSplit(lines[i]);
    const row = {};
    for (let c = 0; c < header.length; c += 1) {
      row[header[c]] = values[c] ?? "";
    }
    rows.push(row);
  }

  return rows;
}

function parseTimeToNs(value) {
  if (!value) {
    return Number.NaN;
  }

  const normalized = String(value).trim().replaceAll(",", "");
  const match = /^(-?\d+(?:\.\d+)?)\s*(ns|us|μs|ms|s)$/i.exec(normalized);
  if (!match) {
    return Number.NaN;
  }

  const number = Number.parseFloat(match[1]);
  const unit = match[2].toLowerCase();
  switch (unit) {
    case "ns":
      return number;
    case "us":
    case "μs":
      return number * 1000;
    case "ms":
      return number * 1_000_000;
    case "s":
      return number * 1_000_000_000;
    default:
      return Number.NaN;
  }
}

function parseBytes(value) {
  if (!value) {
    return Number.NaN;
  }

  const normalized = String(value).trim().replaceAll(",", "");
  const match = /^(-?\d+(?:\.\d+)?)\s*(b|kb|mb|gb)$/i.exec(normalized);
  if (!match) {
    return Number.NaN;
  }

  const number = Number.parseFloat(match[1]);
  const unit = match[2].toLowerCase();
  switch (unit) {
    case "b":
      return number;
    case "kb":
      return number * 1024;
    case "mb":
      return number * 1024 * 1024;
    case "gb":
      return number * 1024 * 1024 * 1024;
    default:
      return Number.NaN;
  }
}

function parseValue(value, kind) {
  switch (kind) {
    case "time":
      return parseTimeToNs(value);
    case "bytes":
      return parseBytes(value);
    case "number":
    default:
      return Number.parseFloat(String(value).replaceAll(",", ""));
  }
}

function formatNumber(value, decimals = 2) {
  if (!Number.isFinite(value)) {
    return "n/a";
  }
  return value.toFixed(decimals);
}

function evaluateThreshold(actual, baseline, maxRegressionPercent, hardMax) {
  let budget = Number.NaN;
  if (Number.isFinite(baseline) && Number.isFinite(maxRegressionPercent)) {
    budget = baseline * (1 + maxRegressionPercent / 100);
  }
  if (Number.isFinite(hardMax)) {
    budget = Number.isFinite(budget) ? Math.min(budget, hardMax) : hardMax;
  }

  if (!Number.isFinite(budget)) {
    return {
      pass: true,
      threshold: Number.NaN,
      deltaPercent: Number.isFinite(baseline) && baseline !== 0 ? ((actual - baseline) / baseline) * 100 : Number.NaN,
    };
  }

  return {
    pass: Number.isFinite(actual) && actual <= budget,
    threshold: budget,
    deltaPercent: Number.isFinite(baseline) && baseline !== 0 ? ((actual - baseline) / baseline) * 100 : Number.NaN,
  };
}

function makeResult(category, name, status, actual, threshold, baseline, unit, detail, deltaPercent = Number.NaN) {
  return {
    category,
    name,
    status,
    actual,
    threshold,
    baseline,
    unit,
    detail,
    deltaPercent,
  };
}

async function loadLatestHostLoadReport(hostloadDir) {
  let entries;
  try {
    entries = await fs.readdir(hostloadDir, { withFileTypes: true });
  } catch {
    return null;
  }

  const jsonFiles = entries.filter((entry) => entry.isFile() && entry.name.endsWith(".json"));
  if (jsonFiles.length === 0) {
    return null;
  }

  const withStats = await Promise.all(
    jsonFiles.map(async (entry) => {
      const fullPath = path.join(hostloadDir, entry.name);
      const stats = await fs.stat(fullPath);
      return { fullPath, mtimeMs: stats.mtimeMs };
    }),
  );
  withStats.sort((a, b) => b.mtimeMs - a.mtimeMs);
  const latestPath = withStats[0].fullPath;
  let report;
  try {
    report = JSON.parse(await fs.readFile(latestPath, "utf8"));
  } catch {
    return null;
  }

  return { latestPath, report };
}

async function loadJsonIfExists(filePath) {
  try {
    return JSON.parse(await fs.readFile(filePath, "utf8"));
  } catch {
    return null;
  }
}

function formatValueWithUnit(value, unit) {
  if (!Number.isFinite(value)) {
    return "n/a";
  }
  switch (unit) {
    case "ns":
      return `${formatNumber(value, 2)} ns`;
    case "ms":
      return `${formatNumber(value, 2)} ms`;
    case "bytes":
      return `${formatNumber(value, 0)} B`;
    default:
      return `${formatNumber(value, 2)} ${unit ?? ""}`.trim();
  }
}

async function ensureDirectoryFor(filePath) {
  const dir = path.dirname(filePath);
  await fs.mkdir(dir, { recursive: true });
}

function statusIcon(status) {
  if (status === "pass") {
    return "PASS";
  }
  if (status === "warn") {
    return "WARN";
  }
  return "FAIL";
}

function summarizeKeyMetrics(results) {
  const map = {};
  for (const item of results) {
    map[`${item.category}.${item.name}`] = Number.isFinite(item.actual) ? item.actual : null;
  }
  return map;
}

function buildMarkdown(summary, previousSummary) {
  const lines = [];
  lines.push("# Remote Perf Gate Summary");
  lines.push("");
  lines.push(`- Timestamp (UTC): ${summary.timestampUtc}`);
  lines.push(`- Overall: **${summary.overallPass ? "PASS" : "FAIL"}**`);
  lines.push(`- Failed checks: **${summary.failedCount}** / ${summary.totalCount}`);
  lines.push("");
  lines.push("## Check Results");
  lines.push("");
  lines.push("| Status | Category | Check | Actual | Threshold | Baseline | Delta | Notes |");
  lines.push("|---|---|---|---:|---:|---:|---:|---|");
  for (const item of summary.results) {
    const actual = formatValueWithUnit(item.actual, item.unit);
    const threshold = formatValueWithUnit(item.threshold, item.unit);
    const baseline = formatValueWithUnit(item.baseline, item.unit);
    const delta = Number.isFinite(item.deltaPercent) ? `${formatNumber(item.deltaPercent, 2)}%` : "n/a";
    lines.push(
      `| ${statusIcon(item.status)} | ${item.category} | ${item.name} | ${actual} | ${threshold} | ${baseline} | ${delta} | ${item.detail} |`,
    );
  }

  if (previousSummary) {
    lines.push("");
    lines.push("## Trend vs Previous Nightly");
    lines.push("");
    lines.push("| Check | Previous | Current | Delta |");
    lines.push("|---|---:|---:|---:|");
    const previous = previousSummary.keyMetrics ?? {};
    const current = summary.keyMetrics ?? {};
    for (const key of Object.keys(current).sort()) {
      const currentValue = current[key];
      const previousValue = previous[key];
      if (!Number.isFinite(currentValue) || !Number.isFinite(previousValue)) {
        continue;
      }

      const delta = previousValue === 0 ? Number.NaN : ((currentValue - previousValue) / previousValue) * 100;
      const deltaText = Number.isFinite(delta) ? `${formatNumber(delta, 2)}%` : "n/a";
      lines.push(`| ${key} | ${formatNumber(previousValue, 2)} | ${formatNumber(currentValue, 2)} | ${deltaText} |`);
    }
  }

  lines.push("");
  lines.push("## Triage");
  lines.push("");
  lines.push("Follow the regression policy in `plan/devtools-remote-runtime-performance-regression-policy.md`.");
  lines.push("");
  return lines.join("\n");
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const config = JSON.parse(await fs.readFile(args.config, "utf8"));
  const results = [];

  for (const check of config.protocol.checks ?? []) {
    const reportPath = path.join(args.protocolDir, check.file);
    let rows;
    try {
      rows = await readCsvObjects(reportPath);
    } catch (error) {
      results.push(
        makeResult(
          "protocol",
          check.name,
          "fail",
          Number.NaN,
          check.hardMax ?? Number.NaN,
          check.baseline ?? Number.NaN,
          check.unit ?? "",
          `missing report: ${reportPath}`,
        ),
      );
      continue;
    }

    const matchingRow = rows.find((row) => {
      for (const [key, value] of Object.entries(check.match ?? {})) {
        if (String(row[key] ?? "") !== String(value)) {
          return false;
        }
      }
      return true;
    });

    if (!matchingRow) {
      results.push(
        makeResult(
          "protocol",
          check.name,
          "fail",
          Number.NaN,
          check.hardMax ?? Number.NaN,
          check.baseline ?? Number.NaN,
          check.unit ?? "",
          "csv row not found",
        ),
      );
      continue;
    }

    const actual = parseValue(matchingRow[check.valueColumn], check.valueKind);
    const budget = evaluateThreshold(actual, check.baseline, check.maxRegressionPercent, check.hardMax);
    results.push(
      makeResult(
        "protocol",
        check.name,
        budget.pass ? "pass" : "fail",
        actual,
        budget.threshold,
        check.baseline ?? Number.NaN,
        check.unit ?? "",
        `from ${path.basename(reportPath)}`,
        budget.deltaPercent,
      ),
    );
  }

  const hostLoadData = await loadLatestHostLoadReport(args.hostloadDir);
  if (!hostLoadData) {
    for (const scenario of config.hostLoad.scenarios ?? []) {
      results.push(
        makeResult(
          "hostload",
          `${scenario.name}.p95`,
          "fail",
          Number.NaN,
          scenario.maxP95Ms ?? Number.NaN,
          Number.NaN,
          "ms",
          "no host load reports found",
        ),
      );
      results.push(
        makeResult(
          "hostload",
          `${scenario.name}.errors`,
          "fail",
          Number.NaN,
          scenario.maxErrors ?? Number.NaN,
          Number.NaN,
          "",
          "no host load reports found",
        ),
      );
    }
  } else {
    for (const scenario of config.hostLoad.scenarios ?? []) {
      const scenarioResult = (hostLoadData.report.Scenarios ?? []).find((item) => item.Name === scenario.name);
      if (!scenarioResult) {
        results.push(
          makeResult(
            "hostload",
            `${scenario.name}.p95`,
            "fail",
            Number.NaN,
            scenario.maxP95Ms ?? Number.NaN,
            Number.NaN,
            "ms",
            `scenario missing in ${path.basename(hostLoadData.latestPath)}`,
          ),
        );
        results.push(
          makeResult(
            "hostload",
            `${scenario.name}.errors`,
            "fail",
            Number.NaN,
            scenario.maxErrors ?? Number.NaN,
            Number.NaN,
            "",
            `scenario missing in ${path.basename(hostLoadData.latestPath)}`,
          ),
        );
        continue;
      }

      results.push(
        makeResult(
          "hostload",
          `${scenario.name}.p95`,
          scenarioResult.P95Ms <= scenario.maxP95Ms ? "pass" : "fail",
          scenarioResult.P95Ms,
          scenario.maxP95Ms,
          Number.NaN,
          "ms",
          `from ${path.basename(hostLoadData.latestPath)}`,
        ),
      );

      results.push(
        makeResult(
          "hostload",
          `${scenario.name}.errors`,
          scenarioResult.Errors <= scenario.maxErrors ? "pass" : "fail",
          scenarioResult.Errors,
          scenario.maxErrors,
          Number.NaN,
          "",
          `from ${path.basename(hostLoadData.latestPath)}`,
        ),
      );
    }
  }

  const webMetrics = await loadJsonIfExists(args.webMetrics);
  const webThresholds = config.web.thresholdsMs ?? {};
  const requiredMetrics = new Set(config.web.requiredMetrics ?? []);
  if (!webMetrics) {
    for (const metricName of Object.keys(webThresholds)) {
      results.push(
        makeResult(
          "web",
          metricName,
          requiredMetrics.has(metricName) ? "fail" : "warn",
          Number.NaN,
          webThresholds[metricName],
          Number.NaN,
          "ms",
          `missing web metrics file: ${args.webMetrics}`,
        ),
      );
    }
  } else {
    for (const [metricName, maxMs] of Object.entries(webThresholds)) {
      const actual = Number(webMetrics[metricName]);
      const hasMetric = Number.isFinite(actual);
      if (!hasMetric) {
        results.push(
          makeResult(
            "web",
            metricName,
            requiredMetrics.has(metricName) ? "fail" : "warn",
            Number.NaN,
            Number(maxMs),
            Number.NaN,
            "ms",
            "metric missing",
          ),
        );
        continue;
      }

      results.push(
        makeResult(
          "web",
          metricName,
          actual <= Number(maxMs) ? "pass" : "fail",
          actual,
          Number(maxMs),
          Number.NaN,
          "ms",
          `from ${path.basename(args.webMetrics)}`,
        ),
      );
    }
  }

  const failed = results.filter((item) => item.status === "fail");
  const summary = {
    timestampUtc: new Date().toISOString(),
    overallPass: failed.length === 0,
    failedCount: failed.length,
    totalCount: results.length,
    artifacts: {
      protocolDir: args.protocolDir,
      hostloadDir: args.hostloadDir,
      webMetrics: args.webMetrics,
    },
    results,
    keyMetrics: summarizeKeyMetrics(results),
  };

  let previousSummary = null;
  if (args.recordHistory && args.historyFile) {
    await ensureDirectoryFor(args.historyFile);
    let history = [];
    try {
      history = JSON.parse(await fs.readFile(args.historyFile, "utf8"));
      if (!Array.isArray(history)) {
        history = [];
      }
    } catch {
      history = [];
    }

    previousSummary = history.length > 0 ? history[history.length - 1] : null;
    history.push({
      timestampUtc: summary.timestampUtc,
      overallPass: summary.overallPass,
      failedCount: summary.failedCount,
      totalCount: summary.totalCount,
      keyMetrics: summary.keyMetrics,
    });
    if (history.length > 90) {
      history = history.slice(history.length - 90);
    }
    await fs.writeFile(args.historyFile, JSON.stringify(history, null, 2), "utf8");
  }

  const markdown = buildMarkdown(summary, previousSummary);

  await ensureDirectoryFor(args.summaryJson);
  await ensureDirectoryFor(args.summaryMarkdown);
  await fs.writeFile(args.summaryJson, JSON.stringify(summary, null, 2), "utf8");
  await fs.writeFile(args.summaryMarkdown, markdown, "utf8");

  console.log(markdown);
  if (failed.length > 0 && args.failOnRegression) {
    process.exitCode = 1;
  }
}

await main();
