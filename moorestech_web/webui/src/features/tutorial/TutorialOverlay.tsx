import { useEffect, useRef, useState, type CSSProperties } from "react";
import { dispatchAction, Topics, useTopic } from "@/bridge";
import { TutorialAnchorRegistry, type ResolvedAnchor } from "@/shared/tutorialAnchor";
import styles from "./style.module.css";

export function TutorialOverlay() {
  const presentation = useTopic(Topics.tutorialPresentation);
  const registry = useRef<TutorialAnchorRegistry | null>(null);
  const lastAck = useRef<Record<string, string>>({});
  const [resolved, setResolved] = useState<Record<string, ResolvedAnchor>>({});
  const [resolvedGuides, setResolvedGuides] = useState<Record<string, ResolvedAnchor>>({});

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
    return combine(presentation.highlights.map((highlight) =>
      registry.current!.subscribe(highlight.anchorId, (value) => {
        setResolved((current) => ({ ...current, [highlight.highlightId]: value }));
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

  // ドラッグガイドの解決購読はハイライトと独立。ackは送らない（webui-design §8.17）
  // Drag guide resolution is subscribed independently of highlights; guides never send an ack (webui-design §8.17)
  useEffect(() => {
    if (!presentation || !registry.current) return;
    return combine(presentation.dragGuides.flatMap((guide) => [
      registry.current!.subscribe(guide.fromAnchorId, (value) =>
        setResolvedGuides((current) => ({ ...current, [`${guide.guideId}:from`]: value }))),
      registry.current!.subscribe(guide.toAnchorId, (value) =>
        setResolvedGuides((current) => ({ ...current, [`${guide.guideId}:to`]: value }))),
    ]));
  }, [presentation]);

  if (!presentation) return null;
  return <div className={styles.overlay} data-testid="tutorial-overlay">
    {presentation.highlights.map((highlight) => {
      const value = resolved[highlight.highlightId];
      if (!value || value.status !== "ready") return null;
      const padding = highlight.paddingPx;
      return <div key={highlight.highlightId} className={styles.highlight} data-kind={highlight.kind}
        style={{ left: value.rect.left - padding, top: value.rect.top - padding,
          width: value.rect.width + padding * 2, height: value.rect.height + padding * 2 }} />;
    })}
    {presentation.dragGuides.map((guide) => {
      const from = resolvedGuides[`${guide.guideId}:from`];
      const to = resolvedGuides[`${guide.guideId}:to`];
      if (!from || from.status !== "ready" || !to || to.status !== "ready") return null;
      const fromX = from.rect.left + from.rect.width / 2;
      const fromY = from.rect.top + from.rect.height / 2;
      const toX = to.rect.left + to.rect.width / 2;
      const toY = to.rect.top + to.rect.height / 2;
      return <div key={guide.guideId} className={styles.dragGuide} data-testid="tutorial-drag-guide"
        style={{ left: fromX, top: fromY,
          "--drag-guide-dx": `${toX - fromX}px`, "--drag-guide-dy": `${toY - fromY}px` } as CSSProperties}>
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
