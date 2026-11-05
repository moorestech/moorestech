import type { Env } from "../../env";
import { fail, json, requireAdmin, requireMethod } from "../../http";
import { issueDevToken, listDevTokens, revokeDevToken } from "../../devTokens/devTokenStore";
import { MACHINE_PATTERN } from "../../devTokens/developerSubject";

// 管理キーは経路の長さやメソッドの検査より先に検証する
// Authenticate the admin key before validating path length or method
export async function routeAdminDevTokens(request: Request, env: Env, segments: string[]): Promise<Response | null> {
  if (segments[0] !== "v1" || segments[1] !== "admin" || segments[2] !== "dev-tokens") return null;
  const denied = requireAdmin(request, env);
  if (denied !== null) return denied;
  if (segments.length === 3) {
    const deniedMethod = requireMethod(request, ["GET", "POST"], "/v1/admin/dev-tokens");
    if (deniedMethod !== null) return deniedMethod;
    if (request.method === "GET") return json({ machines: await listDevTokens(env) });
    return issue(request, env);
  }
  if (segments.length === 4) {
    const deniedMethod = requireMethod(request, ["DELETE"], "/v1/admin/dev-tokens/{machine}");
    if (deniedMethod !== null) return deniedMethod;
    const machine = segments[3]!;
    const result = await revokeDevToken(env, machine);
    if (result.kind === "revoked") return json({ revoked: machine });
    if (result.kind === "superseded") return fail("revocation-superseded", 409);
  }
  console.warn("[dev-tokens] rejected: machine or route not found");
  return fail("not-found", 404);
}

async function issue(request: Request, env: Env): Promise<Response> {
  let body: unknown;
  // 管理者のJSONも外部入力境界で検査する
  // Admin JSON is also validated at the external input boundary
  try {
    body = await request.json();
  } catch {
    console.warn("[dev-tokens] rejected: body is not JSON");
    return fail("bad-machine", 400);
  }
  const machine = typeof body === "object" && body !== null && "machine" in body ? body.machine : null;
  if (typeof machine !== "string" || !MACHINE_PATTERN.test(machine)) {
    console.warn("[dev-tokens] rejected: bad machine name");
    return fail("bad-machine", 400);
  }
  const result = await issueDevToken(env, machine, new Date());
  if (result.kind === "exists") {
    console.warn(`[dev-tokens] rejected: ${machine} already has a token`);
    return fail("machine-already-has-token", 409);
  }
  return json({ machine, token: result.token }, 201);
}
