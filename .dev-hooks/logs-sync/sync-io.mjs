// logs-sync の入出力の下回り（増分コピー・欠落の報告・git 呼び出し）
// Low-level I/O for logs-sync: incremental copies, loss reports and git calls.

import { execFileSync } from "node:child_process";
import { appendFileSync, copyFileSync, existsSync, mkdirSync, readdirSync, statSync, utimesSync } from "node:fs";
import { dirname, join } from "node:path";

// GitHubの100MB上限を超えるファイルはpushできないため、余裕を見てこの大きさを超えたら同期しない
// Files over GitHub's 100MB limit can never be pushed, so anything above this margin is not synced
export const GITHUB_PUSHABLE_MAX_BYTES = 95 * 1024 * 1024;

// exit 0のフックではstderrが画面に出ないため、同じ行を.state/logs-sync-skipped.logへも残す。
// このログは Hermes cron（moorestech-logs-skipped-watch）が Discord へ送る
// A hook exiting 0 has its stderr hidden, so the same line also goes to .state/logs-sync-skipped.log,
// which the Hermes cron job (moorestech-logs-skipped-watch) forwards to Discord.
export function createReporter(stateDir) {
  return function report(line) {
    console.error(`logs-sync: ${line}`);
    // 外部境界: 記録ファイルへの追記失敗（容量不足等）はstderrへ出すだけにしてフックを止めない
    // External boundary: a failed append (e.g. disk full) is reported to stderr only and does not stop the hook
    try {
      appendFileSync(join(stateDir, "logs-sync-skipped.log"), `${new Date().toISOString()} ${line}\n`);
    } catch (error) {
      console.error(`logs-sync: failed to record the loss above: ${error.code ?? ""} ${error.message}`);
    }
  };
}

// 宛先が無い(かつ7日以内のファイル)か、宛先より新しい時だけ書く冪等コピー。write を渡すとコピーの代わりにそれで書く
// Idempotent copy: only when dest is missing (and src is <7 days old) or src is newer. A given write replaces the plain copy.
export function copyIfUpdated(src, dest, report, write = copyFileSync) {
  // 外部境界: 他プロセス管理下のファイル群のためstat/copy失敗はフックを止めず、理由を報告して次へ進む
  // External boundary: stat/copy failures on files owned by other processes don't stop the hook; report the reason and move on.
  let srcStat;
  try {
    srcStat = statSync(src);
  } catch (error) {
    // 元ファイルが無いのは退避すべきものが無いだけで欠落ではない（beadsミラー未生成のclone等）。それ以外のstat失敗は理由を出す
    // A missing source only means there is nothing to archive, not a loss (e.g. a clone without the beads mirror); other stat failures are reported.
    if (error.code !== "ENOENT") report(`stat failed: ${src}: ${error.code} ${error.message}`);
    return;
  }
  try {
    if (existsSync(dest)) {
      // utimesSyncはms精度でしか書けずAPFSのns精度mtimeに常に負けるため、ms切り捨てで比較する
      // utimesSync writes ms precision and always loses to APFS ns mtimes, so compare at floored ms.
      if (Math.floor(srcStat.mtimeMs) <= Math.floor(statSync(dest).mtimeMs)) return;
    } else if (Date.now() - srcStat.mtimeMs > 7 * 24 * 3600 * 1000) {
      return;
    }
    // 同期すべき更新があるのに上限超過で送れない時だけ報告し、考古学repoから黙って欠落させない
    // Report only when a due update cannot be sent over the limit, so the archive never loses a session silently
    if (srcStat.size > GITHUB_PUSHABLE_MAX_BYTES) {
      report(`skipped (over ${GITHUB_PUSHABLE_MAX_BYTES} bytes, GitHub push limit): ${src} size=${srcStat.size}`);
      return;
    }
    mkdirSync(dirname(dest), { recursive: true });
    write(src, dest);
    utimesSync(dest, srcStat.atime, srcStat.mtime);
  } catch (error) {
    report(`copy failed: ${src} -> ${dest}: ${error.code ?? ""} ${error.message}`);
  }
}

// ディレクトリならtrue（stat失敗はfalse）
// True for directories; stat failures yield false.
export function isDirectory(path) {
  // 外部境界: 他プロセス管理下のためstat失敗は握りつぶす
  // External boundary: swallow stat failures on files owned by other processes.
  try {
    return statSync(path).isDirectory();
  } catch {
    return false;
  }
}

// ファイル/ディレクトリのmtimeが今からsec秒以内ならtrue
// True when the file/dir mtime is within sec seconds from now.
export function isNewerThan(path, sec) {
  // 外部境界: stat失敗は「古い」扱い
  // External boundary: treat stat failure as "old".
  try {
    return Date.now() - statSync(path).mtimeMs < sec * 1000;
  } catch {
    return false;
  }
}

// 読めないディレクトリは空配列を返すreaddir
// readdir that returns [] for unreadable directories.
export function safeReaddir(dir) {
  // 外部境界: 存在しない/権限のないディレクトリはfail open
  // External boundary: fail open on missing/unreadable directories.
  try {
    return readdirSync(dir);
  } catch {
    return [];
  }
}

// gitコマンドの薄いラッパー。失敗はnull/空文字で返す
// Thin git wrapper; failures come back as null/empty string.
export function git(dir, args, timeout, input) {
  // 外部境界: 外部プロセス起動の失敗はfail open
  // External boundary: fail open on external process failures.
  try {
    return execFileSync("git", ["-C", dir, ...args], {
      encoding: "utf8",
      timeout: timeout ?? 10000,
      input,
      maxBuffer: 64 * 1024 * 1024,
      stdio: [input === undefined ? "ignore" : "pipe", "pipe", "ignore"],
    }).trim();
  } catch {
    return null;
  }
}
