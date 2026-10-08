import { spawn, spawnSync, type ChildProcess } from "node:child_process";
import { existsSync } from "node:fs";
import { createServer, type IncomingMessage, type Server, type ServerResponse } from "node:http";
import { createRequire } from "node:module";
import { createServer as createNetServer, type AddressInfo } from "node:net";
import path from "node:path";
import { fileURLToPath } from "node:url";
import type { TestProject } from "vitest/node";
import { API_TOKEN, AUTH_SECRET, ORG_ID, PROJECT_ID, PROJECT_NAME } from "./fixtures";

declare module "vitest" {
  export interface ProvidedContext {
    baseUrl: string;
  }
}

const webRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const READY_TIMEOUT_MS = 90_000;

/**
 * Vitest global setup: start a stub backend API and the built Next.js app (`next start`),
 * expose the app's base URL to tests as `baseUrl`, and return a teardown that stops both.
 *
 * @param project The Vitest project, used to `provide` the base URL to tests.
 * @returns Teardown function that stops the Next.js server and the stub API.
 */
export default async function setup(project: TestProject): Promise<() => Promise<void>> {
  if (!existsSync(path.join(webRoot, ".next", "BUILD_ID"))) {
    throw new Error("No Next.js build found. Run `yarn workspace @zagreus/web build` first.");
  }

  const stubApi = await startStubApi();
  const apiBaseUrl = `http://127.0.0.1:${(stubApi.address() as AddressInfo).port}`;

  const port = await freePort();
  const baseUrl = `http://localhost:${port}`;
  const nextBin = createRequire(import.meta.url).resolve("next/dist/bin/next");
  const next = spawn(process.execPath, [nextBin, "start", "-p", String(port)], {
    cwd: webRoot,
    env: {
      ...process.env,
      AUTH_SECRET,
      AUTH_TRUST_HOST: "true",
      API_BASE_URL: apiBaseUrl,
      AUTH_GOOGLE_ID: "integration-google-id",
      AUTH_GOOGLE_SECRET: "integration-google-secret",
      AUTH_MICROSOFT_ENTRA_ID_ID: "integration-microsoft-id",
      AUTH_MICROSOFT_ENTRA_ID_SECRET: "integration-microsoft-secret",
      AUTH_MICROSOFT_ENTRA_ID_ISSUER: "https://login.microsoftonline.com/common/v2.0",
    },
    stdio: ["ignore", "pipe", "pipe"],
  });
  let output = "";
  next.stdout?.on("data", (chunk) => (output += chunk));
  next.stderr?.on("data", (chunk) => (output += chunk));

  try {
    await waitUntilReady(baseUrl, next, () => output);
  } catch (error) {
    stopProcess(next);
    stubApi.close();
    throw error;
  }

  project.provide("baseUrl", baseUrl);

  return async () => {
    stopProcess(next);
    await new Promise<void>((resolve) => stubApi.close(() => resolve()));
  };
}

function startStubApi(): Promise<Server> {
  const server = createServer(handleStubRequest);
  return new Promise((resolve) => server.listen(0, "127.0.0.1", () => resolve(server)));
}

function handleStubRequest(req: IncomingMessage, res: ServerResponse): void {
  if (req.headers.authorization !== `Bearer ${API_TOKEN}`) return sendJson(res, 401, {});
  switch (req.url) {
    case "/api/me":
      return sendJson(res, 200, {
        id: "integration-user",
        email: "user@example.org",
        role: "OrgAdmin",
        orgId: ORG_ID,
        orgName: "Integration Test Org",
      });
    case `/api/organisations/${ORG_ID}/projects`:
      return sendJson(res, 200, [{ id: PROJECT_ID, name: PROJECT_NAME, region: "Greece" }]);
    case `/api/projects/${PROJECT_ID}/assessments/current`:
      return sendJson(res, 200, { submittedAt: new Date().toISOString() });
    default:
      return sendJson(res, 404, {});
  }
}

function sendJson(res: ServerResponse, status: number, body: unknown): void {
  res.writeHead(status, { "content-type": "application/json" });
  res.end(JSON.stringify(body));
}

function freePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = createNetServer();
    server.once("error", reject);
    server.listen(0, () => {
      const { port } = server.address() as AddressInfo;
      server.close(() => resolve(port));
    });
  });
}

async function waitUntilReady(baseUrl: string, next: ChildProcess, output: () => string): Promise<void> {
  const deadline = Date.now() + READY_TIMEOUT_MS;
  while (Date.now() < deadline) {
    if (next.exitCode !== null) {
      throw new Error(`next start exited with code ${next.exitCode}:\n${output()}`);
    }
    try {
      const res = await fetch(`${baseUrl}/login`, { redirect: "manual" });
      if (res.status < 500) return;
    } catch {
      // Server not listening yet.
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  throw new Error(`next start did not become ready within ${READY_TIMEOUT_MS}ms:\n${output()}`);
}

function stopProcess(child: ChildProcess): void {
  if (child.exitCode !== null || child.pid === undefined) return;
  // On Windows, kill() leaves Next's worker processes running; kill the whole tree instead.
  if (process.platform === "win32") spawnSync("taskkill", ["/pid", String(child.pid), "/T", "/F"]);
  else child.kill("SIGTERM");
}
