import type { Env } from "./env";
import { fail, notFound } from "./http";
import { postSession } from "./session";

// Steam Web APIへのfetchを引数で受ける。テストは差し替え、本番はglobalThis.fetchを渡す
// The Steam Web API fetch is injected so tests can replace it; production passes globalThis.fetch
export async function handle(request: Request, env: Env, steamFetch: typeof fetch): Promise<Response> {
  const url = new URL(request.url);
  const segments = url.pathname.split("/").filter((segment) => segment.length > 0);

  if (segments[0] === "v1" && segments.length === 2 && segments[1] === "session") {
    if (request.method !== "POST") {
      console.warn(`[router] rejected method ${request.method} for ${url.pathname}`);
      return fail("method-not-allowed", 405);
    }
    return postSession(request, env, steamFetch);
  }

  // 未知パスだけがR1逐語の {"error":"not_found"} を返す経路
  // Only the unknown-path route answers with R1's verbatim {"error":"not_found"}
  console.warn(`[router] rejected unknown path: ${url.pathname}`);
  return notFound();
}

export default {
  fetch(request: Request, env: Env): Promise<Response> {
    return handle(request, env, globalThis.fetch);
  },
};
