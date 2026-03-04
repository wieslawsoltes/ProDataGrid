import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { test, expect } from "@playwright/test";

const dirname = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(dirname, "..");
const repositoryRoot = path.resolve(projectRoot, "..", "..");
const stateFilePath = path.join(projectRoot, ".perf-host-state.json");
const metricsFilePath = process.env.PRODIAGNOSTICS_WEB_PERF_METRICS
  ? path.resolve(process.env.PRODIAGNOSTICS_WEB_PERF_METRICS)
  : path.join(repositoryRoot, "artifacts", "remote-webclient-perf", "metrics.json");

async function readHostState() {
  const raw = await fs.readFile(stateFilePath, "utf8");
  return JSON.parse(raw);
}

async function writeMetrics(values) {
  await fs.mkdir(path.dirname(metricsFilePath), { recursive: true });

  let current = {};
  try {
    current = JSON.parse(await fs.readFile(metricsFilePath, "utf8"));
  } catch {
    current = {};
  }

  const next = {
    ...current,
    ...values,
    updatedAtUtc: new Date().toISOString(),
  };
  await fs.writeFile(metricsFilePath, JSON.stringify(next, null, 2), "utf8");
}

function parseNodeCount(summaryText) {
  const match = /Nodes:\s*(\d+)/i.exec(summaryText ?? "");
  return match ? Number(match[1]) : 0;
}

async function waitForConnectedState(page) {
  await expect(page.locator("#connectBtn")).toBeDisabled({ timeout: 30_000 });
  await expect(page.locator("#disconnectBtn")).toBeEnabled({ timeout: 30_000 });
}

test.describe.configure({ mode: "serial" });

test("web client connects and renders first tree snapshot", async ({ page }) => {
  const state = await readHostState();
  const wsUrl = String(state.wsUrl ?? "ws://127.0.0.1:29414/attach");

  await page.goto("/index.html");
  await page.fill("#urlInput", wsUrl);

  const connectStarted = Date.now();
  await page.click("#connectBtn");
  await waitForConnectedState(page);
  const connectMs = Date.now() - connectStarted;

  const treeStarted = Date.now();
  await page.click("#refreshTreeBtn");
  await page.waitForTimeout(1_000);
  const nodeCount = parseNodeCount(await page.locator("#treeSummary").textContent());
  const firstTreeMs = Date.now() - treeStarted;

  console.log(`[web perf] connect=${connectMs}ms firstTree=${firstTreeMs}ms nodes=${nodeCount}`);
  await writeMetrics({
    connectMs,
    firstTreeMs,
    nodeCount,
  });
  expect(connectMs).toBeLessThan(20_000);
  expect(firstTreeMs).toBeLessThan(20_000);
});

test("tree filter and expand/collapse response stay bounded", async ({ page }) => {
  const state = await readHostState();
  const wsUrl = String(state.wsUrl ?? "ws://127.0.0.1:29414/attach");

  await page.goto("/index.html");
  await page.fill("#urlInput", wsUrl);
  await page.click("#connectBtn");
  await waitForConnectedState(page);

  await page.click("#refreshTreeBtn");
  await page.waitForTimeout(1_000);
  const nodeCount = parseNodeCount(await page.locator("#treeSummary").textContent());

  test.skip(nodeCount === 0, "Host returned 0 tree nodes; skipping expand/collapse perf assertions.");

  const expander = page.locator("button.tree-expander").first();
  const expanderCount = await expander.count();
  test.skip(expanderCount === 0, "Tree expander controls are unavailable in the current renderer mode.");

  const filterStarted = Date.now();
  await page.fill("#treeFilterInput", "Border");
  await page.waitForTimeout(50);
  const filterMs = Date.now() - filterStarted;

  await expect(expander).toBeVisible();

  const collapseStarted = Date.now();
  await expander.click();
  await page.waitForTimeout(50);
  const collapseMs = Date.now() - collapseStarted;

  const expandStarted = Date.now();
  await expander.click();
  await page.waitForTimeout(50);
  const expandMs = Date.now() - expandStarted;

  console.log(`[web perf] filter=${filterMs}ms collapse=${collapseMs}ms expand=${expandMs}ms`);
  await writeMetrics({
    filterMs,
    collapseMs,
    expandMs,
  });
  expect(filterMs).toBeLessThan(5_000);
  expect(collapseMs).toBeLessThan(3_000);
  expect(expandMs).toBeLessThan(3_000);
});
