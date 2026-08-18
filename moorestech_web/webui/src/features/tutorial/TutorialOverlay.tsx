import { useEffect, useRef, useState, type CSSProperties } from "react";
import { dispatchAction, Topics, useTopic } from "@/bridge";
import { TutorialAnchorRegistry, type ResolvedAnchor } from "@/shared/tutorialAnchor";
import styles from "./style.module.css";

export function TutorialOverlay() {
  const presentation = useTopic(Topics.tutorialPresentation);
  const registry = useRef<TutorialAnchorRegistry | null>(null);
  const lastAck = useRef<Record<string, string>>({});
  // ハイライト/ガイドを統合state化
  // Merge highlight/guide into unified state
  const [resolved, setResolved] = useState<Record<string, ResolvedAnchor>>({});

  useEffect(() => {
    registry.current = new TutorialAnchorRegistry();
    return () => {
      registry.current?.dispose();
      registry.current = null;
    };
  }, []);

  useEffect(() => {
    if (!presentation || !registry.current) return;
    lastAck.current = {};
    setResolved({});

    // anchorId購読の重複を除去
    // Deduplicate anchorIds to subscribe
    const anchorIds = new Set<string>();
    for (const highlight of presentation.highlights) anchorIds.add(highlight.anchorId);
    for (const guide of presentation.dragGuides) {
      anchorIds.add(guide.fromAnchorId);
      anchorIds.add(guide.toAnchorId);
    }
    const highlightByAnchorId = new Map(presentation.highlights.map((highlight) => [highlight.anchorId, highlight]));

    return combine([...anchorIds].map((anchorId) =>
      registry.current!.subscribe(anchorId, (value) => {
        setResolved((current) => {
          const previous = current[anchorId];
          const sameRect = previous?.status === "ready" && value.status === "ready" && previous.rect === value.rect;
          const sameNonReady = previous?.status !== "ready" && value.status !== "ready" &&
            previous?.status === value.status && previous?.reason === value.reason;
          if (sameRect || sameNonReady) return current;
          return { ...current, [anchorId]: value };
        });

        // ackはhighlightのみ送る
        // Only highlights send an ack
        const highlight = highlightByAnchorId.get(anchorId);
        if (!highlight) return;
        const ackKey = `${value.status}:${value.reason}`;
        if (lastAck.current[highlight.highlightId] === ackKey) return;
        lastAck.current[highlight.highlightId] = ackKey;
        void dispatchAction("tutorial.anchor_ack", {
          tutorialSessionId: presentation.tutorialSessionId, revision: presentation.revision,
          highlightId: highlight.highlightId, anchorId: highlight.anchorId,
          status: value.status, reason: value.reason,
        });
      })));
  }, [presentation]);

  if (!presentation) return null;
  return <div className={styles.overlay} data-testid="tutorial-overlay">
    {presentation.highlights.map((highlight) => {
      const value = resolved[highlight.anchorId];
      if (!value || value.status !== "ready") return null;
      const padding = highlight.paddingPx;
      return <div key={highlight.highlightId} className={styles.highlight} data-kind={highlight.kind}
        style={{ left: value.rect.left - padding, top: value.rect.top - padding,
          width: value.rect.width + padding * 2, height: value.rect.height + padding * 2 }} />;
    })}
    {presentation.dragGuides.map((guide) => {
      const from = resolved[guide.fromAnchorId];
      const to = resolved[guide.toAnchorId];
      if (!from || from.status !== "ready" || !to || to.status !== "ready") return null;
      const fromX = from.rect.left + from.rect.width / 2;
      const fromY = from.rect.top + from.rect.height / 2;
      const toX = to.rect.left + to.rect.width / 2;
      const toY = to.rect.top + to.rect.height / 2;
      const dragGuideVars = { "--drag-guide-dx": `${toX - fromX}px`, "--drag-guide-dy": `${toY - fromY}px` } as CSSProperties;
      return <div key={guide.guideId} className={styles.dragGuide} data-testid="tutorial-drag-guide"
        style={{ left: fromX, top: fromY, ...dragGuideVars }}>
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M6 3 L18 12 L11 13.5 L13.5 20 L10.5 21 L8 14.5 L3 18 Z" />
        </svg>
      </div>;
    })}
  </div>;
}

function combine(disposers: Array<() => void>) {
  return () => disposers.forEach((dispose) => dispose());
}
