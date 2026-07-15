import { spawn } from "node:child_process";
import { readFile, unlink } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { randomBytes } from "node:crypto";


// codex exec の JSONL 出力をパースして結果を返す
function parseJsonlOutput(jsonlText) {
  const lines = jsonlText.trim().split("\n").filter(Boolean);
  let threadId = null;
  let resultText = null;
  let usage = null;

  for (const line of lines) {
    try {
      const event = JSON.parse(line);
      if (event.type === "thread.started") {
        threadId = event.thread_id;
      } else if (event.type === "item.completed") {
        resultText = event.item?.text ?? null;
      } else if (event.type === "turn.completed") {
        usage = event.usage ?? null;
      }
    } catch {
      // JSON パース失敗行はスキップ
    }
  }

  return { threadId, resultText, usage };
}

// codex exec を spawn し、結果を返す
export async function runCodexExec({ imagePaths, prompt, sessionId, projectDir }) {
  const outputFile = join(tmpdir(), `codex-audit-${randomBytes(4).toString("hex")}.txt`);

  // codex exec はプロンプトを最初の positional arg として期待する
  const args = sessionId
    ? ["exec", "resume", sessionId, prompt, "--json", "-o", outputFile, "--full-auto"]
    : ["exec", prompt, "--json", "-o", outputFile, "--full-auto", "-C", projectDir];

  for (const imgPath of imagePaths) {
    args.push("-i", imgPath);
  }

  const ac = new AbortController();

  return new Promise((resolve, reject) => {
    let settled = false;

    const child = spawn("codex", args, {
      signal: ac.signal,
      stdio: ["ignore", "pipe", "pipe"],
    });

    let stdout = "";
    let stderr = "";

    child.stdout.on("data", (chunk) => { stdout += chunk; });
    child.stderr.on("data", (chunk) => { stderr += chunk; });

    child.on("close", async (code) => {
      if (settled) return;
      settled = true;

      if (code !== 0) {
        await unlink(outputFile).catch(() => {});
        reject(new Error(stderr || `codex exited with code ${code}`));
        return;
      }

      const parsed = parseJsonlOutput(stdout);

      // resume 時にセッションIDが異なる場合は新規セッションが作られた（元セッションが無効）
      if (sessionId && parsed.threadId && parsed.threadId !== sessionId) {
        await unlink(outputFile).catch(() => {});
        reject(new Error(`セッション "${sessionId}" が見つかりません（期限切れの可能性があります）`));
        return;
      }

      // フォールバック: JSONL から結果が取れなければ -o ファイルを読む
      if (!parsed.resultText) {
        try {
          parsed.resultText = await readFile(outputFile, "utf-8");
        } catch {
          // ファイルもなければ null のまま
        }
      }

      await unlink(outputFile).catch(() => {});
      resolve(parsed);
    });

    child.on("error", async (err) => {
      if (settled) return;
      settled = true;

      await unlink(outputFile).catch(() => {});

      if (err.code === "ABORT_ERR" || ac.signal.aborted) {
        reject(new Error("codex が中断されました"));
      } else if (err.code === "ENOENT") {
        reject(new Error("codex コマンドが見つかりません。Codex CLI がインストールされているか確認してください"));
      } else {
        reject(err);
      }
    });
  });
}
