import { describe, expect, it } from "vitest";
import { signToken, verifyToken, TOKEN_TTL_SECONDS } from "../src/token";

const SECRET = "test-hmac-secret";
const NOW = 1_800_000_000;

describe("token", () => {
  it("署名したトークンは同じ秘密鍵で検証を通りsteamIdを返す", async () => {
    const token = await signToken(SECRET, "76561198000000001", NOW);
    expect(await verifyToken(SECRET, token, NOW + 10)).toBe("76561198000000001");
  });

  it("秘密鍵が違うと検証に失敗する", async () => {
    const token = await signToken(SECRET, "76561198000000001", NOW);
    expect(await verifyToken("other-secret", token, NOW + 10)).toBeNull();
  });

  it("有効期限を過ぎると検証に失敗する", async () => {
    const token = await signToken(SECRET, "76561198000000001", NOW);
    expect(await verifyToken(SECRET, token, NOW + TOKEN_TTL_SECONDS + 1)).toBeNull();
  });

  it("本文を改竄すると検証に失敗する", async () => {
    const token = await signToken(SECRET, "76561198000000001", NOW);
    const [header, , signature] = token.split(".");
    const forged = btoa(JSON.stringify({ sub: "76561198000000002", iat: NOW, exp: NOW + 60 }))
      .replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    expect(await verifyToken(SECRET, `${header}.${forged}.${signature}`, NOW + 10)).toBeNull();
  });

  it("形が壊れたトークンは検証に失敗する", async () => {
    expect(await verifyToken(SECRET, "not-a-token", NOW)).toBeNull();
  });
});
