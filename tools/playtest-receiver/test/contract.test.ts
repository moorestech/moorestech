import { describe, expect, it } from "vitest";
import contract from "../contract.json";
import { ACKED_MARKER, READY_MARKER } from "../src/bundleMarkers";
import { CONTRACT_KINDS, MAX_FILE_BYTES, RESERVED_UPLOAD_SEGMENTS, STEAM_IDENTITY, TOKEN_TTL_SECONDS } from "../src/contract";
import { KIND_PREFIX } from "../src/keys";

// contract.jsonはC#クライアントと共有する正本。Workerの実効値がそこから外れていないことを固定する
// contract.json is the source shared with the C# client; this pins the Worker's effective values to it
describe("contract.json", () => {
  it("Workerの定数がcontract.jsonと一致する", () => {
    expect(STEAM_IDENTITY).toBe(contract.steamIdentity);
    expect(MAX_FILE_BYTES).toBe(contract.maxFileBytes);
    expect([...RESERVED_UPLOAD_SEGMENTS]).toEqual(contract.reservedUploadSegments);
    expect(CONTRACT_KINDS).toEqual(contract.kinds);
    expect(TOKEN_TTL_SECONDS).toBe(contract.tokenTtlSeconds);
  });

  it("kindの表とマーカー名が契約から外れていない", () => {
    expect(Object.keys(KIND_PREFIX)).toEqual(contract.kinds);
    expect(contract.reservedUploadSegments).toContain(READY_MARKER);
    expect(contract.reservedUploadSegments).toContain(ACKED_MARKER);
    expect(contract.reservedUploadSegments).toContain("complete");
  });

  it("契約値はこれまでの実効値を保つ", () => {
    expect(contract).toEqual({
      steamIdentity: "moorestech-playtest",
      maxFileBytes: 100 * 1024 * 1024,
      reservedUploadSegments: ["READY", "ACKED", "complete"],
      kinds: ["report", "progress"],
      tokenTtlSeconds: 3600,
    });
  });
});
