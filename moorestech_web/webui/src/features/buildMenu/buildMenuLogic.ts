import type { BuildMenuEntryData } from "@/bridge/contract/payloadTypes";
import type { ActionPayloads } from "@/bridge";

// 選択アクションの payload を組み立てる純関数
// Pure builder for the select-action payload
export function selectPayload(entry: BuildMenuEntryData): ActionPayloads["build_menu.select"] {
  return { entryType: entry.entryType, entryKey: entry.entryKey };
}

// BP削除アクションの payload を組み立てる純関数
// Pure builder for the blueprint-delete payload
export function deletePayload(name: string): ActionPayloads["blueprint.delete"] {
  return { name };
}
