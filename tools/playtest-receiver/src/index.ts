import type { Env } from "./env";
import { fail, notFound } from "./http";
import { routeAdmin } from "./routes/admin";
import { routeSession } from "./routes/session";
import { routeUploads } from "./routes/uploads";

// Steam Web APIへのfetchを引数で受ける。テストは差し替え、本番はglobalThis.fetchを渡す
// The Steam Web API fetch is injected so tests can replace it; production passes globalThis.fetch
export async function handle(request: Request, env: Env, steamFetch: typeof fetch): Promise<Response> {
  const url = new URL(request.url);
  const segments = splitPathSegments(url.pathname);
  if (segments === null) return fail("bad-path", 400);

  const session = await routeSession(request, env, steamFetch, segments);
  if (session !== null) return session;

  const uploads = await routeUploads(request, env, segments);
  if (uploads !== null) return uploads;

  const admin = await routeAdmin(request, env, segments);
  if (admin !== null) return admin;

  // 未知パスだけがR1逐語の {"error":"not_found"} を返す経路
  // Only the unknown-path route answers with R1's verbatim {"error":"not_found"}
  console.warn(`[router] rejected unknown path: ${url.pathname}`);
  return notFound();
}

// 先頭の"/"由来の空要素だけを落とす。"//"や末尾"/"由来の空セグメントは残し、各routeのisSafeSegment等に届かせて400にする
// Drops only the leading empty element from the leading "/"; empty segments from "//" or a trailing "/" are kept so each route's isSafeSegment etc. can turn them into a 400
// クライアントはセグメントをpercent-encodeして送る（空白・#・日本語を含むファイル名）。ここで1回だけ復号し、以降は実名で扱う
// The client percent-encodes each segment (names with spaces, #, or Japanese), so it is decoded exactly once here and handled by its real name afterwards
function splitPathSegments(pathname: string): string[] | null {
  const raw = pathname.split("/").slice(1);
  const decoded: string[] = [];
  for (const segment of raw) {
    // decodeURIComponentは不正なpercent列で例外を投げる外部入力の境界。URLは他人が自由に書けるのでここで畳む
    // decodeURIComponent throws on a malformed percent sequence, and the URL is attacker-controlled input, so the boundary is folded here
    try {
      decoded.push(decodeURIComponent(segment));
    } catch {
      console.warn(`[router] rejected a path with a malformed percent-encoding: ${pathname}`);
      return null;
    }
  }
  return decoded;
}

export default {
  fetch(request: Request, env: Env): Promise<Response> {
    return handle(request, env, globalThis.fetch);
  },
};
