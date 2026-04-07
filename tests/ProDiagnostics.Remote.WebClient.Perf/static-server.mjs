import fs from "node:fs/promises";
import path from "node:path";
import http from "node:http";
import { fileURLToPath } from "node:url";

const dirname = path.dirname(fileURLToPath(import.meta.url));

function parseArgs() {
  const args = process.argv.slice(2);
  const result = {
    root: path.resolve(dirname, "..", "..", "src", "ProDiagnostics.Remote.WebClient"),
    port: 4173,
  };

  for (let index = 0; index < args.length; index++) {
    const arg = args[index];
    if (arg === "--root" && index + 1 < args.length) {
      result.root = path.resolve(args[++index]);
    } else if (arg === "--port" && index + 1 < args.length) {
      const parsed = Number(args[++index]);
      if (Number.isFinite(parsed) && parsed > 0) {
        result.port = parsed;
      }
    }
  }

  return result;
}

function contentType(filePath) {
  const extension = path.extname(filePath).toLowerCase();
  return (
    {
      ".html": "text/html; charset=utf-8",
      ".js": "application/javascript; charset=utf-8",
      ".css": "text/css; charset=utf-8",
      ".json": "application/json; charset=utf-8",
      ".svg": "image/svg+xml",
      ".png": "image/png",
      ".jpg": "image/jpeg",
      ".jpeg": "image/jpeg",
      ".ico": "image/x-icon",
    }[extension] ?? "application/octet-stream"
  );
}

async function start() {
  const args = parseArgs();

  const server = http.createServer(async (request, response) => {
    try {
      const url = new URL(request.url ?? "/", `http://${request.headers.host ?? "127.0.0.1"}`);
      const requestPath = decodeURIComponent(url.pathname);
      const relativePath = requestPath === "/" ? "index.html" : requestPath.replace(/^\/+/, "");
      const fullPath = path.resolve(args.root, relativePath);

      if (!fullPath.startsWith(args.root)) {
        response.writeHead(403);
        response.end("Forbidden");
        return;
      }

      let stat;
      try {
        stat = await fs.stat(fullPath);
      } catch {
        response.writeHead(404);
        response.end("Not found");
        return;
      }

      const filePath = stat.isDirectory() ? path.join(fullPath, "index.html") : fullPath;
      const data = await fs.readFile(filePath);
      response.writeHead(200, { "Content-Type": contentType(filePath) });
      response.end(data);
    } catch (error) {
      response.writeHead(500);
      response.end(error instanceof Error ? error.message : "internal error");
    }
  });

  server.listen(args.port, "127.0.0.1", () => {
    console.log(`[StaticServer] Listening on http://127.0.0.1:${args.port}`);
  });
}

void start();

