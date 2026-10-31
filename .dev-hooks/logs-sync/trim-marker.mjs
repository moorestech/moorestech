// 削った文字列の代わりに置く目印と、それを元の文字列へ戻す処理（削る側の自己検証と復元CLIで共用）
// Markers that replace trimmed strings, and their restoration (shared by the trimmer's self-check and the restore CLI).
//
//   {"$trim":"git",  repo,url,commit,path,offset,length,sha256[,catn]}  コミット内のファイルの一部 / a slice of a committed file
//   {"$trim":"logs", path,offset,length,sha256[,catn]}                  logs repo に退避済みのファイルの一部 / a slice of a file already in the logs repo
//   {"$trim":"blob", sha256}                                           logs repo の blobs/ に1回だけ置いた本文 / text stored once under blobs/
//   {"$trim":"uncatn", at:[...]}                                         同じ行の行番号つき表示から番号を外したもの / the numbered display at `at`, numbers stripped
//   {"$trim":"same", at:[...]}                                           同じ行の別の場所と同じ文字列 / equals another string in the same line

import { isDeepStrictEqual } from "node:util";
import { sha256 } from "./git-source.mjs";

export const MARKER_KEY = "$trim";

export function isMarker(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value) && typeof value[MARKER_KEY] === "string";
}

// Read ツールの表示形式（行番号 + タブ）を元テキストから組み立てる
// Rebuild the Read tool's display form (line number + tab) from raw text.
export function renderCatN(text, startLine) {
  return text.split("\n").map((line, i) => `${startLine + i}\t${line}`).join("\n");
}

// 行番号つき表示から元テキストと開始行を取り出す（連番でなければ null）
// Recover raw text and start line from the numbered display; null unless numbering is consecutive.
export function parseCatN(display) {
  const lines = display.split("\n");
  const first = /^(\d+)\t/.exec(lines[0]);
  if (!first) return null;
  const startLine = Number(first[1]);
  const raw = [];
  for (const [i, line] of lines.entries()) {
    const prefix = `${startLine + i}\t`;
    if (!line.startsWith(prefix)) return null;
    raw.push(line.slice(prefix.length));
  }
  return { text: raw.join("\n"), startLine };
}

// 行オブジェクト内の目印をすべて元に戻した新しいオブジェクトを返す。戻せない目印は理由つきで投げる
// Return a copy of the line with every marker restored; throws with the reason for any unrestorable marker.
export function restoreLine(line, { git, readBlob, readLogsFile, repoFor }) {
  const restored = mapStrings(line, (marker) => {
    if (marker[MARKER_KEY] === "git") {
      const text = git.resolve(marker, repoFor?.(marker));
      return marker.catn === undefined ? text : renderCatN(text, marker.catn);
    }
    if (marker[MARKER_KEY] === "blob") return readBlob(marker.sha256);
    if (marker[MARKER_KEY] === "logs") {
      const text = readLogsFile(marker.path).slice(marker.offset, marker.offset + marker.length);
      if (sha256(text) !== marker.sha256) throw new Error(`sha256 mismatch: ${marker.path}@${marker.offset}`);
      return marker.catn === undefined ? text : renderCatN(text, marker.catn);
    }
    return marker;
  });
  return mapStrings(restored, (marker) => {
    const kind = marker[MARKER_KEY];
    if (kind !== "same" && kind !== "uncatn") throw new Error(`unknown marker: ${kind}`);
    const target = marker.at.reduce((node, key) => node?.[key], restored);
    if (typeof target !== "string") throw new Error(`${kind}-marker target missing: ${marker.at.join(".")}`);
    if (kind === "same") return target;
    const parsed = parseCatN(target);
    if (!parsed) throw new Error(`uncatn target is not a numbered display: ${marker.at.join(".")}`);
    return parsed.text;
  });
}

// 削った行が元の行へ正確に戻ることを確かめる（戻らなければ削らずに原文を使う）
// Verify a trimmed line restores exactly to the original (callers fall back to the original otherwise).
export function restoresTo(trimmed, original, ctx) {
  // silent-ok: 戻せない削り方は採用せず原文を保存するので記録は欠けない / 欠落が無いため通知は不要
  try {
    return isDeepStrictEqual(restoreLine(trimmed, ctx), original);
  } catch {
    return false;
  }
}

// 目印を見つけるたびに fn で置き換えながら、オブジェクトを複製する
// Deep-copy a value, replacing each marker with fn(marker).
function mapStrings(value, fn) {
  if (isMarker(value)) return fn(value);
  if (Array.isArray(value)) return value.map((v) => mapStrings(v, fn));
  if (value !== null && typeof value === "object") {
    const out = {};
    for (const [k, v] of Object.entries(value)) out[k] = mapStrings(v, fn);
    return out;
  }
  return value;
}
