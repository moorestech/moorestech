import { isAcked, READY_MARKER } from "../bundleMarkers";
import { MAX_FILE_BYTES, RESERVED_UPLOAD_SEGMENTS } from "../contract";
import type { Env } from "../env";
import { fail, json, requireMethod } from "../http";
import { bundlePrefix, isKind, isSafeSegment, joinSafePath, pendingIndexKey, type PlaytestKind } from "../keys";
import { verifyToken } from "../token";

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
  if (rest.length === 1 && rest[0] === "complete") {
    const denied = requireMethod(request, ["POST"], "upload complete");
    if (denied !== null) return denied;
    return completeUpload(request, env, kind, id);
  }
  const denied = requireMethod(request, ["PUT"], "upload put");
  if (denied !== null) return denied;
  return putUpload(request, env, kind, id, rest);
}

// Bearerトークンだけが置き場を決める。戻り値はsteamIdか、そのまま返す拒否応答
// Only the bearer token decides the destination; returns the steamId or a rejection response to return as-is
async function authorize(request: Request, env: Env): Promise<string | Response> {
  const header = request.headers.get("authorization") ?? "";
  if (!header.startsWith("Bearer ")) {
    console.warn("[upload] rejected: missing or malformed Authorization header");
    return fail("unauthorized", 401);
  }
  const verification = await verifyToken(env.SESSION_HMAC_SECRET, header.slice("Bearer ".length), Math.floor(Date.now() / 1000));
  if (verification.kind === "misconfigured") {
    console.warn("[upload] cannot verify tokens because SESSION_HMAC_SECRET is missing");
    return fail("server-misconfigured", 500);
  }
  if (verification.kind === "rejected") return fail("unauthorized", 401); // verifyToken自身が理由をwarn済み / verifyToken already warned its reason
  if (!isSafeSegment(verification.steamId)) {
    // steamIdはR2キーの構成要素になる。token偽造は出来ないが、キー安全性はここでも保証する
    // steamId becomes part of the R2 key; the token can't be forged, but key safety is still enforced here too
    console.warn(`[upload] rejected a token whose steamId is not a safe key segment: ${verification.steamId}`);
    return fail("unauthorized", 401);
  }
  return verification.steamId;
}

async function putUpload(
  request: Request,
  env: Env,
  kind: PlaytestKind,
  id: string,
  pathSegments: string[],
): Promise<Response> {
  const steamId = await authorize(request, env);
  if (typeof steamId !== "string") return steamId;

  const relativePath = joinSafePath(pathSegments);
  if (relativePath === null) {
    console.warn(`[upload] rejected an unsafe path: ${pathSegments.join("/")}`);
    return fail("bad-path", 400);
  }
  if (RESERVED_UPLOAD_SEGMENTS.has(pathSegments[0] as string)) {
    console.warn(`[upload] rejected a PUT into a reserved name: ${relativePath}`);
    return fail("reserved-name", 400);
  }

  // 解釈できないContent-Lengthは拒否側へ倒す: 欠落は411、数字以外は400、上限超過は413
  // An unreadable Content-Length is rejected outright: missing is 411, non-digits is 400, over the limit is 413
  const label = `${steamId}/${id}/${relativePath}`;
  const rawLength = request.headers.get("content-length");
  if (rawLength === null) {
    console.warn(`[upload] ${label} is missing a Content-Length header`);
    return fail("length-required", 411);
  }
  if (!/^\d+$/.test(rawLength)) {
    console.warn(`[upload] ${label} declares a non-numeric Content-Length: "${rawLength}"`);
    return fail("bad-request", 400);
  }
  const declared = Number(rawLength);
  if (declared > MAX_FILE_BYTES) {
    console.warn(`[upload] ${label} declares ${declared} bytes, over the limit`);
    return fail("too-large", 413);
  }

  // 取り込み済みバンドルへの再送は書かずに成功扱い。ACKED後の上書きで取り込み内容とR2がずれるのを防ぐ
  // A resend into an already acked bundle succeeds without writing, so R2 never drifts from what was ingested
  if (await isAcked(env.BUCKET, kind, steamId, id)) {
    console.warn(`[upload] ignored a PUT into an already acked bundle: ${label}`);
    return json({ stored: relativePath });
  }

  const key = `${bundlePrefix(kind, steamId, id)}/${relativePath}`;
  if (request.body === null) {
    if (declared !== 0) {
      console.warn(`[upload] ${label} declares ${declared} bytes but has no body`);
      return fail("length-mismatch", 400);
    }
    await env.BUCKET.put(key, "");
    return json({ stored: relativePath });
  }

  // 実バイト数を宣言長へ縛る。不一致かどうかは失敗の順序ではなく実際に届いたバイト数で判定する（両側が同時に落ちるため）
  // Actual bytes are bound to the declared length; a mismatch is judged by the bytes that arrived, not failure order, since both sides fail together
  let receivedBytes = 0;
  let bodyEnded = false;
  const counter = new TransformStream<Uint8Array, Uint8Array>({
    transform(chunk, controller) {
      receivedBytes += chunk.byteLength;
      controller.enqueue(chunk);
    },
    flush() {
      bodyEnded = true;
    },
  });
  const fixedLength = new FixedLengthStream(declared);
  const pumpAbort = new AbortController();
  const [pumped, stored] = await Promise.allSettled([
    request.body.pipeThrough(counter).pipeTo(fixedLength.writable, { signal: pumpAbort.signal }),
    env.BUCKET.put(key, fixedLength.readable).catch((error: unknown) => {
      // 保存が落ちたら読み手がいなくなる。送り込みを止めないと待ちが終わらない
      // Once storing fails nobody reads the stream, so the pump is aborted or the wait never ends
      pumpAbort.abort(error);
      throw error;
    }),
  ]);
  if (receivedBytes > declared || (bodyEnded && receivedBytes !== declared)) {
    console.warn(`[upload] ${label} body did not match Content-Length ${declared}: received ${receivedBytes} bytes`);
    return fail("length-mismatch", 400);
  }
  if (pumped.status === "rejected" || stored.status === "rejected") {
    const error: unknown = stored.status === "rejected" ? stored.reason : (pumped as PromiseRejectedResult).reason;
    console.warn(`[upload] ${label} could not be stored in R2: ${describeError(error)}`);
    return fail("storage-unavailable", 503);
  }
  return json({ stored: relativePath });
}

function describeError(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

async function completeUpload(request: Request, env: Env, kind: PlaytestKind, id: string): Promise<Response> {
  const steamId = await authorize(request, env);
  if (typeof steamId !== "string") return steamId;

  // ACKED後のcompleteで索引を作り直すと取り込み済みが未ACKへ戻る。書かずに冪等の成功を返す
  // Re-creating the index after ACKED would resurrect an ingested bundle as pending, so answer an idempotent success without writing
  if (await isAcked(env.BUCKET, kind, steamId, id)) {
    console.warn(`[upload] ignored complete for an already acked bundle: ${steamId}/${id}`);
    return json({ ready: true });
  }

  const summary = await request.text();
  await env.BUCKET.put(`${bundlePrefix(kind, steamId, id)}/${READY_MARKER}`, summary, {
    httpMetadata: { contentType: "application/json" },
  });

  // 未ACKの列挙をR2の全走査にしないため、READYと対の索引オブジェクトを置く。ackで消す
  // A paired index object keeps "pending" enumerable without scanning all of R2; ack deletes it
  await env.BUCKET.put(pendingIndexKey(kind, steamId, id), "");
  return json({ ready: true });
}
