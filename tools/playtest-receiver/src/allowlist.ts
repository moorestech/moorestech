export const ALLOWLIST_KEY = "config/allowlist.json";

export async function readAllowlist(bucket: R2Bucket): Promise<string[]> {
  const object = await bucket.get(ALLOWLIST_KEY);
  if (object === null) {
    console.warn(`[allowlist] ${ALLOWLIST_KEY} is absent; treating the allowlist as empty`);
    return [];
  }

  const text = await object.text();
  // R2の中身は人手のPUTでも壊れうる境界。壊れていたら全員不許可へ倒し、理由をログへ出す
  // R2 content can be corrupted by a hand-made PUT; on damage fail closed to "nobody allowed" and log why
  try {
    const parsed = JSON.parse(text) as { steamIds?: unknown };
    if (!Array.isArray(parsed.steamIds)) {
      console.warn("[allowlist] steamIds is not an array; treating the allowlist as empty");
      return [];
    }
    return parsed.steamIds.filter((value): value is string => typeof value === "string");
  } catch (error) {
    console.warn(`[allowlist] failed to parse ${ALLOWLIST_KEY}: ${error instanceof Error ? error.message : String(error)}`);
    return [];
  }
}

export async function writeAllowlist(bucket: R2Bucket, steamIds: string[]): Promise<void> {
  const unique = [...new Set(steamIds)];
  await bucket.put(ALLOWLIST_KEY, JSON.stringify({ steamIds: unique }), {
    httpMetadata: { contentType: "application/json" },
  });
}
