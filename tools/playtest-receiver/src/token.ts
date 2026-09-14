export const TOKEN_TTL_SECONDS = 3600;

interface TokenPayload {
  sub: string;
  iat: number;
  exp: number;
}

// 秘密鍵が無ければ署名できない。呼び出し側が500を返せるようnullへ畳む（例外にはしない）
// Without a secret there is nothing to sign, so this collapses to null and lets the caller answer 500
export async function signToken(secret: string, steamId: string, nowSeconds: number): Promise<string | null> {
  if (!isSecretConfigured(secret)) return null;
  const header = encode(JSON.stringify({ alg: "HS256", typ: "JWT" }));
  const payload: TokenPayload = { sub: steamId, iat: nowSeconds, exp: nowSeconds + TOKEN_TTL_SECONDS };
  const body = encode(JSON.stringify(payload));
  const signature = await sign(secret, `${header}.${body}`);
  return `${header}.${body}.${signature}`;
}

// 検証は「形・署名・期限」の3段。どれで落ちてもnullへ畳み、呼び出し側は401だけを返す
// Verification is shape, signature, expiry; any failure collapses to null and the caller answers 401
export async function verifyToken(secret: string, token: string, nowSeconds: number): Promise<string | null> {
  if (!isSecretConfigured(secret)) return null;
  const parts = token.split(".");
  if (parts.length !== 3) {
    console.warn("[token] rejected: bad-format");
    return null;
  }
  const [header, body, signature] = parts as [string, string, string];

  const expected = await sign(secret, `${header}.${body}`);
  if (!signatureMatches(signature, expected)) {
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

// 秘密鍵の欠落は全経路で同じ形で拒否する。ADMIN_KEYと同じく内容を見る前に落とす
// A missing secret is rejected the same way everywhere, before touching content, just like ADMIN_KEY
function isSecretConfigured(secret: string): boolean {
  if (secret) return true;
  console.warn("[token] rejected: SESSION_HMAC_SECRET is not configured");
  return false;
}

// 署名比較は長さも内容も定数時間で。理由は呼び出し元が1箇所でログする
// Signatures are compared in constant time, length included; the caller logs the single reason
function signatureMatches(signature: string, expected: string): boolean {
  let diff = signature.length ^ expected.length;
  const length = Math.max(signature.length, expected.length);
  for (let i = 0; i < length; i++) diff |= (signature.charCodeAt(i) || 0) ^ (expected.charCodeAt(i) || 0);
  return diff === 0;
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
