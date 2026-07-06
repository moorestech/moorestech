import http from "node:http";
import fs from "node:fs";
import fsp from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// 引数から対象ディレクトリとポートを取得する
// Parse the target directory and port from argv
function parseArgs(argv) {
  const args = { dir: null, port: 4981 };
  for (let i = 0; i < argv.length; i += 1) {
    if (argv[i] === "--dir") args.dir = path.resolve(argv[i + 1]);
    if (argv[i] === "--port") args.port = Number(argv[i + 1]);
  }
  if (!args.dir) throw new Error("--dir is required");
  return args;
}

const { dir: RESULT_DIR, port: PORT } = parseArgs(process.argv.slice(2));

// 欠けているファイルは空として扱う
// Treat missing optional files as empty values
async function readText(filePath) {
  if (!fs.existsSync(filePath)) return "";
  return fsp.readFile(filePath, "utf8");
}

async function readJson(filePath) {
  const text = await readText(filePath);
  if (!text.trim()) return null;
  return JSON.parse(text);
}

function looksLikeTrial(dirPath) {
  return ["report.md", "task.md", "workflow.md", "transcript.jsonl"].some((file) => fs.existsSync(path.join(dirPath, file)));
}

async function transcriptStats(filePath) {
  const text = await readText(filePath);
  if (!text.trim()) return { lineCount: 0, models: [] };
  const models = new Set();
  const lines = text.split("\n").filter(Boolean);
  for (const line of lines) {
    const model = /"model"\s*:\s*"([^"]+)"/.exec(line)?.[1];
    if (model) models.add(model);
  }
  return { lineCount: lines.length, models: [...models].sort() };
}

async function listTrialDirs() {
  if (looksLikeTrial(RESULT_DIR)) return [RESULT_DIR];
  const entries = await fsp.readdir(RESULT_DIR, { withFileTypes: true });
  return entries
    .filter((entry) => entry.isDirectory())
    .map((entry) => path.join(RESULT_DIR, entry.name))
    .filter(looksLikeTrial)
    .sort((a, b) => path.basename(a).localeCompare(path.basename(b)));
}

// trialごとの主要成果物を単一オブジェクトにまとめる
// Pack each trial's important artifacts into one object
async function collectTrials() {
  const dirs = await listTrialDirs();
  const trials = [];
  for (const dir of dirs) {
    const status = await readJson(path.join(dir, "out", "status.json"));
    const transcript = await transcriptStats(path.join(dir, "transcript.jsonl"));
    trials.push({
      name: path.basename(dir),
      dir,
      status,
      transcript,
      report: await readText(path.join(dir, "report.md")),
      task: await readText(path.join(dir, "task.md")),
      workflow: await readText(path.join(dir, "workflow.md")),
      pane: await readText(path.join(dir, "pane.txt")),
    });
  }
  return trials;
}

function summarizeTrials(trials) {
  return {
    trialCount: trials.length,
    withStatus: trials.filter((trial) => trial.status).length,
    withReport: trials.filter((trial) => trial.report.trim()).length,
    withTranscript: trials.filter((trial) => trial.transcript.lineCount > 0).length,
  };
}

// ビューアが読む単一JSONへ正規化する
// Normalize live-trial output into one JSON payload
async function buildResult() {
  const trials = await collectTrials();
  return {
    kind: "run-skill-live-trial",
    sourceDir: RESULT_DIR,
    title: path.basename(RESULT_DIR),
    summary: summarizeTrials(trials),
    trials,
  };
}

function sendJson(res, payload) {
  res.writeHead(200, { "Content-Type": "application/json; charset=utf-8" });
  res.end(JSON.stringify(payload));
}

async function serveIndex(res) {
  const html = await fsp.readFile(path.join(__dirname, "index.html"), "utf8");
  res.writeHead(200, { "Content-Type": "text/html; charset=utf-8" });
  res.end(html);
}

const server = http.createServer(async (req, res) => {
  if (req.url === "/api/result") {
    sendJson(res, await buildResult());
    return;
  }
  await serveIndex(res);
});

server.listen(PORT, () => {
  console.log(`Live trial viewer serving ${RESULT_DIR} at http://localhost:${PORT}`);
});
