#!/usr/bin/env node
import { resolve } from "node:path";
import { unlink } from "node:fs/promises";
import { preprocessImagesForCodex } from "./lib/image.mjs";
import { buildSystemPrompt } from "./lib/prompt.mjs";
import { runCodexExec } from "./lib/codex-runner.mjs";

// --- CLI 引数パース ---
function parseArgs(argv) {
  let sessionId = null;
  let customPrompt = null;
  const positional = [];

  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === "--session" && argv[i + 1]) {
      sessionId = argv[++i];
    } else if (argv[i] === "--ask" && argv[i + 1]) {
      customPrompt = argv[++i];
    } else if (argv[i] === "--help") {
      return { help: true };
    } else {
      positional.push(argv[i]);
    }
  }

  return {
    sessionId,
    customPrompt,
    imagePaths: positional.map((p) => resolve(p)),
  };
}

const parsed = parseArgs(process.argv.slice(2));

if (parsed.help || process.argv.length <= 2) {
  console.error(
    [
      "Usage: node tools/codex-audit.mjs [image ...] --ask \"確認観点\" [--session <ID>]",
      "",
      "Options:",
      "  --ask <text>           確認観点の指示（必須）",
      "  --session <ID>         既存セッションID",
      "  --help                 このヘルプを表示",
      "",
      "画像は省略可。画像なしはコード/アルゴリズム相談モード。",
    ].join("\n")
  );
  process.exit(1);
}

// --- メイン ---
async function main() {
  if (!parsed.customPrompt) {
    console.error("Error: --ask は必須です");
    process.exit(1);
  }

  // 画像前処理（1MB超のみ圧縮、存在しないファイルは sharp が throw する）
  const processedPaths = await preprocessImagesForCodex(parsed.imagePaths);

  try {
    // プロンプト構築
    let prompt;
    if (parsed.sessionId) {
      prompt = parsed.customPrompt;
    } else {
      const systemPrompt = await buildSystemPrompt();
      prompt = `${systemPrompt}\n\n---\n\n${parsed.customPrompt}`;
    }

    // 実行
    const result = await runCodexExec({
      imagePaths: processedPaths,
      prompt,
      sessionId: parsed.sessionId,
      projectDir: process.cwd(),
    });

    if (!result.resultText) {
      console.error("Error: レスポンスにテキストが含まれていません");
      process.exit(1);
    }
    console.log(result.resultText);

    // セッションID案内（新規のみ）
    if (!parsed.sessionId && result.threadId) {
      console.error(`--session ${result.threadId} を指定してください`);
    }
  } finally {
    // 圧縮で生成した一時ファイルを削除
    await Promise.all(
      processedPaths
        .filter((p, i) => p !== parsed.imagePaths[i])
        .map((p) => unlink(p).catch(() => {}))
    );
  }
}

main().catch((err) => {
  console.error(`Error: ${err.message}`);
  process.exit(1);
});
