import { z } from "zod";

export const GameStateDataSchema = z.object({ state: z.enum(["InGame", "Skit", "CutScene"]) });
// 枠線+任意labelTutorialGuid(t()解決)
// Outline plus optional labelTutorialGuid, resolved via t()
export const TutorialHighlightSchema = z.object({
  kind: z.literal("outline"), elementId: z.string(), anchorId: z.string(),
  paddingPx: z.number().nonnegative(), blocksPointerInput: z.boolean(),
  labelTutorialGuid: z.string().uuid().optional(),
}).strict();
// D&D説明の矢印ガイド。from/to両anchorが解決している間だけ描く
// Drag guide arrows for D&D instruction; drawn only while both anchors resolve
export const TutorialDragGuideSchema = z.object({
  kind: z.literal("dragGuide"), elementId: z.string(),
  fromAnchorId: z.string(), toAnchorId: z.string(),
}).strict();
// キー操作ヒント。uiState一致中のみHUD表示
// Key-control hint; shown only while uiState matches
export const TutorialKeyControlSchema = z.object({
  kind: z.literal("keyControl"), elementId: z.string(),
  tutorialGuid: z.string().uuid(), keyName: z.string(), uiState: z.string(),
}).strict();
// overlay要素はkind判別unionの単一列。種別追加は配列を増やさずunionへ足す
// Overlay elements form one kind-discriminated union list; new kinds extend the union, not the arrays
export const TutorialOverlayElementSchema = z.discriminatedUnion("kind", [
  TutorialHighlightSchema, TutorialDragGuideSchema, TutorialKeyControlSchema,
]);
// sessionはchallenge単位。同時currentの複数challengeが並存できる
// One session per challenge, so simultaneously current challenges coexist
export const TutorialSessionSchema = z.object({
  tutorialSessionId: z.string(), challengeId: z.string(),
  elements: z.array(TutorialOverlayElementSchema),
}).strict();
export const TutorialPresentationDataSchema = z.object({
  revision: z.number().int().nonnegative(), sessions: z.array(TutorialSessionSchema),
});
// ワールドピン: Unity射影の正規化座標と画面外矢印用の方向ベクトル。文言はGuid導出キーでWeb解決する
// World pins: Unity-projected normalized coords plus an off-screen arrow vector; text resolves web-side from the GUID
export const WorldPinSchema = z.object({
  pinId: z.string(), tutorialGuid: z.string().uuid(),
  screenX: z.number(), screenY: z.number(), onScreen: z.boolean(),
  directionX: z.number(), directionY: z.number(),
}).strict();
export const WorldPinPresentationDataSchema = z.object({
  revision: z.number().int().nonnegative(), pins: z.array(WorldPinSchema),
});
export const SkitPresentationStateSchema = z.object({
  mode: z.enum(["none", "background", "blocking"]), speakerName: z.string(), body: z.string(),
  choices: z.array(z.object({ choiceId: z.string(), labelKey: z.string().optional(), label: z.string() })),
  textAreaVisible: z.boolean(), transitionVisible: z.boolean(), autoEnabled: z.boolean(),
  skipActive: z.boolean(), uiHidden: z.boolean(),
  textReveal: z.object({ mode: z.enum(["instant", "typewriter"]), intervalMs: z.number().int().nonnegative() }),
});
export const SkitPresentationDataSchema = z.object({
  sessionId: z.string(), sceneRevision: z.number().int().nonnegative(),
  presentationState: SkitPresentationStateSchema,
  allowedIntents: z.array(z.enum(["advance", "select", "set-auto", "skip", "set-ui-hidden"])),
});
