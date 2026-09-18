import type { Env } from "../env";
import { fail, requireMethod } from "../http";
import { isKind, isSafeSegment } from "../keys";
import { completeUpload } from "./uploadsComplete";
import { prepareUpload } from "./uploadsPrepare";

// アップロード経路の一致判定とディスパッチをここへ寄せる。一致しなければnullでindex.tsの次の経路へ委ねる
// Path matching and dispatch live here; returns null on a non-match so index.ts can try the next route
export async function routeUploads(request: Request, env: Env, segments: string[]): Promise<Response | null> {
  if (!(segments[0] === "v1" && segments[1] === "uploads")) return null;

  // "/uploads/kind/../a.txt" は正規化でkind自体が畳まれ3セグメントになりうる。既知prefix配下として400で扱う
  // "/uploads/kind/../a.txt" normalizes away the kind segment itself, landing at 3 segments; still answer 400 under this known prefix, not a generic 404
  if (segments.length < 4) {
    console.warn(`[router] rejected a malformed upload path: /${segments.join("/")}`);
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
  if (rest.length === 1 && rest[0] === "prepare") {
    const denied = requireMethod(request, ["POST"], "upload prepare");
    if (denied !== null) return denied;
    return prepareUpload(request, env, kind, id);
  }
  if (rest.length === 1 && rest[0] === "complete") {
    const denied = requireMethod(request, ["POST"], "upload complete");
    if (denied !== null) return denied;
    return completeUpload(request, env, kind, id);
  }
  const denied = requireMethod(request, ["PUT"], "upload put");
  if (denied !== null) return denied;
  // 旧クライアントのPUT中継。バイト列はもう受けない（ADR 0064）。理由を返して再ビルドを促す
  // The old client's relayed PUT; bytes are no longer accepted (ADR 0064), so answer with the reason
  console.warn(`[upload] rejected a relayed PUT (direct upload required): /${segments.join("/")}`);
  return fail("direct-upload-required", 410);
}
