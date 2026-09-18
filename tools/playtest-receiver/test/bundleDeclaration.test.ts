import { describe, expect, it } from "vitest";
import { MAX_BUNDLE_BYTES, MAX_BUNDLE_FILES, MAX_FILE_BYTES } from "../src/contract";
import { parseDeclaration } from "../src/bundleDeclaration";

function files(count: number, bytes = 1) {
  return { files: Array.from({ length: count }, (_, i) => ({ path: `frames/frame_${i}.jpg`, bytes })) };
}

describe("parseDeclaration", () => {
  it("正しい宣言はそのまま通る", () => {
    const result = parseDeclaration({ files: [{ path: "manifest.json", bytes: 10 }, { path: "frames/frame 1.jpg", bytes: 20 }] });
    expect(result).toEqual({ ok: true, files: [{ path: "manifest.json", bytes: 10 }, { path: "frames/frame 1.jpg", bytes: 20 }] });
  });
  it("配列でない・path/bytesが欠ける・bytesが整数でない宣言は400", () => {
    expect(parseDeclaration({}).ok).toBe(false);
    expect(parseDeclaration({ files: [{ path: "a" }] })).toMatchObject({ ok: false, status: 400 });
    expect(parseDeclaration({ files: [{ path: "a", bytes: 1.5 }] })).toMatchObject({ ok: false, status: 400 });
    expect(parseDeclaration({ files: [{ path: "a", bytes: -1 }] })).toMatchObject({ ok: false, status: 400 });
  });
  it("逸脱パス・予約名・重複は400", () => {
    expect(parseDeclaration({ files: [{ path: "../x", bytes: 1 }] })).toMatchObject({ ok: false, status: 400, error: "bad-path" });
    expect(parseDeclaration({ files: [{ path: "READY", bytes: 1 }] })).toMatchObject({ ok: false, status: 400, error: "reserved-name" });
    expect(parseDeclaration({ files: [{ path: "a", bytes: 1 }, { path: "a", bytes: 2 }] })).toMatchObject({ ok: false, status: 400, error: "duplicate-path" });
  });
  it("1ファイル上限超・件数超・総量超は413", () => {
    expect(parseDeclaration({ files: [{ path: "a", bytes: MAX_FILE_BYTES + 1 }] })).toMatchObject({ ok: false, status: 413, error: "too-large" });
    expect(parseDeclaration(files(MAX_BUNDLE_FILES + 1))).toMatchObject({ ok: false, status: 413, error: "too-many-files" });
    expect(parseDeclaration({ files: [{ path: "a", bytes: MAX_FILE_BYTES }, { path: "b", bytes: MAX_FILE_BYTES }, { path: "c", bytes: MAX_BUNDLE_BYTES - 2 * MAX_FILE_BYTES + 1 }] })).toMatchObject({ ok: false, status: 413, error: "bundle-too-large" });
  });
  it("空の宣言は400（何も送らない箱はcompleteできない）", () => {
    expect(parseDeclaration(files(0))).toMatchObject({ ok: false, status: 400, error: "empty-declaration" });
  });
});
