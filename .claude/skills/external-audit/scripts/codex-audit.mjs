#!/usr/bin/env node
// codex-audit.mjs — 外部AI監査/相談を Codex CLI 経由で行う自己完結ラッパー。
// 画像は省略可。--ask プロンプトのみで相談モードとして動作する。
// 依存: codex CLI のみ（npm パッケージ不要）。

import { spawn } from "node:child_process";
import { readFile, unlink } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { randomBytes } from "node:crypto";

function parseArgs(argv) {
  let sessionId = null;
  let customPrompt = null;
  const positional = [];

  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === "--session" && argv[i + 1]) {
      sessionId = argv[++i];
    } else if (argv[i] === "--ask" && argv[i + 1]) {
      customPrompt = argv[++i];
    } else if (argv[i] === "--help" || argv[i] === "-h") {
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
      "Usage: node codex-audit.mjs [image ...] --ask \"プロンプト\" [--session <ID>]",
      "",
      "Options:",
      "  --ask <text>     プロンプト本文（必須）。評価基準と確認観点を含めること",
      "  --session <ID>   既存セッションを再開する場合のID",
      "  --help, -h       このヘルプを表示",
      "",
      "画像は省略可。画像なしはコード/設計の相談モード。",
      "初回実行時、stderr に --session <UUID> 案内が出力される。",
    ].join("\n")
  );
  process.exit(1);
}

function parseJsonlOutput(jsonlText) {
  const lines = jsonlText.trim().split("\n").filter(Boolean);
  let threadId = null;
  let resultText = null;

  for (const line of lines) {
    try {
      const event = JSON.parse(line);
      if (event.type === "thread.started") {
        threadId = event.thread_id;
      } else if (event.type === "item.completed") {
        resultText = event.item?.text ?? null;
      }
    } catch {
      // JSON パース失敗行はスキップ
    }
  }

  return { threadId, resultText };
}

async function runCodexExec({ imagePaths, prompt, sessionId }) {
  const outputFile = join(tmpdir(), `codex-audit-${randomBytes(4).toString("hex")}.txt`);

  const args = sessionId
    ? ["exec", "resume", sessionId, prompt, "--json", "-o", outputFile, "--full-auto"]
    : ["exec", prompt, "--json", "-o", outputFile, "--full-auto"];

  for (const imgPath of imagePaths) {
    args.push("-i", imgPath);
  }

  return new Promise((resolvePromise, reject) => {
    const child = spawn("codex", args, { stdio: ["ignore", "pipe", "pipe"] });

    let stdout = "";
    let stderr = "";

    child.stdout.on("data", (chunk) => { stdout += chunk; });
    child.stderr.on("data", (chunk) => { stderr += chunk; });

    child.on("close", async (code) => {
      if (code !== 0) {
        await unlink(outputFile).catch(() => {});
        reject(new Error(stderr || `codex exited with code ${code}`));
        return;
      }

      const parsedOutput = parseJsonlOutput(stdout);

      if (sessionId && parsedOutput.threadId && parsedOutput.threadId !== sessionId) {
        await unlink(outputFile).catch(() => {});
        reject(new Error(`セッション "${sessionId}" が見つかりません（期限切れの可能性）`));
        return;
      }

      if (!parsedOutput.resultText) {
        try {
          parsedOutput.resultText = await readFile(outputFile, "utf-8");
        } catch {
          // ファイルもなければ null のまま
        }
      }

      await unlink(outputFile).catch(() => {});
      resolvePromise(parsedOutput);
    });

    child.on("error", async (err) => {
      await unlink(outputFile).catch(() => {});
      if (err.code === "ENOENT") {
        reject(new Error("codex コマンドが見つかりません。Codex CLI がインストールされているか確認してください"));
      } else {
        reject(err);
      }
    });
  });
}

async function main() {
  if (!parsed.customPrompt) {
    console.error("Error: --ask は必須です");
    process.exit(1);
  }

  const result = await runCodexExec({
    imagePaths: parsed.imagePaths,
    prompt: parsed.customPrompt,
    sessionId: parsed.sessionId,
  });

  if (!result.resultText) {
    console.error("Error: レスポンスにテキストが含まれていません");
    process.exit(1);
  }

  console.log(result.resultText);

  if (!parsed.sessionId && result.threadId) {
    console.error(`--session ${result.threadId} を指定してください`);
  }
}

main().catch((err) => {
  console.error(`Error: ${err.message}`);
  process.exit(1);
});
