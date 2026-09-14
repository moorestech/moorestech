import type { Env } from "./env";
import { fail, notFound } from "./http";
import { isKind, isSafeSegment } from "./keys";
import { postSession } from "./session";
import { completeUpload, putUpload } from "./uploads";

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

  if (segments[0] === "v1" && segments[1] === "uploads") {
    // "/uploads/kind/../a.txt" は正規化でkind自体が畳まれ3セグメントになりうる。既知prefix配下として400で扱う
    // "/uploads/kind/../a.txt" normalizes away the kind segment itself, landing at 3 segments; still answer 400 under this known prefix, not a generic 404
    if (segments.length < 4) {
      console.warn(`[router] rejected a malformed upload path: ${url.pathname}`);
      return fail("bad-path", 400);
    }
    const kind = segments[2] as string;
    const id = segments[3] as string;
    if (!isKind(kind)) {
      console.warn(`[router] rejected an unknown upload kind: ${kind}`);
      return fail("bad-kind", 400);
    }
    // idもキーの一部なので同じ検査を通す。ここを抜かすと ".." のidでprefixを抜けられる
    // The id is part of the key too, so it takes the same check; skipping it would let ".." escape the prefix
    if (!isSafeSegment(id)) {
      console.warn(`[router] rejected an unsafe upload id: ${id}`);
      return fail("bad-path", 400);
    }

    const rest = segments.slice(4);
    if (rest.length === 1 && rest[0] === "complete") {
      if (request.method !== "POST") {
        console.warn(`[router] rejected method ${request.method} for ${url.pathname}`);
        return fail("method-not-allowed", 405);
      }
      return completeUpload(request, env, kind, id);
    }
    if (request.method !== "PUT") {
      console.warn(`[router] rejected method ${request.method} for ${url.pathname}`);
      return fail("method-not-allowed", 405);
    }
    return putUpload(request, env, kind, id, rest);
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
