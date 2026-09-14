import type { Env } from "./env";
import { notFound } from "./http";
import { routeSession } from "./routes/session";
import { routeUploads } from "./routes/uploads";

// Steam Web APIへのfetchを引数で受ける。テストは差し替え、本番はglobalThis.fetchを渡す
// The Steam Web API fetch is injected so tests can replace it; production passes globalThis.fetch
export async function handle(request: Request, env: Env, steamFetch: typeof fetch): Promise<Response> {
  const url = new URL(request.url);
  const segments = splitPathSegments(url.pathname);

  const session = await routeSession(request, env, steamFetch, segments);
  if (session !== null) return session;

  const uploads = await routeUploads(request, env, segments);
  if (uploads !== null) return uploads;

  // 未知パスだけがR1逐語の {"error":"not_found"} を返す経路
  // Only the unknown-path route answers with R1's verbatim {"error":"not_found"}
  console.warn(`[router] rejected unknown path: ${url.pathname}`);
  return notFound();
}

// 先頭の"/"由来の空要素だけを落とす。"//"や末尾"/"由来の空セグメントは残し、各routeのisSafeSegment等に届かせて400にする
// Drops only the leading empty element from the leading "/"; empty segments from "//" or a trailing "/" are kept so each route's isSafeSegment etc. can turn them into a 400
function splitPathSegments(pathname: string): string[] {
  return pathname.split("/").slice(1);
}

export default {
  fetch(request: Request, env: Env): Promise<Response> {
    return handle(request, env, globalThis.fetch);
  },
};
