// サーバーの通知生成APIへ渡るidを全て拾い、Web表で解決するか検査する。表の追加漏れは「不明な通知」として見えワイヤ形テストでは捕まらない
// 補間idはenum定義を展開して各値を検査し、送られない値だけを明示除外する。分類できない形はテストを落とし無音で対象外にしない
// itemEarnedはcategoryでキーが決まるため表の対象外
// Collects every id passed to the server's notification factories and checks it resolves in the web table; a missing row surfaces as "Unknown notification", which wire-shape tests cannot catch
// Interpolated ids expand the enum definition and check each value, excluding only values never sent; an unclassifiable form fails instead of being silently skipped
// itemEarned takes its key from the category, so it is outside the table
import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { L } from "@/shared/i18n";
import { resolveNotificationKey } from "./notificationMessages";

const serverScriptsDir = fileURLToPath(new URL("../../../../../moorestech_server/Assets/Scripts/", import.meta.url));
const factoryCallFirstArgument = /NotificationMessagePack\.Create(?:Achievement|AchievementWithItem|OperationDenied)\(\s*(\$?"[^"]*")/g;
const factoryInternalLiteral = /"((?:achievement|denied|saveMigration)\.[^"]+)"/g;
const plainLiteral = /^"([^"{}]+)"$/;
const fixedMemberInterpolation = /^\$"([^"{}]+)\{(\w+)\.(\w+)\}"$/;
const reasonInterpolation = /^\$"([^"{}]+)\{[^"{}]+\}"$/;

// 補間idの接頭辞→展開するenumと、その経路では送られない値
// Interpolated id prefix -> the enum to expand and the values that path never sends
const interpolatedIdEnums = new Map<string, { enumName: string; notSentMembers: string[] }>([
  ["denied.railEdit.", { enumName: "RailConnectionEditFailureReason", notSentMembers: ["None"] }],
  ["denied.electricWireExtend.", { enumName: "ElectricWirePlacementFailureReason", notSentMembers: ["InventoryFull", "NotConnected"] }],
  [
    "denied.electricWireDisconnect.",
    {
      enumName: "ElectricWirePlacementFailureReason",
      notSentMembers: ["None", "OutOfRange", "AlreadyConnected", "ConnectionLimit", "NoWireItem", "NoPoleItem", "PositionOccupied", "InvalidMode", "NotUnlocked", "InsufficientItems"],
    },
  ],
]);

function readServerSources(): Map<string, string> {
  const sources = new Map<string, string>();
  const files = readdirSync(serverScriptsDir, { recursive: true, encoding: "utf8" })
    .filter((path) => path.endsWith(".cs") && !path.split(/[\\/]/).some((segment) => segment.startsWith("Tests")));
  for (const file of files) sources.set(file, readFileSync(join(serverScriptsDir, file), "utf8"));
  return sources;
}

function findEnumMembers(sources: Map<string, string>, enumName: string): string[] | undefined {
  const enumBody = new RegExp(`enum\\s+${enumName}\\s*\\{([^}]*)\\}`);
  for (const source of sources.values()) {
    const body = source.match(enumBody)?.[1];
    if (body) return body.split(",").map((member) => member.split("=")[0].trim()).filter((member) => member.length > 0);
  }
  return undefined;
}

function readEnumMembers(sources: Map<string, string>, enumName: string): string[] {
  const members = findEnumMembers(sources, enumName);
  if (!members) throw new Error(`enum ${enumName} not found in server sources`);
  return members;
}

// 生成APIの第1引数を分類してidへ展開する。分類できない引数は unclassified へ積む
// Classify each factory first argument and expand it to ids; unclassifiable arguments go to unclassified
function collectServerNotificationIds(sources: Map<string, string>) {
  const ids = new Set<string>();
  const unclassified: string[] = [];
  for (const [file, source] of sources) {
    for (const [, argument] of source.matchAll(factoryCallFirstArgument)) {
      const literal = argument.match(plainLiteral);
      const fixedMember = argument.match(fixedMemberInterpolation);
      const reason = argument.match(reasonInterpolation);
      // 登録済み接頭辞の補間は変数経由の理由値としてenum展開し、未登録で{Enum.Member}形なら固定値として扱う
      // A registered prefix is a variable reason expanded over its enum; otherwise a {Enum.Member} form is one fixed value
      if (literal) ids.add(literal[1]);
      else if (reason && interpolatedIdEnums.has(reason[1])) {
        const { enumName, notSentMembers } = interpolatedIdEnums.get(reason[1])!;
        for (const member of readEnumMembers(sources, enumName)) if (!notSentMembers.includes(member)) ids.add(reason[1] + member);
      } else if (fixedMember && findEnumMembers(sources, fixedMember[2])?.includes(fixedMember[3])) ids.add(fixedMember[1] + fixedMember[3]);
      else unclassified.push(`${file}: ${argument}`);
    }
    // ファクトリ内に直書きされたid（セーブ移行通知）も送信idに含める
    // Ids hard-coded inside the factory class (the save-migration notice) are sent ids too
    if (file.endsWith("NotificationMessagePack.cs")) {
      for (const [, id] of source.matchAll(factoryInternalLiteral)) ids.add(id);
    }
  }
  return { ids: [...ids].sort(), unclassified };
}

describe("サーバー通知idの表網羅", () => {
  const sources = readServerSources();
  const { ids, unclassified } = collectServerNotificationIds(sources);

  it("走査が送信元の各形を拾えている", () => {
    // 走査が壊れて0件になると網羅検査が空振りで緑になるため、各形の既知idで走査自体を確かめる
    // A broken scan would yield no ids and pass vacuously, so known ids of each form prove the scan works
    expect(ids).toContain("denied.miningInventoryFull");
    expect(ids).toContain("achievement.unlockedItem");
    expect(ids).toContain("saveMigration.missingMasterPruned");
    expect(ids).toContain("denied.blueprint.NotUnlocked");
    expect(ids).toContain("denied.railEdit.InvalidNode");
    expect(ids).toContain("denied.electricWireDisconnect.InventoryFull");
  });

  it("通知生成APIの引数は全て分類できる形である", () => {
    expect(unclassified).toEqual([]);
  });

  it("除外指定したenum値は実在する", () => {
    // enum値の改名で除外指定が空振りし、送られる値を黙って対象外にしないようにする
    // Guard against a renamed enum value leaving a stale exclusion that silently skips a sent value
    for (const { enumName, notSentMembers } of interpolatedIdEnums.values()) {
      const members = readEnumMembers(sources, enumName);
      for (const member of notSentMembers) expect(members).toContain(member);
    }
  });

  it("サーバーが送る通知idは全て不明通知以外のキーへ解決する", () => {
    const unresolved = ids.filter((id) => resolveNotificationKey(id) === L.ui.notification.unknownMessage);
    expect(unresolved).toEqual([]);
  });

  it("採掘で満杯により失った通知は専用文言を使う", () => {
    expect(resolveNotificationKey("denied.miningInventoryFull")).toBe(L.ui.notification.miningInventoryFull);
  });
});
