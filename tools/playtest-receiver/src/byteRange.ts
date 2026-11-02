// Rangeヘッダの解釈結果。単一範囲だけを扱い、複数範囲や読めない形は満たせない扱いにする
// Result of reading a Range header; only a single range is supported, anything else counts as unsatisfiable
export type ByteRangeRequest =
  | { kind: "whole" }
  | { kind: "partial"; offset: number; length: number }
  | { kind: "unsatisfiable"; reason: string };

const SINGLE_RANGE_PATTERN = /^bytes=(\d*)-(\d*)$/;

// bytes=a-b / bytes=a- / bytes=-n を全長sizeに照らして実範囲へ直す。終端が全長を超える指定は末尾までに丸める（RFC 9110）
// Resolves bytes=a-b / bytes=a- / bytes=-n against the total size; an end past the size is clamped to the last byte (RFC 9110)
export function parseByteRange(header: string | null, size: number): ByteRangeRequest {
  if (header === null) return { kind: "whole" };
  const match = SINGLE_RANGE_PATTERN.exec(header.trim());
  if (match === null) return unsatisfiable(`not a single bytes range: ${header}`);
  const [, startText = "", endText = ""] = match;
  if (startText === "" && endText === "") return unsatisfiable(`range has neither start nor end: ${header}`);

  // 末尾n バイト指定。nが全長以上なら全体を返す
  // Suffix form (last n bytes); n at or past the size means the whole object
  if (startText === "") {
    const suffix = toSafeInteger(endText);
    if (suffix === null || suffix === 0 || size === 0) return unsatisfiable(`suffix ${endText} cannot be served from ${size} bytes`);
    const length = Math.min(suffix, size);
    return { kind: "partial", offset: size - length, length };
  }

  // 開始位置つきの指定。開始が全長以上・終端が開始より前なら満たせない
  // Forms with a start; a start at or past the size, or an end before the start, is unsatisfiable
  const start = toSafeInteger(startText);
  if (start === null || start >= size) return unsatisfiable(`start ${startText} is not within ${size} bytes`);
  const requestedEnd = endText === "" ? size - 1 : toSafeInteger(endText);
  if (requestedEnd === null || requestedEnd < start) return unsatisfiable(`end ${endText} is before start ${startText}`);
  const end = Math.min(requestedEnd, size - 1);
  return { kind: "partial", offset: start, length: end - start + 1 };
}

function toSafeInteger(text: string): number | null {
  const value = Number(text);
  return Number.isSafeInteger(value) ? value : null;
}

function unsatisfiable(reason: string): ByteRangeRequest {
  return { kind: "unsatisfiable", reason };
}
