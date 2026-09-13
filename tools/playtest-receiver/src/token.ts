export const TOKEN_TTL_SECONDS = 3600;

interface TokenPayload {
  sub: string;
  iat: number;
  exp: number;
}

export async function signToken(secret: string, steamId: string, nowSeconds: number): Promise<string> {
  const header = encode(JSON.stringify({ alg: "HS256", typ: "JWT" }));
  const payload: TokenPayload = { sub: steamId, iat: nowSeconds, exp: nowSeconds + TOKEN_TTL_SECONDS };
  const body = encode(JSON.stringify(payload));
  const signature = await sign(secret, `${header}.${body}`);
  return `${header}.${body}.${signature}`;
}

// 検証は「形・署名・期限」の3段。どれで落ちてもnullへ畳み、呼び出し側は401だけを返す
// Verification is shape, signature, expiry; any failure collapses to null and the caller answers 401
export async function verifyToken(secret: string, token: string, nowSeconds: number): Promise<string | null> {
  const parts = token.split(".");
  if (parts.length !== 3) {
    console.warn("[token] rejected: bad-format");
    return null;
  }
  const [header, body, signature] = parts as [string, string, string];

  const expected = await sign(secret, `${header}.${body}`);
  if (signature.length !== expected.length) {
    console.warn("[token] rejected: bad-signature");
    return null;
  }
  let diff = 0;
  for (let i = 0; i < expected.length; i++) diff |= signature.charCodeAt(i) ^ expected.charCodeAt(i);
  if (diff !== 0) {
    console.warn("[token] rejected: bad-signature");
    return null;
  }

  const payload = decodePayload(body);
  if (payload === null) return null;
  if (payload.exp <= nowSeconds) {
    console.warn("[token] rejected: expired");
    return null;
  }
  if (payload.sub.length === 0) {
    console.warn("[token] rejected: empty-sub");
    return null;
  }
  return payload.sub;
}

function decodePayload(body: string): TokenPayload | null {
  // 外から来た文字列のJSONパースは境界。壊れた入力はnullへ隔離する
  // Parsing a caller-supplied string is a boundary; malformed input is isolated into null
  try {
    const text = atob(body.replace(/-/g, "+").replace(/_/g, "/"));
    const parsed = JSON.parse(text) as Partial<TokenPayload>;
    if (typeof parsed.sub !== "string" || typeof parsed.exp !== "number" || typeof parsed.iat !== "number") {
      console.warn("[token] rejected: bad-payload-shape");
      return null;
    }
    return { sub: parsed.sub, iat: parsed.iat, exp: parsed.exp };
  } catch {
    console.warn("[token] rejected: bad-payload-encoding");
    return null;
  }
}

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

function base64Url(bytes: Uint8Array): string {
  let binary = "";
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}
