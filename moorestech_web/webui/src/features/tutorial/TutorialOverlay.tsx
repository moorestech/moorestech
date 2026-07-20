/* eslint-disable local/no-jsx-visible-literal -- Tutorial message is server-owned display data; messageKey migration is handled by the tutorial producer. */
import { useEffect, useRef, useState } from "react";
import { dispatchAction, Topics, useTopic } from "@/bridge";
import { TutorialAnchorRegistry, type ResolvedAnchor } from "@/shared/tutorialAnchor";
import styles from "./style.module.css";

export function TutorialOverlay() {
  const presentation = useTopic(Topics.tutorialPresentation);
  const registry = useRef<TutorialAnchorRegistry | null>(null);
  const lastAck = useRef<Record<string, string>>({});
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

  if (!presentation) return null;
  return <div className={styles.overlay} data-testid="tutorial-overlay">
    {presentation.highlights.map((highlight) => {
      const value = resolved[highlight.highlightId];
      if (!value || value.status !== "ready") return null;
      const padding = highlight.paddingPx;
      return <div key={highlight.highlightId} className={styles.highlight} data-kind={highlight.kind}
        style={{ left: value.rect.left - padding, top: value.rect.top - padding,
          width: value.rect.width + padding * 2, height: value.rect.height + padding * 2 }}>
        {highlight.kind === "callout" && highlight.message && <div className={styles.callout}>{highlight.message}</div>}
      </div>;
    })}
  </div>;
}

function combine(disposers: Array<() => void>) {
  return () => disposers.forEach((dispose) => dispose());
}
