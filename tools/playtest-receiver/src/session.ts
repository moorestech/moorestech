import { readAllowlist } from "./allowlist";
import type { Env } from "./env";
import { fail, json } from "./http";
import { authenticateUserTicket } from "./steamAuth";
import { signToken } from "./token";

// hexエンコードされたバイナリチケットは必ず偶数長。奇数長は形の時点で弾く
// A hex-encoded binary ticket is always even length; odd lengths are rejected by shape alone
const TICKET_PATTERN = /^(?:[0-9a-fA-F]{2}){1,4096}$/;

export async function postSession(request: Request, env: Env, steamFetch: typeof fetch): Promise<Response> {
  const ticket = await readTicket(request);
  if (ticket === null) return fail("bad-request", 400);

  const verified = await authenticateUserTicket(steamFetch, env, ticket);
  if (verified.steamId === null) return fail("invalid-ticket", 401);

  const allowlist = await readAllowlist(env.BUCKET);
  if (!allowlist.includes(verified.steamId)) {
    console.warn(`[session] ${verified.steamId} is not on the allowlist`);
    return fail("not-allowed", 403);
  }

  const token = await signToken(env.SESSION_HMAC_SECRET, verified.steamId, Math.floor(Date.now() / 1000));
  if (token === null) {
    // SESSION_HMAC_SECRET未設定はtoken.ts側で既にwarn済み。ここでは200へtoken:nullを漏らさず500へ畳む
    // token.ts already warns about a missing SESSION_HMAC_SECRET; here we must not leak token:null in a 200
    console.warn("[session] cannot issue a token because signing is unavailable");
    return fail("server-misconfigured", 500);
  }
  return json({ steamId: verified.steamId, allowed: true, token });
}

async function readTicket(request: Request): Promise<string | null> {
  // クライアントが送るJSONのパースは外部入力境界。壊れた入力は400へ隔離する
  // Parsing client-supplied JSON is an external-input boundary; malformed input is isolated into a 400
  let body: { ticket?: unknown };
  try {
    body = (await request.json()) as { ticket?: unknown };
  } catch (error) {
    console.warn(`[session] rejected: body is not JSON (${error instanceof Error ? error.message : String(error)})`);
    return null;
  }
  if (typeof body.ticket !== "string" || !TICKET_PATTERN.test(body.ticket)) {
    console.warn("[session] rejected: ticket is not a hex string");
    return null;
  }
  return body.ticket;
}
