import type { Env } from "./env";

export const STEAM_IDENTITY = "moorestech-playtest";
const STEAM_ENDPOINT = "https://partner.steam-api.com/ISteamUserAuth/AuthenticateUserTicket/v1/";

export interface SteamTicketResult {
  steamId: string | null;
  reason: string;
}

interface SteamResponseBody {
  response?: {
    params?: { result?: string; steamid?: string };
    error?: { errorcode?: number; errordesc?: string };
  };
}

// Steam Web APIは外部サービス境界。到達失敗も応答の形の崩れも「検証できなかった」へ畳む
// The Steam Web API is an external boundary; unreachability and malformed bodies both collapse to "not verified"
export async function authenticateUserTicket(steamFetch: typeof fetch, env: Env, ticketHex: string): Promise<SteamTicketResult> {
  const url = `${STEAM_ENDPOINT}?key=${encodeURIComponent(env.STEAM_WEB_API_KEY)}&appid=${encodeURIComponent(env.STEAM_APP_ID)}&ticket=${encodeURIComponent(ticketHex)}&identity=${encodeURIComponent(STEAM_IDENTITY)}`;

  let body: SteamResponseBody;
  try {
    const response = await steamFetch(url, { method: "GET" });
    if (!response.ok) {
      console.warn(`[steam] AuthenticateUserTicket returned HTTP ${response.status}`);
      return { steamId: null, reason: `http-${response.status}` };
    }
    body = (await response.json()) as SteamResponseBody;
  } catch (error) {
    const message = redactApiKey(error instanceof Error ? error.message : String(error), env.STEAM_WEB_API_KEY);
    console.warn(`[steam] AuthenticateUserTicket failed: ${message}`);
    return { steamId: null, reason: message };
  }

  const error = body.response?.error;
  if (error !== undefined) {
    console.warn(`[steam] ticket rejected: ${error.errorcode} ${error.errordesc}`);
    return { steamId: null, reason: `steam-error-${error.errorcode ?? "unknown"}` };
  }

  const params = body.response?.params;
  if (params?.result !== "OK" || typeof params.steamid !== "string" || params.steamid.length === 0) {
    console.warn(`[steam] unexpected result: ${params?.result ?? "missing"}`);
    return { steamId: null, reason: `result-${params?.result ?? "missing"}` };
  }
  return { steamId: params.steamid, reason: "ok" };
}

// fetch例外messageはURLを含みうるため、publisher秘密鍵漏洩防止でここだけ置換しwarn/reasonへ渡す
// A fetch exception's message can embed the URL; redact the publisher key here before it reaches warn/reason
function redactApiKey(message: string, apiKey: string): string {
  if (!apiKey) return message;
  return message.split(apiKey).join("***");
}
