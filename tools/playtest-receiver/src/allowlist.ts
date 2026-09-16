export const ALLOWLIST_KEY = "config/allowlist.json";

// 破損を空リストと区別する。空へ畳むとGET→編集→全置換PUTで許可リストを消し飛ばせてしまう
// Corruption is kept distinct from empty; folding it to [] would let GET -> edit -> full PUT wipe the list
export type AllowlistReadResult = { kind: "ok"; steamIds: string[] } | { kind: "corrupt"; reason: string };

export async function readAllowlist(bucket: R2Bucket): Promise<AllowlistReadResult> {
  const object = await bucket.get(ALLOWLIST_KEY);
  if (object === null) {
    console.warn(`[allowlist] ${ALLOWLIST_KEY} is absent; treating the allowlist as empty`);
    return { kind: "ok", steamIds: [] };
  }

  const text = await object.text();
  // R2の中身は人手のPUTでも壊れうる境界。パース失敗は破損として理由付きで返す
  // R2 content can be corrupted by a hand-made PUT; a parse failure is reported as corruption with its reason
  let parsed: unknown;
  try {
    parsed = JSON.parse(text);
  } catch (error) {
    return corrupt(`parse-failed: ${error instanceof Error ? error.message : String(error)}`);
  }

  // 形の崩れ（配列でない・文字列以外の要素）も黙って捨てず破損として扱う
  // Shape damage (not an array, non-string elements) is also corruption rather than silently filtered
  const steamIds = (parsed as { steamIds?: unknown } | null)?.steamIds;
  if (!Array.isArray(steamIds)) return corrupt("steamIds-not-array");
  if (steamIds.some((value) => typeof value !== "string")) return corrupt("steamIds-has-non-string");
  return { kind: "ok", steamIds: steamIds as string[] };
}

// 実際に保存した重複除去済みの配列を返す。呼び出し側で同じ計算を繰り返させない
// Returns the deduplicated array actually stored, so callers never recompute it
export async function writeAllowlist(bucket: R2Bucket, steamIds: string[]): Promise<string[]> {
  const unique = [...new Set(steamIds)];
  await bucket.put(ALLOWLIST_KEY, JSON.stringify({ steamIds: unique }), {
    httpMetadata: { contentType: "application/json" },
  });
  return unique;
}

function corrupt(reason: string): AllowlistReadResult {
  console.warn(`[allowlist] ${ALLOWLIST_KEY} is corrupt (${reason}); callers must fail closed`);
  return { kind: "corrupt", reason };
}
