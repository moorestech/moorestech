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

// 未知パスの404はR1が逐語で指定する形。他のエラー応答（reason形）とは別経路
// The 404 for an unknown path uses the exact shape R1 mandates, distinct from the reason-shaped errors elsewhere
export function notFound(): Response {
  return json({ error: "not_found" }, 404);
}

// 管理APIの共通関門。長さの違いも含めて定数時間で比べ、鍵の推測を助けない
// Shared gate for the admin API; compares in constant time, including length, so it leaks nothing
export function requireAdmin(request: Request, env: Env): Response | null {
  if (!env.ADMIN_KEY) {
    // ADMIN_KEY未設定は空文字同士の比較でfail-openし得るため、内容を見る前に拒否する
    // An unset ADMIN_KEY could compare equal to an empty header, so reject before inspecting content
    console.warn("[admin] rejected: ADMIN_KEY is not configured");
    return fail("unauthorized", 401);
  }
  const presented = request.headers.get("x-admin-key") ?? "";
  if (!constantTimeEquals(presented, env.ADMIN_KEY)) {
    console.warn("[admin] rejected a request without a matching X-Admin-Key");
    return fail("unauthorized", 401);
  }
  return null;
}

// メソッド検査＋warn＋405 fail の3行を1箇所へ。7経路がこの形を個別実装していた
// Bundles method check + warn + 405 fail into one call; 7 routes used to repeat this shape individually
export function requireMethod(request: Request, method: string, label: string): Response | null {
  if (request.method === method) return null;
  console.warn(`[router] rejected method ${request.method} for ${label}`);
  return fail("method-not-allowed", 405);
}

export function constantTimeEquals(a: string, b: string): boolean {
  const left = new TextEncoder().encode(a);
  const right = new TextEncoder().encode(b);
  let diff = left.length ^ right.length;
  const length = Math.max(left.length, right.length);
  for (let i = 0; i < length; i++) diff |= (left[i] ?? 0) ^ (right[i] ?? 0);
  return diff === 0;
}
