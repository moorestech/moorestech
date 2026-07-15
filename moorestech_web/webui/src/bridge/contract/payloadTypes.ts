// サーバー由来の DTO 型（topic snapshot / event の data 部）
// Server-originated DTO types (the data part of topic snapshot/event)
// 現状は手書き。将来 C# からの自動生成に置換する余地を残す
// Handwritten for now; leaves room to be replaced by C#-generated types later

export type SlotData = { itemId: number; count: number };
export type InventoryArea = "main" | "hotbar" | "grab";
export type SlotRef = { area: InventoryArea; slot: number };

// ブロックインベントリ移動はプレイヤー側(main/hotbar/grab)とブロック側(block)を跨ぐ
// Block-inventory moves span the player side (main/hotbar/grab) and the block side (block)
export type BlockInventoryArea = InventoryArea | "block";
export type BlockSlotRef = { area: BlockInventoryArea; slot: number };

export type PlayerInventoryData = {
  mainSlots: SlotData[];
  hotbarSlots: SlotData[];
  grab: SlotData;
  // 選択中のホットバー index (0-8)。uGUI HotBarView.SelectIndex 相当
  // Currently selected hotbar index (0-8); mirrors uGUI HotBarView.SelectIndex
  selectedHotbar: number;
};

// COM-2 モーダル要求。uGUI OneButtonModal(title/message/1ボタン) 相当
// COM-2 modal request; mirrors uGUI OneButtonModal (title/message/single button)
export type ModalRequest = {
  id: string;
  title: string;
  message: string;
  buttonText: string;
  variant: "confirm" | "error";
  // 入力必須モーダル（BP名入力等）。false時はC#側でキー省略されるため optional
  // Input-required modal (e.g. blueprint naming); omitted when false, so it is optional
  input?: boolean;
};
// modal は C# 側 NullValueHandling.Ignore で null 時にキーごと省略される（無し時は {}）
// modal is dropped key-and-all when null by the C# NullValueHandling.Ignore (absent → {})
export type ModalData = { modal?: ModalRequest };

// COM-3 汎用プログレス。uGUI ProgressBarView(Show/Hide + 0..1) 相当
// COM-3 generic progress; mirrors uGUI ProgressBarView (Show/Hide + 0..1)
// label は null 時にキー省略されるため optional（型で欠落を表現する）
// label is key-omitted when null, so it is optional (the type expresses the omission)
export type ProgressData = { visible: boolean; progress: number; label?: string };

// INFRA-6 最小版: C# UIStateEnum の現在値。未知のstate名も受理し画面ルータが安全側に倒す
// Minimal INFRA-6: current C# UIStateEnum value; unknown names are accepted and the router fails safe
export type UiStateData = { state: string };

// INV-6 液体スロット。uGUI FluidSlotView(アイコン + amount/capacity + 名前 tooltip) 相当
// INV-6 fluid slot; mirrors uGUI FluidSlotView (icon + amount/capacity + name tooltip)
export type FluidSlotData = { fluidId: number; amount: number; capacity: number; name: string };

// INV-4/BLK-1 ブロックインベントリ。閉状態は open:false のみ、他キーは C# 側で全省略される
// INV-4/BLK-1 block inventory; the closed state is only open:false, the C# side omits every other key
// open を判別子にした discriminated union で「開なら全フィールド存在」を型が保証する（!data.open ガードを正当化）
// A discriminated union on open lets the type guarantee "all fields present when open" (justifies the !data.open guard)
// BLK-2〜5/8 ブロック詳細。capability合成（機能単位optional）でブロック種別unionにしない(spec D1)
// BLK-2..5/8 block details; capability composition (per-feature optionals), never a per-blockType union (spec D1)
export type MachineDetailData = {
  recipeGuid: string;
  currentState: string;
  currentPower: number;
  requestPower: number;
  // itemSlots を 入力→出力→モジュール に分割する位置（uGUIのスロット構成順）
  // Split positions of itemSlots into input→output→module (uGUI slot ordering)
  slotLayout: { input: number; output: number; module: number };
};
export type GeneratorDetailData = { remainingFuelTime: number; currentFuelTime: number; operatingRate: number };
export type MinerDetailData = {
  currentPower: number;
  requestPower: number;
  miningItems: { itemId: number; itemsPerMinute: number }[];
};
export type GearDetailData = { isClockwise: boolean; currentRpm: number; currentTorque: number; baseRpm: number; baseTorque: number };
export type ElectricNetworkData = { totalGeneratePower: number; totalRequiredPower: number; consumerCount: number; powerRate: number };
export type GearNetworkStopReason = "none" | "rocked" | "overRequirePower";
export type GearNetworkData = { totalRequiredGearPower: number; totalGenerateGearPower: number; stopReason: GearNetworkStopReason };
export type FilterSplitterMode = "default" | "whitelist" | "blacklist";
export type FilterSplitterDirectionData = { mode: FilterSplitterMode; filterItemIds: number[] };
export type FilterSplitterData = { directionCount: number; filterSlotCountPerDirection: number; directions: FilterSplitterDirectionData[] };

