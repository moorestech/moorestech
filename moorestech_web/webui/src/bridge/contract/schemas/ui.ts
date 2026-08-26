import { z } from "zod";

export const ModalRequestSchema = z.object({
  id: z.string(),
  title: z.string(),
  message: z.string(),
  buttonText: z.string(),
  variant: z.enum(["confirm", "error"]),
  input: z.boolean().optional(),
});
export const ModalDataSchema = z.object({ modal: ModalRequestSchema.optional() });
export const ProgressDataSchema = z.object({
  visible: z.boolean(), progress: z.number(), label: z.string().optional(),
});

// キー名も文言もローカライズキーで届く。内容の正はC#のUIState（ADR-0032）
// Both the key name and the text arrive as localization keys; C#'s UIState owns the content (ADR-0032)
const KeyHintSchema = z.object({ keyNameKey: z.string(), textKey: z.string() });

// 未知のstate名は画面ルータが安全側へ処理するため文字列全体を受理する
// Accept every state name because the screen router handles unknown names safely
export const UiStateDataSchema = z.object({
  state: z.string(),
  // 入れ子stateの語彙: 列車HUD(GameScreen/PauseMenuScreen)とスキット(Playing/PauseMenu)
  // Nested-state vocabulary: train HUD (GameScreen/PauseMenuScreen) and skit (Playing/PauseMenu)
  subState: z.enum(["GameScreen", "PauseMenuScreen", "Playing", "PauseMenu"]).optional(),
  keyHints: z.array(KeyHintSchema).default([]),
});
export const TrainRidingDataSchema = z.object({
  riding: z.boolean(),
  branchCandidateCount: z.number().int().nonnegative(),
  selectedBranchIndex: z.number().int().nonnegative(),
});
export const LocalizationDataSchema = z.object({
  locale: z.string().min(1),
  revision: z.number().int().nonnegative(),
});
export const PauseMenuDataSchema = z.object({ disconnected: z.boolean() });
const PlacementModeCommonFields = {
  height: z.number().int(),
  unavailableReason: z.string(),
  // ホイールを消費中かはC#が判定して配る。Web側は種別から再導出しない
  // C# decides whether the wheel is consumed and publishes it; the Web must not re-derive it from the target kind
  wheelOwnedByTool: z.boolean(),
};
export const PlacementModeDataSchema = z.discriminatedUnion("selectedTargetType", [
  z.object({
    selectedTargetType: z.literal("block"),
    selectedBlockGuid: z.string().uuid(),
    ...PlacementModeCommonFields,
  }).strict(),
  z.object({
    selectedTargetType: z.literal("connectTool"),
    selectedConnectToolGuid: z.string().uuid(),
    ...PlacementModeCommonFields,
  }).strict(),
  z.object({
    selectedTargetType: z.literal("trainCar"),
    selectedTrainCarGuid: z.string().uuid(),
    ...PlacementModeCommonFields,
  }).strict(),
  z.object({
    selectedTargetType: z.literal("blueprintCopy"),
    ...PlacementModeCommonFields,
  }).strict(),
  // rawは辞書キーを持たないユーザー命名BPのみ
  // raw covers only user-authored blueprints without dictionary keys
  z.object({
    selectedTargetType: z.literal("raw"),
    selectedName: z.string(),
    ...PlacementModeCommonFields,
  }).strict(),
]);
export const CrosshairDataSchema = z.object({ visible: z.boolean() });
export const UiVisibilityDataSchema = z.object({ visible: z.boolean() });
// tooltipは辞書キーと{p0}補間パラメータのみを受け取り、生の表示文字列も寸法値も受け付けない
// Tooltips accept only a dictionary key and {p0} interpolation params — never raw display text, never sizes
export const TooltipDataSchema = z.object({
  visible: z.boolean(),
  textKey: z.string(),
  textParams: z.array(z.string()),
}).strict();

// itemIdはアイテム無し時にキー自体が省略される（NullValueHandling.Ignore）想定だがnullableも許容する
// itemId is normally omitted (not sent as null) when there is no item, but nullable is accepted too
const MessageNotificationSchema = z.object({
  seq: z.number(),
  category: z.enum(["achievement", "operationDenied"]),
  messageId: z.string(),
  messageParams: z.array(z.string()),
  // シリアライザ揺れでnullが来ても弾かないよう外部境界として広めに受ける
  // Widened as an external boundary so serializer drift sending null is not rejected
  itemId: z.number().nullable().optional(),
}).strict();

// アイコンと個数の欠損は境界で弾く
// Missing icon or amount is rejected at the boundary
const ItemEarnedNotificationSchema = z.object({
  seq: z.number(),
  category: z.literal("itemEarned"),
  messageId: z.string(),
  messageParams: z.array(z.string()),
  itemId: z.number(),
  count: z.number().int().positive(),
});

// 接続直後の{}を専用variantで受理
// The {} arriving right after connect is accepted by a dedicated variant
const EmptyNotificationSchema = z.object({}).strict();

export const NotificationDataSchema = z.union([
  EmptyNotificationSchema,
  ItemEarnedNotificationSchema,
  MessageNotificationSchema,
]);
