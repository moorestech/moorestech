export type TutorialAnchorId = `${Lowercase<string>}.${Lowercase<string>}`;
export type AnchorId = TutorialAnchorId;

export type TutorialAnchorAttributes = Readonly<{
  "data-tutorial-anchor": TutorialAnchorId;
}>;

export function tutorialAnchor(anchorId: TutorialAnchorId): TutorialAnchorAttributes {
  return { "data-tutorial-anchor": anchorId };
}