export type BlockInventoryOpen = {
  open: true;
  blockType: string;
  identifier: string;
  blockName: string;
  itemSlots: SlotData[];
  fluidSlots: FluidSlotData[];
  // progress は null 時にキー省略されるため optional
  // progress is key-omitted when null, so it is optional
  progress?: number;
  // 詳細は該当ブロックのみ付与（C# NullValueHandling.Ignore で非該当キーは省略）
  // Details are attached only for applicable blocks (absent keys omitted via C# NullValueHandling.Ignore)
  machine?: MachineDetailData;
  generator?: GeneratorDetailData;
  miner?: MinerDetailData;
  gear?: GearDetailData;
  electricNetwork?: ElectricNetworkData;
  gearNetwork?: GearNetworkData;
  filterSplitter?: FilterSplitterData;
};
export type BlockInventoryClosed = { open: false };
export type BlockInventoryData = BlockInventoryOpen | BlockInventoryClosed;

export type RequiredItem = { itemId: number; count: number };
export type CraftRecipe = {
  recipeGuid: string;
  resultItemId: number;
  resultCount: number;
  craftTime: number;
  requiredItems: RequiredItem[];
};
export type CraftRecipesData = { recipes: CraftRecipe[] };

export type MachineRecipeItem = { itemId: number; count: number };
export type MachineRecipe = {
  recipeGuid: string;
  blockId: number;
  blockName: string;
  time: number;
  inputItems: MachineRecipeItem[];
  outputItems: MachineRecipeItem[];
};
export type MachineRecipesData = { recipes: MachineRecipe[] };

export type RecipeViewerItemListData = { itemIds: number[] };

export type ItemMasterEntry = { itemId: number; name: string; maxStack: number };
export type ItemMasterData = { items: ItemMasterEntry[] };

// FEAT-RES-1 研究ツリー。表示可否は ui_state.current(ResearchTree) から導出し、本topicはノードデータのみ運ぶ
// FEAT-RES-1 research tree; visibility derives from ui_state.current (ResearchTree), this topic carries node data only
export type ResearchNodeState =
  | "completed" | "researchable"
  | "unresearchableNotEnoughItem" | "unresearchableNotEnoughPreNode" | "unresearchableAllReasons";
export type ResearchNodeData = {
  guid: string;
  name: string;
  description: string;
  state: ResearchNodeState;
  // マスタ GraphViewSettings.UIPosition。uGUI anchoredPosition と同値
  // Master GraphViewSettings.UIPosition; same value as the uGUI anchoredPosition
  position: { x: number; y: number };
  prevGuids: string[];
  consumeItems: { itemId: number; count: number }[];
  rewardItemIds: number[];
  unlockItemIds: number[];
};
export type ResearchTreeData = { nodes: ResearchNodeData[] };

// BM-1 ビルドメニューエントリ。uGUI BuildMenuEntryCatalog の合成結果をそのまま運ぶ
// BM-1 build-menu entries; carries the composed result of the uGUI BuildMenuEntryCatalog
export type BuildMenuEntryType = "block" | "trainCar" | "connectTool" | "blueprintCopy" | "blueprint";
export type BuildMenuEntryData = {
  entryType: BuildMenuEntryType;
  // 種別ごとの安定キー: block=BlockId / trainCar=Guid / connectTool=ToolType / blueprint=BP名 / blueprintCopy=""
  // Stable key per type: block=BlockId, trainCar=Guid, connectTool=ToolType, blueprint=BP name, blueprintCopy=""
  entryKey: string;
  label: string;
  tooltip: string;
  // アイコン無し（BP・BPコピー）はキー省略されるため optional
  // Icon-less entries (blueprints, copy tool) omit the key, so it is optional
  iconUrl?: string;
};
export type BuildMenuData = { entries: BuildMenuEntryData[] };
