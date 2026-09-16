import { TOKEN_TTL_SECONDS } from "./contract";
import { constantTimeEquals } from "./http";

interface TokenPayload {
  sub: string;
  iat: number;
  exp: number;
}

// misconfiguredはサーバー側の設定欠落。呼び出し側はクライアントの落ち度（401）と区別して500を返す
// misconfigured is a missing server-side setting; the caller answers 500, distinct from a client fault (401)
export type TokenVerification = { kind: "ok"; steamId: string } | { kind: "rejected"; reason: string } | { kind: "misconfigured" };

// 失効時刻の計算はここ1箇所。署名するexpとセッション応答のexpiresAtが必ず一致する
// Expiry is computed only here, so the signed exp and the session response's expiresAt always agree
export function tokenExpiresAtSeconds(nowSeconds: number): number {
  return nowSeconds + TOKEN_TTL_SECONDS;
}

// 秘密鍵が無ければ署名できない。呼び出し側が500を返せるようnullへ畳む（例外にはしない）
// Without a secret there is nothing to sign, so this collapses to null and lets the caller answer 500
export async function signToken(secret: string, steamId: string, nowSeconds: number): Promise<string | null> {
  if (!isSecretConfigured(secret)) return null;
  const header = encode(JSON.stringify({ alg: "HS256", typ: "JWT" }));
  const payload: TokenPayload = { sub: steamId, iat: nowSeconds, exp: tokenExpiresAtSeconds(nowSeconds) };
  const body = encode(JSON.stringify(payload));
  const signature = await sign(secret, `${header}.${body}`);
  return `${header}.${body}.${signature}`;
}

// 検証は「設定・形・署名・期限」の順。設定欠落だけはmisconfigured、それ以外の失敗は理由付きrejected
// Verification checks config, shape, signature, expiry; only a missing secret is misconfigured, other failures are rejected with a reason
export async function verifyToken(secret: string, token: string, nowSeconds: number): Promise<TokenVerification> {
  if (!isSecretConfigured(secret)) return { kind: "misconfigured" };
  const parts = token.split(".");
  if (parts.length !== 3) return reject("bad-format");
  const [header, body, signature] = parts as [string, string, string];

  const expected = await sign(secret, `${header}.${body}`);
  if (!constantTimeEquals(signature, expected)) return reject("bad-signature");

  const payload = decodePayload(body);
  if (typeof payload === "string") return reject(payload);
  if (payload.exp <= nowSeconds) return reject("expired");
  if (payload.sub.length === 0) return reject("empty-sub");
  return { kind: "ok", steamId: payload.sub };
}

function reject(reason: string): TokenVerification {
  console.warn(`[token] rejected: ${reason}`);
  return { kind: "rejected", reason };
}

// 秘密鍵の欠落は全経路で同じ形で検出する。ADMIN_KEYと同じく内容を見る前に落とす
// A missing secret is detected the same way everywhere, before touching content, just like ADMIN_KEY
function isSecretConfigured(secret: string): boolean {
  if (secret) return true;
  console.warn("[token] SESSION_HMAC_SECRET is not configured");
  return false;
}

// 戻り値の文字列は拒否理由。呼び出し側がrejectでwarnする
// A string return value is the rejection reason, which the caller warns via reject
function decodePayload(body: string): TokenPayload | "bad-payload-shape" | "bad-payload-encoding" {
  // 外から来た文字列のJSONパースは境界。壊れた入力は理由へ隔離する
  // Parsing a caller-supplied string is a boundary; malformed input is isolated into a reason
  try {
    const text = atob(body.replace(/-/g, "+").replace(/_/g, "/"));
    const parsed = JSON.parse(text) as Partial<TokenPayload> | null;
    if (typeof parsed?.sub !== "string" || typeof parsed.exp !== "number" || typeof parsed.iat !== "number") {
      return "bad-payload-shape";
    }
    return { sub: parsed.sub, iat: parsed.iat, exp: parsed.exp };
  } catch {
    return "bad-payload-encoding";
  }
}

// 秘密鍵をそのまま生バイトのHMAC鍵にする（HS256の定義どおり。導出やsaltは挟まない）
// The secret is the raw HMAC key exactly as HS256 defines it; no derivation or salt is applied
async function sign(secret: string, message: string): Promise<string> {
  const key = await crypto.subtle.importKey(
    "raw",
    new TextEncoder().encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const mac = await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(message));
  return base64Url(new Uint8Array(mac));
}

function encode(text: string): string {
  return base64Url(new TextEncoder().encode(text));
}

// JWTのbase64urlはURL安全な字種で、末尾のパディング`=`を必ず落とす
// JWT's base64url uses the URL-safe alphabet and always drops the trailing `=` padding
function base64Url(bytes: Uint8Array): string {
  let binary = "";
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}
