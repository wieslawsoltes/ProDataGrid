import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const dirname = path.dirname(fileURLToPath(import.meta.url));
const stateFilePath = path.join(dirname, ".perf-host-state.json");

async function processExists(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

export default async function globalTeardown() {
  let state;
  try {
    const raw = await fs.readFile(stateFilePath, "utf8");
    state = JSON.parse(raw);
  } catch {
    return;
  }

  const pid = Number(state?.pid ?? 0);
  if (Number.isFinite(pid) && pid > 0 && (await processExists(pid))) {
    try {
      process.kill(pid, "SIGTERM");
    } catch {
      // no-op
    }
  }

  await fs.rm(stateFilePath, { force: true });
}

