import type { Env } from "./env";
import { fail } from "./http";

// Steam Web APIへのfetchを引数で受ける。テストは差し替え、本番はglobalThis.fetchを渡す
// The Steam Web API fetch is injected so tests can replace it; production passes globalThis.fetch
export async function handle(request: Request, env: Env, steamFetch: typeof fetch): Promise<Response> {
  const url = new URL(request.url);
  const segments = url.pathname.split("/").filter((segment) => segment.length > 0);

  if (segments[0] !== "v1") return fail("not-found", 404);

  if (segments.length === 2 && segments[1] === "session") {
    if (request.method !== "POST") return fail("method-not-allowed", 405);
    return fail("not-found", 404);
  }

  return fail("not-found", 404);
}

export default {
  fetch(request: Request, env: Env): Promise<Response> {
    return handle(request, env, globalThis.fetch);
  },
};
