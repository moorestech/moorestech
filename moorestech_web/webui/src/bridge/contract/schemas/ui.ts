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
  // 入れ子state名もstateと同じく寛容に受ける。語彙追加でui_stateごと捨てないため
  // The nested state name is accepted as leniently as state, so a new value never discards the whole ui_state payload
  subState: z.string().optional(),
  // ホストは常に配列を載せるので欠損は契約破れ。既定値で吸収すると全画面のヒント消失が無言故障になる
  // The host always sends the array, so a missing field is a contract break; a default would turn a full HUD loss into a silent failure
  keyHints: z.array(KeyHintSchema),
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
// 開始ゲートの待機。precedenceはC#の起動順が正本
// A start gate's waiting; C# owns the precedence order
const StartGateWaitingSchema = z.object({ waiting: z.boolean(), precedence: z.number().int().nonnegative() });
export const EventLanguageGateDataSchema = StartGateWaitingSchema;
// 送信可否の判定はC#が持ち、その結論そのものが kind で届く。独立booleanの袋は有り得ない組合せを表現できてしまう
// C# owns the send-permission decision and its verdict arrives as the kind; a bag of booleans could express impossible combinations
// 欠けた記録は送信可否と独立に起こるため、どの kind でも同じ形で載る
// Missing records happen independently of send permission, so every kind carries them the same way
const BugReportMissingField = { missing: z.array(z.string()) };
const BugReportStatusSchema = z.discriminatedUnion("kind", [
  z.object({ kind: z.literal("noSession"), ...BugReportMissingField }).strict(),
  z.object({ kind: z.literal("capturing"), ...BugReportMissingField }).strict(),
  z.object({ kind: z.literal("submitting"), ...BugReportMissingField }).strict(),
  z.object({ kind: z.literal("ready"), ...BugReportMissingField }).strict(),
  z.object({ kind: z.literal("submitted"), ...BugReportMissingField }).strict(),
]);
export const PauseMenuDataSchema = z.object({ disconnected: z.boolean(), bugReport: BugReportStatusSchema });
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
// tooltipは辞書キーと{p0}補間パラメータの行配列のみを受け取り、生の表示文字列も寸法値も受け付けない
// Tooltips accept only an array of lines (dictionary key + {p0} params) — never raw display text, never sizes
export const TooltipLineSchema = z.object({
  textKey: z.string(),
  textParams: z.array(z.string()),
}).strict();

// 表示状態は行から導出されるものなので、表示なら1行以上・非表示なら0行という対応をスキーマ側で固定する
// Visibility is derived from the lines, so the schema pins the pairing: visible means at least one line, hidden means none
export const TooltipDataSchema = z.discriminatedUnion("visible", [
  z.object({
    visible: z.literal(false),
    lines: z.array(TooltipLineSchema).max(0),
  }).strict(),
  z.object({
    visible: z.literal(true),
    lines: z.array(TooltipLineSchema).min(1),
  }).strict(),
]);

// itemIdはアイテム無し時にキー自体が省略される（NullValueHandling.Ignore）想定だがnullableも許容する
// itemId is normally omitted (not sent as null) when there is no item, but nullable is accepted too
const MessageNotificationSchema = z.object({
  seq: z.number(),
  category: z.enum(["achievement", "operationDenied", "saveMigration"]),
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
