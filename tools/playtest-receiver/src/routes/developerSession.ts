import type { Env } from "../env";
import { fail, json, requireMethod } from "../http";
import { DEV_TOKEN_PATTERN, findMachineByToken } from "../devTokens/devTokenStore";
import { developerSubjectOf } from "../devTokens/developerSubject";
import { signToken, tokenExpiresAtSeconds } from "../token";

// Steam経路と同じセッション応答へ交換する
// Exchange a developer credential for the same session response as Steam
export async function routeDeveloperSession(request: Request, env: Env, segments: string[]): Promise<Response | null> {
  if (segments.length !== 3 || segments[0] !== "v1" || segments[1] !== "session" || segments[2] !== "developer") return null;
  const denied = requireMethod(request, ["POST"], "/v1/session/developer");
  if (denied !== null) return denied;
  const credential = await readToken(request);
  if (credential === null) return fail("bad-request", 400);
  const machine = await findMachineByToken(env, credential);
  if (machine === null) {
    console.warn("[developer-session] rejected: invalid developer token");
    return fail("invalid-developer-token", 401);
  }
  const sub = developerSubjectOf(machine);
  const now = Math.floor(Date.now() / 1000);
  const token = await signToken(env.SESSION_HMAC_SECRET, sub, now);
  if (token === null) return fail("server-misconfigured", 500);
  return json({ steamId: sub, allowed: true, token, expiresAt: new Date(tokenExpiresAtSeconds(now) * 1000).toISOString() });
}

async function readToken(request: Request): Promise<string | null> {
  let body: unknown;
  // JSON本文は外部入力境界。壊れた入力を400へ隔離する
  // The JSON body is an external input boundary; isolate malformed input into a 400
  try {
    body = await request.json();
  } catch {
    console.warn("[developer-session] rejected: body is not JSON");
    return null;
  }
  const token = typeof body === "object" && body !== null && "token" in body ? body.token : null;
  if (typeof token !== "string" || !DEV_TOKEN_PATTERN.test(token)) {
    console.warn("[developer-session] rejected: malformed developer token");
    return null;
  }
  return token;
}
