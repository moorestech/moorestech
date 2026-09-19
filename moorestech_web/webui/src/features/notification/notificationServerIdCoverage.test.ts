// サーバーC#が文字列リテラルで送る通知idが、Web側の対応表で全て解決されることを検査する
// Checks that every notification id the server C# sends as a string literal resolves in the web table
// 表への追加漏れはプレイヤーに「不明な通知」として見えるため、ワイヤ形のテストでは捕まらない配線を突き合わせる
// A missing row surfaces as "Unknown notification" to players, which wire-shape tests cannot catch
// enum名を連結して作るid（denied.railEdit.* 等）は末尾が「.」のリテラルになるため対象外
// Ids built by concatenating enum names (denied.railEdit.* etc.) appear as literals ending in "." and are skipped
import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { L } from "@/shared/i18n";
import { resolveNotificationKey } from "./notificationMessages";

const serverScriptsDir = fileURLToPath(new URL("../../../../../moorestech_server/Assets/Scripts/", import.meta.url));
const notificationIdLiteral = /"((?:achievement|denied|saveMigration)\.[A-Za-z.]*[A-Za-z])"/g;

function collectServerNotificationIds(): string[] {
  const ids = new Set<string>();
  const files = readdirSync(serverScriptsDir, { recursive: true, encoding: "utf8" })
    .filter((path) => path.endsWith(".cs") && !path.includes("Tests"));
  for (const file of files) {
    const source = readFileSync(join(serverScriptsDir, file), "utf8");
    for (const match of source.matchAll(notificationIdLiteral)) ids.add(match[1]);
  }
  return [...ids].sort();
}

describe("サーバー通知idの表網羅", () => {
  const serverIds = collectServerNotificationIds();

  it("走査がサーバーの通知idを拾えている", () => {
    // 走査パスが壊れて0件になると網羅検査が空振りで緑になるため、既知idの存在で走査自体を確かめる
    // A broken scan path would yield zero ids and a vacuous pass, so a known id proves the scan works
    expect(serverIds).toContain("denied.craftMaterialShortage");
    expect(serverIds).toContain("denied.miningInventoryFull");
  });

  it("サーバーが送る通知idは全て不明通知以外のキーへ解決する", () => {
    const unresolved = serverIds.filter((id) => resolveNotificationKey(id) === L.ui.notification.unknownMessage);
    expect(unresolved).toEqual([]);
  });

  it("採掘で満杯により失った通知は専用文言を使う", () => {
    expect(resolveNotificationKey("denied.miningInventoryFull")).toBe(L.ui.notification.miningInventoryFull);
  });
});
