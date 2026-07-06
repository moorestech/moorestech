// topic payload の実行時 shape ガード。依存追加なしの手書きチェックで単一チェックポイントに使う
// Runtime shape guards for topic payloads; hand-written (no deps) for use at a single choke point
import { Topics } from "./protocol";

function isObject(v: unknown): v is Record<string, unknown> {
  return typeof v === "object" && v !== null && !Array.isArray(v);
}
function isNumber(v: unknown): v is number {
  return typeof v === "number";
}
function isString(v: unknown): v is string {
  return typeof v === "string";
}
function isBool(v: unknown): v is boolean {
  return typeof v === "boolean";
}
function isArrayOf(v: unknown, each: (x: unknown) => boolean): boolean {
  return Array.isArray(v) && v.every(each);
}
function isSlot(v: unknown): boolean {
  return isObject(v) && isNumber(v.itemId) && isNumber(v.count);
}
function isFluidSlot(v: unknown): boolean {
  return isObject(v) && isNumber(v.fluidId) && isNumber(v.amount) && isNumber(v.capacity) && isString(v.name);
}

function validInventory(d: unknown): boolean {
  return isObject(d) && isArrayOf(d.mainSlots, isSlot) && isArrayOf(d.hotbarSlots, isSlot) && isSlot(d.grab) && isNumber(d.selectedHotbar);
}

// 閉状態は open:false のみ。開状態は全フィールド必須で progress のみ省略可
// Closed is only open:false; open requires every field with progress the sole optional
function validBlockInventory(d: unknown): boolean {
  if (!isObject(d) || !isBool(d.open)) return false;
  if (!d.open) return true;
  return (
    isString(d.blockType) &&
    isString(d.identifier) &&
    isString(d.blockName) &&
    isArrayOf(d.itemSlots, isSlot) &&
    isArrayOf(d.fluidSlots, isFluidSlot) &&
    (d.progress === undefined || isNumber(d.progress))
  );
}

// modal は省略(無し)可。存在するなら全フィールド文字列
// modal may be omitted (none); when present all fields are strings
function validModal(d: unknown): boolean {
  if (!isObject(d)) return false;
  if (d.modal === undefined) return true;
  const m = d.modal;
  return isObject(m) && isString(m.id) && isString(m.title) && isString(m.message) && isString(m.buttonText) && isString(m.variant);
}

function validProgress(d: unknown): boolean {
  return isObject(d) && isBool(d.visible) && isNumber(d.progress) && (d.label === undefined || isString(d.label));
}

// state は列挙名文字列。値の解釈（既知/未知）はルータ側の責務
// state is an enum-name string; interpreting known/unknown values is the router's job
function validUiState(d: unknown): boolean {
  return isObject(d) && isString(d.state);
}

function validCraftRecipes(d: unknown): boolean {
  return isObject(d) && Array.isArray(d.recipes);
}
function validMachineRecipes(d: unknown): boolean {
  return isObject(d) && Array.isArray(d.recipes);
}
function validItemList(d: unknown): boolean {
  return isObject(d) && isArrayOf(d.itemIds, isNumber);
}

const validators: Record<string, (d: unknown) => boolean> = {
  [Topics.inventory]: validInventory,
  [Topics.blockInventory]: validBlockInventory,
  [Topics.modal]: validModal,
  [Topics.progress]: validProgress,
  [Topics.craftRecipes]: validCraftRecipes,
  [Topics.machineRecipes]: validMachineRecipes,
  [Topics.itemList]: validItemList,
  [Topics.uiState]: validUiState,
};

// 既知 topic は検証、未知 topic は購読されず到達しないため素通しする
// Validate known topics; unknown topics are never subscribed so they don't arrive — pass them through
export function validateTopicPayload(topic: string, data: unknown): boolean {
  const validate = validators[topic];
  return validate ? validate(data) : true;
}
