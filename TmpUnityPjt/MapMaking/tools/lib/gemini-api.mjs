import { randomBytes } from "node:crypto";

const BASE_URL = "https://generativelanguage.googleapis.com/v1beta";
const MODEL = "gemini-3.1-pro-preview";
const DEFAULT_TTL = "3600s";

// 共通エラーハンドリング
async function assertOk(res, context) {
  if (!res.ok) {
    const errText = await res.text();
    throw new Error(`${context} (${res.status}): ${errText}`);
  }
}

// 5文字の短縮セッションIDを生成
function generateShortId() {
  return randomBytes(3).toString("base64url").slice(0, 5);
}

// キャッシュ作成 — displayName に短縮IDを設定
export async function createCache(apiKey, systemPrompt, ttl = DEFAULT_TTL) {
  const url = `${BASE_URL}/cachedContents?key=${apiKey}`;
  const shortId = generateShortId();

  const body = {
    model: `models/${MODEL}`,
    displayName: shortId,
    systemInstruction: {
      parts: [{ text: systemPrompt }],
    },
    ttl,
  };

  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  await assertOk(res, "キャッシュ作成失敗");

  const cache = await res.json();
  cache.shortId = shortId;
  return cache;
}

// 短縮IDからキャッシュのフルネームを解決（List APIでページネーション付き逆引き）
export async function resolveShortId(apiKey, shortId) {
  if (shortId.startsWith("cachedContents/")) return shortId;

  let pageToken = "";
  do {
    const params = new URLSearchParams({ key: apiKey, pageSize: "100" });
    if (pageToken) params.set("pageToken", pageToken);

    const res = await fetch(`${BASE_URL}/cachedContents?${params}`);
    await assertOk(res, "キャッシュ一覧取得失敗");

    const data = await res.json();
    const match = data.cachedContents?.find((c) => c.displayName === shortId);
    if (match) return match.name;

    pageToken = data.nextPageToken ?? "";
  } while (pageToken);

  throw new Error(`セッション "${shortId}" が見つかりません（期限切れの可能性があります）`);
}

// キャッシュ情報取得
export async function getCache(apiKey, cacheId) {
  const res = await fetch(`${BASE_URL}/${cacheId}?key=${apiKey}`);
  await assertOk(res, "キャッシュ取得失敗");
  return res.json();
}

// TTL 延長 — セッション利用時に自動で呼ぶ
export async function renewCacheTTL(apiKey, cacheId, ttl = DEFAULT_TTL) {
  const res = await fetch(`${BASE_URL}/${cacheId}?key=${apiKey}`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ ttl }),
  });
  await assertOk(res, "TTL延長失敗");
  return res.json();
}

// キャッシュ参照で generateContent を呼び出し
export async function generateWithCache(apiKey, cacheId, userParts) {
  const res = await fetch(
    `${BASE_URL}/models/${MODEL}:generateContent?key=${apiKey}`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        cachedContent: cacheId,
        contents: [{ role: "user", parts: userParts }],
      }),
    }
  );
  await assertOk(res, "Gemini API エラー");
  return res.json();
}

// レスポンスからテキストを抽出
export function extractText(response) {
  return (
    response.candidates?.[0]?.content?.parts
      ?.map((p) => p.text)
      .filter(Boolean)
      .join("\n") || null
  );
}

// usage_metadata からキャッシュ情報を抽出
export function extractUsage(response) {
  const meta = response.usageMetadata;
  if (!meta) return null;
  return {
    cachedTokens: meta.cachedContentTokenCount ?? 0,
    promptTokens: meta.promptTokenCount ?? 0,
    responseTokens: meta.candidatesTokenCount ?? 0,
    totalTokens: meta.totalTokenCount ?? 0,
  };
}
