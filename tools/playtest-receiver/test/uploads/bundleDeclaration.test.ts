import { describe, expect, it } from "vitest";
import { MAX_BUNDLE_BYTES, MAX_BUNDLE_FILES, MAX_FILE_BYTES } from "../../src/contract";
import { classifyRedeclaration, parseDeclaration } from "../../src/uploads/bundleDeclaration";

function files(count: number, bytes = 1) {
  return { generation: 1, files: Array.from({ length: count }, (_, i) => ({ path: `frames/frame_${i}.jpg`, bytes })) };
}

describe("parseDeclaration", () => {
  it("正しい宣言はそのまま通る", () => {
    const result = parseDeclaration({ generation: 1, files: [{ path: "manifest.json", bytes: 10 }, { path: "frames/frame 1.jpg", bytes: 20 }] });
    expect(result).toEqual({ ok: true, generation: 1, files: [{ path: "manifest.json", bytes: 10 }, { path: "frames/frame 1.jpg", bytes: 20 }] });
  });
  it("配列でない・path/bytesが欠ける・bytesが整数でない宣言は400", () => {
    expect(parseDeclaration({}).ok).toBe(false);
    expect(parseDeclaration({ generation: 1, files: [{ path: "a" }] })).toMatchObject({ ok: false, status: 400 });
    expect(parseDeclaration({ generation: 1, files: [{ path: "a", bytes: 1.5 }] })).toMatchObject({ ok: false, status: 400 });
    expect(parseDeclaration({ generation: 1, files: [{ path: "a", bytes: -1 }] })).toMatchObject({ ok: false, status: 400 });
  });
  it("逸脱パス・予約名・重複は400", () => {
    expect(parseDeclaration({ generation: 1, files: [{ path: "../x", bytes: 1 }] })).toMatchObject({ ok: false, status: 400, error: "bad-path" });
    expect(parseDeclaration({ generation: 1, files: [{ path: "READY", bytes: 1 }] })).toMatchObject({ ok: false, status: 400, error: "reserved-name" });
    expect(parseDeclaration({ generation: 1, files: [{ path: "a", bytes: 1 }, { path: "a", bytes: 2 }] })).toMatchObject({ ok: false, status: 400, error: "duplicate-path" });
  });
  it("1ファイル上限超・件数超・総量超は413", () => {
    expect(parseDeclaration({ generation: 1, files: [{ path: "a", bytes: MAX_FILE_BYTES + 1 }] })).toMatchObject({ ok: false, status: 413, error: "too-large" });
    expect(parseDeclaration(files(MAX_BUNDLE_FILES + 1))).toMatchObject({ ok: false, status: 413, error: "too-many-files" });
    expect(parseDeclaration({ generation: 1, files: [{ path: "a", bytes: MAX_FILE_BYTES }, { path: "b", bytes: MAX_FILE_BYTES }, { path: "c", bytes: MAX_BUNDLE_BYTES - 2 * MAX_FILE_BYTES + 1 }] })).toMatchObject({ ok: false, status: 413, error: "bundle-too-large" });
  });
  it("空の宣言は400（何も送らない箱はcompleteできない）", () => {
    expect(parseDeclaration(files(0))).toMatchObject({ ok: false, status: 400, error: "empty-declaration" });
  });
});

describe("宣言の世代", () => {
  it.each([["欠落", undefined], ["非整数", 1.5], ["0", 0], ["文字列", "1"]])("generationが%sなら400 bad-request", (_, generation) => {
    expect(parseDeclaration({ generation, files: [{ path: "a", bytes: 1 }] })).toMatchObject({ ok: false, status: 400, error: "bad-request" });
  });

  const stored = { generation: 2, files: [{ path: "a", bytes: 1 }, { path: "b", bytes: 2 }] };
  it.each([
    ["同世代で同じ集合（順序違い）はsame", 2, { b: 2, a: 1 }, "same"],
    ["同世代で縮んだ集合はconflict", 2, { a: 1 }, "conflict"],
    ["同世代で長さ違いはconflict", 2, { a: 1, b: 3 }, "conflict"],
    ["上の世代の部分集合はreplace", 3, { a: 1 }, "replace"],
    ["上の世代で同じ集合もreplace", 3, { a: 1, b: 2 }, "replace"],
    ["上の世代でもファイル追加はconflict", 3, { a: 1, b: 2, c: 1 }, "conflict"],
    ["上の世代でも長さ違いはconflict", 3, { a: 2 }, "conflict"],
    ["下の世代は同じ集合でもconflict", 1, { a: 1, b: 2 }, "conflict"],
  ] as const)("%s", (_, generation, entries, expected) => {
    const requested = { generation, files: Object.entries(entries).map(([path, bytes]) => ({ path, bytes })) };
    expect(classifyRedeclaration(stored, requested)).toBe(expected);
  });
});
