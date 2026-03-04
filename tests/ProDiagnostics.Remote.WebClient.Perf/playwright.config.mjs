import { defineConfig } from "@playwright/test";
import path from "node:path";
import { fileURLToPath } from "node:url";

const dirname = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(dirname, "..", "..");
const webClientRoot = path.join(repositoryRoot, "src", "ProDiagnostics.Remote.WebClient");

export default defineConfig({
  testDir: path.join(dirname, "tests"),
  timeout: 120_000,
  expect: {
    timeout: 30_000,
  },
  retries: 0,
  workers: 1,
  reporter: [["list"]],
  use: {
    baseURL: "http://127.0.0.1:4173",
    trace: "retain-on-failure",
  },
  globalSetup: path.join(dirname, "global-setup.mjs"),
  globalTeardown: path.join(dirname, "global-teardown.mjs"),
  webServer: {
    command: `node "${path.join(dirname, "static-server.mjs")}" --root "${webClientRoot}" --port 4173`,
    url: "http://127.0.0.1:4173/index.html",
    timeout: 30_000,
    reuseExistingServer: true,
  },
});

