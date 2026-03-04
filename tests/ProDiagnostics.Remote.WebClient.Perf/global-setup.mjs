import fs from "node:fs/promises";
import fsSync from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { spawn } from "node:child_process";

const dirname = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(dirname, "..", "..");
const stateFilePath = path.join(dirname, ".perf-host-state.json");
const logDirectory = path.join(repositoryRoot, "artifacts", "remote-webclient-perf");
const logFilePath = path.join(logDirectory, "perf-host.log");
const hostProject = path.join(
  repositoryRoot,
  "tests",
  "ProDiagnostics.Remote.WebClient.Perf",
  "PerfHost",
  "ProDiagnostics.Remote.WebClient.PerfHost.csproj",
);

const startupTimeoutMs = 120_000;
const readyMarker = "[PerfHost] READY";

function waitForReady(processHandle) {
  return new Promise((resolve, reject) => {
    const timeout = setTimeout(() => {
      reject(new Error("Timed out waiting for perf host readiness marker."));
    }, startupTimeoutMs);

    function onData(data) {
      const text = data.toString();
      if (text.includes(readyMarker)) {
        clearTimeout(timeout);
        processHandle.stdout.off("data", onData);
        resolve();
      }
    }

    processHandle.stdout.on("data", onData);
    processHandle.once("exit", (code, signal) => {
      clearTimeout(timeout);
      reject(
        new Error(
          `Perf host process exited before readiness marker (code=${code ?? "null"}, signal=${signal ?? "null"}).`,
        ),
      );
    });
  });
}

export default async function globalSetup() {
  await fs.mkdir(logDirectory, { recursive: true });
  await fs.writeFile(logFilePath, "", "utf8");

  const hostProcess = spawn(
    "dotnet",
    [
      "run",
      "-c",
      "Release",
      "--project",
      hostProject,
      "--",
      "--port",
      "29414",
      "--controls",
      "1500",
    ],
    {
      cwd: repositoryRoot,
      env: {
        ...process.env,
        DOTNET_CLI_TELEMETRY_OPTOUT: "1",
      },
      stdio: ["ignore", "pipe", "pipe"],
    },
  );

  const outputStream = fsSync.createWriteStream(logFilePath, { flags: "a" });
  hostProcess.stdout.pipe(outputStream);
  hostProcess.stderr.pipe(outputStream);

  try {
    await waitForReady(hostProcess);
  } catch (error) {
    hostProcess.kill("SIGTERM");
    throw error;
  }

  await fs.writeFile(
    stateFilePath,
    JSON.stringify(
      {
        pid: hostProcess.pid,
        wsUrl: "ws://127.0.0.1:29414/attach",
        logFilePath,
      },
      null,
      2,
    ),
    "utf8",
  );
}

