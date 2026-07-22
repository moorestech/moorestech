import type { DynamicTutorialAnchorId, StaticTutorialAnchorId } from "./anchorIds";

export type TutorialAnchorId = StaticTutorialAnchorId | DynamicTutorialAnchorId;
export type AnchorId = TutorialAnchorId;

export type TutorialAnchorAttributes = Readonly<{
  "data-tutorial-anchor": TutorialAnchorId;
}>;

export function tutorialAnchor(anchorId: TutorialAnchorId): TutorialAnchorAttributes {
  return { "data-tutorial-anchor": anchorId };
}
