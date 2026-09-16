import { STEAM_IDENTITY } from "./contract";
import type { Env } from "./env";

const STEAM_ENDPOINT = "https://partner.steam-api.com/ISteamUserAuth/AuthenticateUserTicket/v1/";

// rejected=Steamがチケットを否認、unverifiable=Steamに聞けなかった。前者は401、後者は再試行可能な503へ分ける
// rejected = Steam denied the ticket, unverifiable = Steam could not be asked; the caller maps them to 401 and a retryable 503
export type SteamTicketResult =
  | { kind: "verified"; steamId: string }
  | { kind: "rejected"; reason: string }
  | { kind: "unverifiable"; reason: string };

interface SteamResponseBody {
  response?: {
    params?: { result?: string; steamid?: string };
    error?: { errorcode?: number; errordesc?: string };
  };
}

export async function authenticateUserTicket(steamFetch: typeof fetch, env: Env, ticketHex: string): Promise<SteamTicketResult> {
  const url = `${STEAM_ENDPOINT}?key=${encodeURIComponent(env.STEAM_WEB_API_KEY)}&appid=${encodeURIComponent(env.STEAM_APP_ID)}&ticket=${encodeURIComponent(ticketHex)}&identity=${encodeURIComponent(STEAM_IDENTITY)}`;

  // Steam Web APIは外部サービス境界。HTTP失敗・到達失敗・本文の非JSONは「検証できなかった」へ畳む
  // The Steam Web API is an external boundary; HTTP failures, unreachability and non-JSON bodies collapse to "unverifiable"
  let body: SteamResponseBody | null;
  try {
    const response = await steamFetch(url, { method: "GET" });
    if (!response.ok) return unverifiable(`http-${response.status}`);
    body = (await response.json()) as SteamResponseBody | null;
  } catch (error) {
    return unverifiable(redactApiKey(error instanceof Error ? error.message : String(error), env.STEAM_WEB_API_KEY));
  }

  // 本文にerrorがある、またはresultがOK以外なら、Steamがチケットを明示的に否認した
  // An error in the body, or a result other than OK, means Steam explicitly denied the ticket
  const error = body?.response?.error;
  if (error !== undefined) return rejected(`steam-error-${error.errorcode ?? "unknown"}`);
  const params = body?.response?.params;
  if (params?.result === undefined) return unverifiable("malformed-response");
  if (params.result !== "OK") return rejected(`result-${params.result}`);

  // OKなのにsteamidが無いのは応答の形の崩れ。否認ではないので検証不能側へ倒す
  // OK without a steamid is a malformed response, not a denial, so it falls to unverifiable
  if (typeof params.steamid !== "string" || params.steamid.length === 0) return unverifiable("missing-steamid");
  return { kind: "verified", steamId: params.steamid };
}

function rejected(reason: string): SteamTicketResult {
  console.warn(`[steam] ticket rejected: ${reason}`);
  return { kind: "rejected", reason };
}

function unverifiable(reason: string): SteamTicketResult {
  console.warn(`[steam] ticket could not be verified: ${reason}`);
  return { kind: "unverifiable", reason };
}

// fetch例外messageはURLを含みうるため、publisher秘密鍵漏洩防止でここだけ置換しwarn/reasonへ渡す
// A fetch exception's message can embed the URL; redact the publisher key here before it reaches warn/reason
function redactApiKey(message: string, apiKey: string): string {
  if (!apiKey) return message;
  return message.split(apiKey).join("***");
}
