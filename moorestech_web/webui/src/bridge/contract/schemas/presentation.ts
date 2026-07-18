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
