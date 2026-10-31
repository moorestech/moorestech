// 記録から削ったファイル内容を、消えないコミットの中から見つけて元に戻すための git 窓口
// Git access used to locate trimmed file content inside commits that will not disappear, and to restore it.

import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { realpathSync } from "node:fs";
import { dirname, isAbsolute, relative } from "node:path";

export function sha256(text) {
  return createHash("sha256").update(text, "utf8").digest("hex");
}

// 1回の同期の中で同じ git 呼び出しを繰り返さないための記憶つき窓口
// A memoising facade so one sync run never repeats the same git call.
export function createGitSource() {
  const toplevels = new Map();
  const durable = new Map();
  const blobs = new Map();

  // absPath を含む repo の最上位（git 管理外なら null）
  // Top level of the repo containing absPath (null outside git).
  function toplevelOf(absPath) {
    const dir = dirname(absPath);
    if (!toplevels.has(dir)) toplevels.set(dir, git(dir, ["rev-parse", "--show-toplevel"]));
    return toplevels.get(dir);
  }

  // リモートに載っていて消えないコミットだけを候補にする（既定ブランチ、push 済みなら HEAD）
  // Only commits already on a remote survive branch cleanup: the default branch, plus HEAD when pushed.
  function durableCommits(top) {
    if (durable.has(top)) return durable.get(top);
    const commits = [];
    const base = ["origin/HEAD", "origin/master", "origin/main"]
      .map((ref) => git(top, ["rev-parse", "--verify", "-q", `${ref}^{commit}`]))
      .find(Boolean);
    if (base) commits.push(base);
    const head = git(top, ["rev-parse", "--verify", "-q", "HEAD"]);
    if (head && head !== base && git(top, ["branch", "-r", "--contains", head])) commits.push(head);
    const url = git(top, ["remote", "get-url", "origin"]) ?? "";
    const entry = { commits, url };
    durable.set(top, entry);
    return entry;
  }

  function blobText(top, commit, path) {
    const key = `${top}\0${commit}\0${path}`;
    if (!blobs.has(key)) blobs.set(key, git(top, ["cat-file", "blob", `${commit}:${path}`], { trim: false }));
    return blobs.get(key);
  }

  // text が absPath のどこかの版にそのまま含まれていれば、その場所を指す参照を返す（無ければ null）
  // Return a reference to where text appears verbatim in a durable version of absPath, or null.
  function locate(absPath, text) {
    if (!isAbsolute(absPath) || text.length === 0) return null;
    const top = toplevelOf(absPath);
    if (!top) return null;
    // git は実パス（/var → /private/var 等）で答えるので、記録上のパスも実パスにそろえて比べる
    // git answers with real paths (e.g. /var -> /private/var), so compare against the real path of absPath.
    const path = relative(top, realPath(absPath));
    if (path.startsWith("..")) return null;
    const { commits, url } = durableCommits(top);
    for (const commit of commits) {
      const blob = blobText(top, commit, path);
      const offset = blob === null ? -1 : blob.indexOf(text);
      if (offset >= 0) return { $trim: "git", repo: top, url, commit, path, offset, length: text.length, sha256: sha256(text) };
    }
    return null;
  }

  // 参照から元の文字列を取り出す。取り出せない・ハッシュ不一致なら理由つきで投げる
  // Resolve a reference back to the original text; throw with the reason when missing or mismatched.
  function resolve(ref, repoOverride) {
    const top = repoOverride ?? ref.repo;
    const blob = blobText(top, ref.commit, ref.path);
    if (blob === null) throw new Error(`git blob not found: ${top} ${ref.commit}:${ref.path}`);
    const text = blob.slice(ref.offset, ref.offset + ref.length);
    if (sha256(text) !== ref.sha256) throw new Error(`sha256 mismatch: ${ref.commit}:${ref.path}@${ref.offset}`);
    return text;
  }

  return { locate, resolve };
}

function realPath(path) {
  // silent-ok: 消えたファイルは元の表記のまま比べ、一致しなければ削らず原文を残すので欠けない / 欠落が無いため通知は不要
  try {
    return realpathSync(path);
  } catch {
    return path;
  }
}

// gitコマンドの薄いラッパー。失敗は null（呼び出し側が「削らない」「戻せない」と判断する）
// Thin git wrapper; failures return null so callers decide "do not trim" or "cannot restore".
function git(dir, args, { trim = true } = {}) {
  // 外部境界: git 管理外・ブランチ不在は通常起こる。null を受けた側が削らずに原文を残すので欠落にならない
  // External boundary: non-repos and missing refs are normal; callers keep the original text on null, so nothing is lost.
  try {
    const out = execFileSync("git", ["-C", dir, ...args], {
      encoding: "utf8",
      timeout: 10000,
      maxBuffer: 256 * 1024 * 1024,
      stdio: ["ignore", "pipe", "ignore"],
    });
    return trim ? out.trim() || null : out;
  } catch {
    return null;
  }
}
