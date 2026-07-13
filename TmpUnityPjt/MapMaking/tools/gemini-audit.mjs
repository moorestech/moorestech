#!/usr/bin/env node
import { resolve } from "node:path";
import { preprocessImages } from "./lib/image.mjs";
import { buildSystemPrompt } from "./lib/prompt.mjs";
import {
  createCache,
  getCache,
  resolveShortId,
  renewCacheTTL,
  generateWithCache,
  extractText,
  extractUsage,
} from "./lib/gemini-api.mjs";

// --- CLI 引数パース ---
const args = process.argv.slice(2);

// フラグ解析: --session, --session-info, --prompt を抽出
function parseArgs(argv) {
  let sessionId = null;
  let sessionInfoId = null;
  let customPrompt = null;
  const positional = [];

  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === "--session" && argv[i + 1]) {
      sessionId = argv[++i];
    } else if (argv[i] === "--session-info" && argv[i + 1]) {
      sessionInfoId = argv[++i];
    } else if (argv[i] === "--prompt" && argv[i + 1]) {
      customPrompt = argv[++i];
    } else if (argv[i] === "--help") {
      return { help: true };
    } else {
      positional.push(argv[i]);
    }
  }

  return {
    sessionId,
    sessionInfoId,
    customPrompt,
    imagePaths: positional.map((p) => resolve(p)),
  };
}

const parsed = parseArgs(args);

if (parsed.help || (args.length === 0)) {
  console.error(
    [
      "Usage: node tools/gemini-audit.mjs <image> [image2 ...] --prompt \"確認観点\" [--session <ID>]",
      "",
      "Options:",
      "  --prompt <text>        確認観点の指示（必須）",
      "  --session <ID>         既存セッションIDを指定（キャッシュヒット）",
      "  --session-info <ID>    セッション情報を表示して終了",
      "  --help                 このヘルプを表示",
    ].join("\n")
  );
  process.exit(1);
}

// --- API キー確認 ---
const apiKey = process.env.GEMINI_API_KEY;
if (!apiKey) {
  console.error("Error: GEMINI_API_KEY 環境変数が設定されていません");
  process.exit(1);
}

// --- セッション情報表示 ---
async function showSessionInfo(shortId) {
  const fullId = await resolveShortId(apiKey, shortId);
  const cache = await getCache(apiKey, fullId);
  console.error(`[Session] ID: ${cache.displayName}`);
  console.error(`[Session] モデル: ${cache.model}`);
  console.error(`[Session] 作成: ${cache.createTime}`);
  console.error(`[Session] 更新: ${cache.updateTime}`);
  console.error(`[Session] 有効期限: ${cache.expireTime}`);
  if (cache.usageMetadata) {
    console.error(`[Session] キャッシュトークン数: ${cache.usageMetadata.totalTokenCount}`);
  }
}

// --- メイン ---
async function main() {
  // --session-info: 情報表示のみ
  if (parsed.sessionInfoId) {
    await showSessionInfo(parsed.sessionInfoId);
    return;
  }

  // 画像とプロンプトの検証
  if (parsed.imagePaths.length === 0) {
    console.error("Error: 画像パスを1つ以上指定してください");
    process.exit(1);
  }
  if (!parsed.customPrompt) {
    console.error("Error: --prompt は必須です");
    process.exit(1);
  }

  // セッション準備と画像前処理を並列実行
  const sessionPromise = (async () => {
    if (parsed.sessionId) {
      const sid = parsed.sessionId;
      const fullId = await resolveShortId(apiKey, sid);
      await renewCacheTTL(apiKey, fullId);
      console.error(`[Session] 既存セッション使用: ${sid}（TTL延長済み）`);
      return { cacheId: fullId, shortId: sid };
    }
    console.error("[Session] 新規セッション作成中...");
    const systemPrompt = await buildSystemPrompt();
    const cache = await createCache(apiKey, systemPrompt);
    console.error(`[Session] 新規セッション作成: ${cache.shortId}`);
    console.error(`[Session] 次回以降は --session ${cache.shortId} を指定してください`);
    console.error(`[Session] 有効期限: ${cache.expireTime}`);
    return { cacheId: cache.name, shortId: cache.shortId };
  })();

  const imagePromise = (async () => {
    console.error(`${parsed.imagePaths.length}枚の画像を前処理中...`);
    const bufs = await preprocessImages(parsed.imagePaths);
    const totalKB = bufs.reduce((s, b) => s + b.length, 0) / 1024;
    console.error(`前処理完了: 合計${totalKB.toFixed(1)}KB (JPEG x${bufs.length})`);
    return bufs;
  })();

  const [{ cacheId }, imageBuffers] = await Promise.all([sessionPromise, imagePromise]);

  // ユーザーパーツ構築（テキスト + 画像）
  const parts = [{ text: parsed.customPrompt }];
  for (const buf of imageBuffers) {
    parts.push({
      inline_data: { mime_type: "image/jpeg", data: buf.toString("base64") },
    });
  }

  // Gemini API 呼び出し
  console.error("Gemini API に送信中...");
  const response = await generateWithCache(apiKey, cacheId, parts);

  // レスポンス出力
  const text = extractText(response);
  if (!text) {
    console.error("Gemini からのレスポンスにテキストが含まれていません:");
    console.error(JSON.stringify(response, null, 2));
    process.exit(1);
  }
  console.log(text);

  // トークン使用量をstderrに出力
  const usage = extractUsage(response);
  if (usage) {
    console.error(
      `[Token] cached: ${usage.cachedTokens}, prompt: ${usage.promptTokens}, ` +
      `response: ${usage.responseTokens}, total: ${usage.totalTokens}`
    );
  }
}

main().catch((err) => {
  console.error(`Error: ${err.message}`);
  process.exit(1);
});
