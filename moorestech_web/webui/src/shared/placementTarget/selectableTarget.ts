import { L, blockNameKey, connectToolNameKey, trainCarNameKey, type TranslationKey } from "../i18n";

// 配置対象の種別
// Selectable target kinds; raw covers only user-authored names without a dictionary key
export type SelectableTarget =
  | { type: "block"; guid: string }
  | { type: "connectTool"; guid: string }
  | { type: "trainCar"; guid: string }
  | { type: "blueprintCopy" }
  | { type: "raw"; label: string };

// 表示名解決に必要な最小フィールド
// The minimal fields display-name resolution needs; both build_menu.entries and resolved local_player.hotbar slots satisfy it
export type NamedPlacementTarget =
  | { kind: "block" | "connectTool" | "trainCar"; id: string }
  | { kind: "blueprintCopy" }
  | { kind: "blueprint"; label: string };

// 配置対象の表示名解決はこの1本に集約する（BuildMenu/配置HUD/ホットバー共用・分岐の複製禁止）
// All selectable-target display names resolve here, shared by BuildMenu, the placement HUD, and the hotbar
export function localizeSelectableTargetName(
  target: SelectableTarget,
  translate: (key: TranslationKey) => string,
): string {
  switch (target.type) {
    case "block": return translate(blockNameKey(target.guid));
    case "connectTool": return translate(connectToolNameKey(target.guid));
    case "trainCar": return translate(trainCarNameKey(target.guid));
    case "blueprintCopy": return translate(L.ui.buildMenu.blueprintCopy);
    case "raw": return target.label;
  }
}

// 配信kindを表示種別へ写す
// Map the wire kind onto the resolution type; only saved blueprints keep their raw label
export function placementTargetOf(entry: NamedPlacementTarget): SelectableTarget {
  switch (entry.kind) {
    case "block": return { type: "block", guid: entry.id };
    case "connectTool": return { type: "connectTool", guid: entry.id };
    case "trainCar": return { type: "trainCar", guid: entry.id };
    case "blueprintCopy": return { type: "blueprintCopy" };
    default: return { type: "raw", label: entry.label };
  }
}
