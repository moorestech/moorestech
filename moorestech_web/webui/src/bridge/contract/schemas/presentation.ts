import { z } from "zod";

export const GameStateDataSchema = z.object({ state: z.enum(["InGame", "Skit", "CutScene"]) });
export const TutorialHighlightSchema = z.object({
  highlightId: z.string(), anchorId: z.string(), kind: z.enum(["outline", "spotlight", "callout"]),
  messageKey: z.string().optional(), message: z.string(), paddingPx: z.number().nonnegative(),
  blocksPointerInput: z.boolean(),
});
export const TutorialPresentationDataSchema = z.object({
  tutorialSessionId: z.string(), revision: z.number().int().nonnegative(),
  challengeId: z.string(), highlights: z.array(TutorialHighlightSchema),
});
// ワールドピン: Unityが射影した正規化スクリーン座標（0..1左上原点）と画面外矢印用の方向ベクトル
// World pins: Unity-projected normalized screen coords (0..1, top-left origin) plus a direction vector for off-screen arrows
export const WorldPinSchema = z.object({
  pinId: z.string(), text: z.string(),
  screenX: z.number(), screenY: z.number(), onScreen: z.boolean(),
  directionX: z.number(), directionY: z.number(),
});
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
