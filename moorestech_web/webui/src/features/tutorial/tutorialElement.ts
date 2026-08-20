import type { TutorialPresentationData } from "@/bridge";

type TutorialSession = TutorialPresentationData["sessions"][number];
export type TutorialOverlayElement = TutorialSession["elements"][number];

// sessionId+elementIdのreact key導出。TutorialOverlay/KeyControlHintHud共通
// Derives the react key from sessionId+elementId, shared by TutorialOverlay/KeyControlHintHud
export function tutorialElementKey(tutorialSessionId: string, elementId: string): string {
  return `${tutorialSessionId}:${elementId}`;
}

// anchor購読の作り直しが要る変化だけを表す署名。keyControlだけの増減では変わらない
// Signature of only the changes that require rebuilding anchor subscriptions; keyControl-only edits leave it unchanged
export function anchoredSubscriptionSignature(presentation: TutorialPresentationData | null): string {
  if (!presentation) return "";
  const parts: string[] = [];
  for (const session of presentation.sessions) {
    for (const element of session.elements) {
      const elementKey = tutorialElementKey(session.tutorialSessionId, element.elementId);
      switch (element.kind) {
        case "outline":
          parts.push(`${elementKey}:outline:${element.anchorId}`);
          continue;
        case "dragGuide":
          parts.push(`${elementKey}:dragGuide:${element.fromAnchorId}:${element.toAnchorId}`);
          continue;
        case "keyControl":
          // keyControlはanchorを持たず購読対象外
          // keyControl has no anchor and never joins the subscription set
          continue;
        default:
          assertNever(element);
          continue;
      }
    }
  }
  return parts.join("|");
}

// 種別を足したら網羅漏れをコンパイル時に落とす
// Adding a kind breaks compilation here instead of silently falling through
export function assertNever(value: never): never {
  throw new Error(`Unhandled tutorial overlay element: ${JSON.stringify(value)}`);
}
