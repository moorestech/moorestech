import type { Env } from "./env";

export function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json; charset=utf-8" },
  });
}

export function fail(reason: string, status: number): Response {
  return json({ reason }, status);
}

// 管理APIの共通関門。長さの違いも含めて定数時間で比べ、鍵の推測を助けない
// Shared gate for the admin API; compares in constant time, including length, so it leaks nothing
export function requireAdmin(request: Request, env: Env): Response | null {
  const presented = request.headers.get("x-admin-key") ?? "";
  if (!constantTimeEquals(presented, env.ADMIN_KEY)) {
    console.warn("[admin] rejected a request without a matching X-Admin-Key");
    return fail("unauthorized", 401);
  }
  return null;
}

function constantTimeEquals(a: string, b: string): boolean {
  const left = new TextEncoder().encode(a);
  const right = new TextEncoder().encode(b);
  let diff = left.length ^ right.length;
  const length = Math.max(left.length, right.length);
  for (let i = 0; i < length; i++) diff |= (left[i] ?? 0) ^ (right[i] ?? 0);
  return diff === 0;
}
