import { readFileSync } from "node:fs";
import type { ServerResponse } from "node:http";
import { WebSocket } from "ws";
import { Topics } from "../../../src/bridge/transport/protocol";
import { localizationLanguagesUrl } from "../../../src/bridge/transport/httpEndpoints";
import { send } from "../wire";
import { state, subscribersOf } from "../state";

// 言語カタログの正本はlocalization_settings.csv（topicControlsのcsv読み口と同じ流儀）
// localization_settings.csv is the single source for the language catalog (same reading style as topicControls)
const languages = readLanguageCatalog();

function readLanguageCatalog(): { code: string; displayName: string }[] {
  const csvUrl = new URL("../../../../../Localization/localization_settings.csv", import.meta.url);
  const lines = readFileSync(csvUrl, "utf8").split("\n").slice(1).filter((line) => line.trim() !== "");
  if (lines.length === 0) throw new Error("localization_settings.csv has no language rows");
  return lines.map((line) => {
    const [code, displayName] = line.split(",");
    if (!code || !displayName) throw new Error(`localization_settings.csv has a malformed row: ${line}`);
    return { code: code.trim(), displayName: displayName.trim() };
  });
}

export function serveLanguageCatalog(url: string, response: ServerResponse): boolean {
  if (url !== localizationLanguagesUrl) return false;
  // 本番DTOと同じcode/displayNameだけをJSONで返す
  // Return only code/displayName in JSON, matching the production DTO
  response.setHeader("content-type", "application/json");
  response.end(JSON.stringify(languages));
  return true;
}

export function applyLocalizationAction(actionType: string, payload: unknown): string | null | undefined {
  if (actionType !== "localization.setLocale") return null;
  // 本番カタログと同じ許可表で外部payloadを検証する
  // Validate the external payload against the same allowlist as the production catalog
  const locale = readLocale(payload);
  if (!languages.some((language) => language.code === locale)) return "unknown_locale";

  // 本番SetLanguageと同じく現在値を更新し、全購読者へtopicを配信する
  // Update the current value and publish the topic to all subscribers, like production SetLanguage
  const current = { locale, revision: 1 };
  state.topicOverrides.set(Topics.localization, current);
  setTimeout(() => {
    for (const subscriber of subscribersOf(Topics.localization)) {
      if (subscriber.readyState === WebSocket.OPEN) {
        send(subscriber, { op: "event", topic: Topics.localization, data: current });
      }
    }
  }, 30);
  return undefined;
}

function readLocale(payload: unknown): string {
  if (typeof payload !== "object" || payload === null || !("locale" in payload)) return "";
  return typeof payload.locale === "string" ? payload.locale : "";
}
