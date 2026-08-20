import type { DynamicTutorialAnchorId, StaticTutorialAnchorId } from "./anchorIds";

export type TutorialAnchorId = StaticTutorialAnchorId | DynamicTutorialAnchorId;
export type AnchorId = TutorialAnchorId;

export type TutorialAnchorAttributes = Readonly<{
  "data-tutorial-anchor": string;
}>;

// 1要素が複数のアンカー名を名乗れるよう空白区切りトークン列にする（アンカーIDに空白は含まれない）
// One element may declare several anchor names as a whitespace-separated token list (anchor IDs never contain spaces)
export function tutorialAnchor(first: TutorialAnchorId, ...rest: TutorialAnchorId[]): TutorialAnchorAttributes {
  return { "data-tutorial-anchor": [first, ...rest].join(" ") };
}

// トークン一致セレクタ。resolveAnchor と registry が同じ書式で問い合わせる
// Token-match selector shared by resolveAnchor and the registry
export function tutorialAnchorSelector(anchorId: string): string {
  const escaped = globalThis.CSS?.escape ? globalThis.CSS.escape(anchorId) : anchorId.replaceAll('"', '\\"');
  return `[data-tutorial-anchor~="${escaped}"]`;
}
