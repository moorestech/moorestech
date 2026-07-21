import http from "node:http";
import fs from "node:fs";
import fsp from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const DIST_DIR = path.join(__dirname, "dist");

// --dir/--port を argv から取得
// Parse --dir/--port from argv
function parseArgs(argv) {
  const args = { dir: null, port: 4970 };
  for (let i = 0; i < argv.length; i += 1) {
    if (argv[i] === "--dir") args.dir = path.resolve(argv[i + 1]);
    if (argv[i] === "--port") args.port = Number(argv[i + 1]);
  }
  if (!args.dir) throw new Error("--dir is required");
  return args;
}

const { dir: MEDIA_DIR, port: PORT } = parseArgs(process.argv.slice(2));

const VIDEO_EXT = new Set([".webm", ".mp4", ".mov", ".mkv"]);
const IMAGE_EXT = new Set([".png", ".jpg", ".jpeg", ".gif", ".webp"]);
const MIME_TYPES = {
  ".webm": "video/webm",
  ".mp4": "video/mp4",
  ".mov": "video/quicktime",
  ".mkv": "video/x-matroska",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".gif": "image/gif",
  ".webp": "image/webp",
  ".html": "text/html",
  ".js": "text/javascript",
  ".css": "text/css",
  ".svg": "image/svg+xml",
};

// MEDIA_DIR配下を再帰走査しメディアファイルを列挙
// Recursively walk MEDIA_DIR and collect media file entries
async function listMediaFiles(baseDir) {
  const results = [];

  async function walk(currentDir) {
    const entries = await fsp.readdir(currentDir, { withFileTypes: true });
    for (const entry of entries) {
      const fullPath = path.join(currentDir, entry.name);
      if (entry.isDirectory()) {
        await walk(fullPath);
        continue;
      }
      const ext = path.extname(entry.name).toLowerCase();
      const type = VIDEO_EXT.has(ext) ? "video" : IMAGE_EXT.has(ext) ? "image" : null;
      if (!type) continue;
      const stat = await fsp.stat(fullPath);
      results.push({
        name: path.relative(baseDir, fullPath).split(path.sep).join("/"),
        type,
        size: stat.size,
      });
    }
  }

  await walk(baseDir);
  results.sort((a, b) => a.name.localeCompare(b.name));
  return results;
}

// dist/ 配下の静的ファイルを返す（存在しなければ404）
// Serve a static file from dist/, 404 if missing
async function serveStatic(req, res) {
  const urlPath = req.url === "/" ? "/index.html" : req.url;
  const filePath = path.join(DIST_DIR, decodeURIComponent(urlPath.split("?")[0]));
  if (!filePath.startsWith(DIST_DIR)) {
    res.writeHead(403).end("Forbidden");
    return;
  }
  if (!fs.existsSync(filePath)) {
    res.writeHead(404).end("Not Found");
    return;
  }
  const ext = path.extname(filePath).toLowerCase();
  res.writeHead(200, { "Content-Type": MIME_TYPES[ext] ?? "application/octet-stream" });
  fs.createReadStream(filePath).pipe(res);
}

// Range対応でメディアファイルをストリーム配信（video seek用）
// Stream a media file with HTTP Range support (needed for video seeking)
async function serveMedia(req, res) {
  const relativePath = decodeURIComponent(req.url.replace(/^\/media\//, ""));
  const filePath = path.resolve(MEDIA_DIR, relativePath);
  // パストラバーサル対策: 解決後パスがMEDIA_DIR配下か検証
  // Guard against path traversal by checking the resolved prefix
  if (!filePath.startsWith(MEDIA_DIR + path.sep)) {
    res.writeHead(403).end("Forbidden");
    return;
  }
  if (!fs.existsSync(filePath)) {
    res.writeHead(404).end("Not Found");
    return;
  }

  const stat = await fsp.stat(filePath);
  const ext = path.extname(filePath).toLowerCase();
  const contentType = MIME_TYPES[ext] ?? "application/octet-stream";
  const range = req.headers.range;

  if (!range) {
    res.writeHead(200, { "Content-Type": contentType, "Content-Length": stat.size });
    fs.createReadStream(filePath).pipe(res);
    return;
  }

  const match = /bytes=(\d*)-(\d*)/.exec(range);
  const start = match[1] ? Number(match[1]) : 0;
  const end = match[2] ? Number(match[2]) : stat.size - 1;
  res.writeHead(206, {
    "Content-Type": contentType,
    "Content-Range": `bytes ${start}-${end}/${stat.size}`,
    "Accept-Ranges": "bytes",
    "Content-Length": end - start + 1,
  });
  fs.createReadStream(filePath, { start, end }).pipe(res);
}

const server = http.createServer(async (req, res) => {
  if (req.url === "/api/list") {
    const items = await listMediaFiles(MEDIA_DIR);
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify({ dirName: path.basename(MEDIA_DIR), items }));
    return;
  }
  if (req.url.startsWith("/media/")) {
    await serveMedia(req, res);
    return;
  }
  await serveStatic(req, res);
});

server.listen(PORT, () => {
  console.log(`Evidence viewer serving ${MEDIA_DIR} at http://localhost:${PORT}`);
});
