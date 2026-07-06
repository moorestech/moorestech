import http from "node:http";
import fs from "node:fs";
import fsp from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// 引数から対象ディレクトリとポートを取得する
// Parse the target directory and port from argv
function parseArgs(argv) {
  const args = { dir: null, port: 4980 };
  for (let i = 0; i < argv.length; i += 1) {
    if (argv[i] === "--dir") args.dir = path.resolve(argv[i + 1]);
    if (argv[i] === "--port") args.port = Number(argv[i + 1]);
  }
  if (!args.dir) throw new Error("--dir is required");
  return args;
}

const { dir: RESULT_DIR, port: PORT } = parseArgs(process.argv.slice(2));

// 欠けているファイルは空文字として扱う
// Treat missing optional files as empty text
async function readText(filePath) {
  if (!fs.existsSync(filePath)) return "";
  return fsp.readFile(filePath, "utf8");
}

async function readJson(filePath) {
  const text = await readText(filePath);
  if (!text.trim()) return null;
  return JSON.parse(text);
}

// run-* ディレクトリから計画と採点結果を収集する
// Collect plans and evaluations from run-* directories
async function collectRuns() {
  const entries = await fsp.readdir(RESULT_DIR, { withFileTypes: true });
  const runDirs = entries
    .filter((entry) => entry.isDirectory() && entry.name.startsWith("run-"))
    .map((entry) => entry.name)
    .sort((a, b) => a.localeCompare(b));

  const runs = [];
  for (const name of runDirs) {
    const runDir = path.join(RESULT_DIR, name);
    const plan = await readText(path.join(runDir, "plan.md"));
    const evaluation = await readJson(path.join(runDir, "eval-output.json"));
    runs.push({ name, plan, evaluation });
  }
  return runs;
}

function summarizeRuns(runs) {
  const scored = runs.filter((run) => run.evaluation);
  const average = (field) => {
    const values = scored.map((run) => Number(run.evaluation[field])).filter(Number.isFinite);
    if (values.length === 0) return null;
    return Math.round((values.reduce((sum, value) => sum + value, 0) / values.length) * 10) / 10;
  };
  return {
    runCount: runs.length,
    scoredCount: scored.length,
    averageOverall: average("overall_score"),
    averageGoal: average("goal_score"),
  };
}

// ビューアが読む単一JSONへ正規化する
// Normalize the result directory into one JSON payload
async function buildResult() {
  const runs = await collectRuns();
  return {
    kind: "run-skill-iter-improve",
    sourceDir: RESULT_DIR,
    title: path.basename(RESULT_DIR),
    summary: summarizeRuns(runs),
    documents: {
      iterLog: await readText(path.join(RESULT_DIR, "iter-log.md")),
      reproContext: await readText(path.join(RESULT_DIR, "repro-context.md")),
      rubric: await readText(path.join(RESULT_DIR, "rubric.md")),
      improvementDiff: await readText(path.join(RESULT_DIR, "improvement-diff.txt")),
    },
    runs,
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
  console.log(`Iter improve viewer serving ${RESULT_DIR} at http://localhost:${PORT}`);
});
