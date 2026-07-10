// topic payload の実行時 shape ガード。依存追加なしの手書きチェックで単一チェックポイントに使う
// Runtime shape guards for topic payloads; hand-written (no deps) for use at a single choke point
import { Topics } from "../transport/protocol";

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

// 各capability詳細のshapeガード。undefined(キー省略)は常に許容する
// Shape guards per capability detail; undefined (omitted key) is always accepted
function validMachineDetail(v: unknown): boolean {
  if (v === undefined) return true;
  if (!isObject(v)) return false;
  const layout = v.slotLayout;
  return (
    isString(v.recipeGuid) && isString(v.currentState) && isNumber(v.currentPower) && isNumber(v.requestPower) &&
    isObject(layout) && isNumber(layout.input) && isNumber(layout.output) && isNumber(layout.module)
  );
}
function validGeneratorDetail(v: unknown): boolean {
  if (v === undefined) return true;
  return isObject(v) && isNumber(v.remainingFuelTime) && isNumber(v.currentFuelTime) && isNumber(v.operatingRate);
}
function validMinerDetail(v: unknown): boolean {
  if (v === undefined) return true;
  return isObject(v) && isNumber(v.currentPower) && isNumber(v.requestPower) &&
    isArrayOf(v.miningItems, (m) => isObject(m) && isNumber(m.itemId) && isNumber(m.itemsPerMinute));
}
function validGearDetail(v: unknown): boolean {
  if (v === undefined) return true;
  return isObject(v) && isBool(v.isClockwise) && isNumber(v.currentRpm) && isNumber(v.currentTorque) && isNumber(v.baseRpm) && isNumber(v.baseTorque);
}
function validElectricNetwork(v: unknown): boolean {
  if (v === undefined) return true;
  return isObject(v) && isNumber(v.totalGeneratePower) && isNumber(v.totalRequiredPower) && isNumber(v.consumerCount) && isNumber(v.powerRate);
}
function validGearNetwork(v: unknown): boolean {
  if (v === undefined) return true;
  return isObject(v) && isNumber(v.totalRequiredGearPower) && isNumber(v.totalGenerateGearPower) && isString(v.stopReason);
}
function validFilterSplitter(v: unknown): boolean {
  if (v === undefined) return true;
  return isObject(v) && isNumber(v.directionCount) && isNumber(v.filterSlotCountPerDirection) &&
    isArrayOf(v.directions, (d) => isObject(d) && isString(d.mode) && isArrayOf(d.filterItemIds, isNumber));
}

// 閉状態は open:false のみ。開状態は基本フィールド必須 + capability詳細は省略可
// Closed is only open:false; open requires base fields, capability details are optional
function validBlockInventory(d: unknown): boolean {
  if (!isObject(d) || !isBool(d.open)) return false;
  if (!d.open) return true;
  return (
    isString(d.blockType) &&
    isString(d.identifier) &&
    isString(d.blockName) &&
    isArrayOf(d.itemSlots, isSlot) &&
    isArrayOf(d.fluidSlots, isFluidSlot) &&
    (d.progress === undefined || isNumber(d.progress)) &&
    validMachineDetail(d.machine) && validGeneratorDetail(d.generator) && validMinerDetail(d.miner) &&
    validGearDetail(d.gear) && validElectricNetwork(d.electricNetwork) && validGearNetwork(d.gearNetwork) &&
    validFilterSplitter(d.filterSplitter)
  );
}

// modal は省略(無し)可。存在するなら全フィールド文字列
// modal may be omitted (none); when present all fields are strings
function validModal(d: unknown): boolean {
  if (!isObject(d)) return false;
  if (d.modal === undefined) return true;
  const m = d.modal;
  return isObject(m) && isString(m.id) && isString(m.title) && isString(m.message) && isString(m.buttonText) && isString(m.variant) &&
    (m.input === undefined || isBool(m.input));
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
function isMachineRecipeItem(v: unknown): boolean {
  return isObject(v) && isNumber(v.itemId) && isNumber(v.count);
}
function isMachineRecipe(v: unknown): boolean {
  return (
    isObject(v) && isString(v.recipeGuid) && isNumber(v.blockId) && isString(v.blockName) &&
    isNumber(v.time) && isArrayOf(v.inputItems, isMachineRecipeItem) && isArrayOf(v.outputItems, isMachineRecipeItem)
  );
}
function validMachineRecipes(d: unknown): boolean {
  return isObject(d) && isArrayOf(d.recipes, isMachineRecipe);
}
function validItemList(d: unknown): boolean {
  return isObject(d) && isArrayOf(d.itemIds, isNumber);
}

// 研究ノードは表示に使う全フィールドを検査する（不正ノード1件で全体破棄）
// Validate every displayed field of research nodes (one bad node drops the whole payload)
function validResearchNode(v: unknown): boolean {
  return isObject(v) && isString(v.guid) && isString(v.name) && isString(v.description) && isString(v.state) &&
    isObject(v.position) && isNumber(v.position.x) && isNumber(v.position.y) &&
    isArrayOf(v.prevGuids, isString) &&
    isArrayOf(v.consumeItems, (c) => isObject(c) && isNumber(c.itemId) && isNumber(c.count)) &&
    isArrayOf(v.rewardItemIds, isNumber) && isArrayOf(v.unlockItemIds, isNumber);
}
function validResearchTree(d: unknown): boolean {
  return isObject(d) && isArrayOf(d.nodes, validResearchNode);
}

function validBuildMenuEntry(v: unknown): boolean {
  return isObject(v) && isString(v.entryType) && isString(v.entryKey) && isString(v.label) && isString(v.tooltip) &&
    (v.iconUrl === undefined || isString(v.iconUrl));
}
function validBuildMenu(d: unknown): boolean {
  return isObject(d) && isArrayOf(d.entries, validBuildMenuEntry);
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
  [Topics.researchTree]: validResearchTree,
  [Topics.buildMenu]: validBuildMenu,
};

// 既知 topic は検証、未知 topic は購読されず到達しないため素通しする
// Validate known topics; unknown topics are never subscribed so they don't arrive — pass them through
export function validateTopicPayload(topic: string, data: unknown): boolean {
  const validate = validators[topic];
  return validate ? validate(data) : true;
}
