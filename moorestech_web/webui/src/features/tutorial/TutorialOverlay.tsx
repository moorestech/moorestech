import { useEffect, useRef, useState, type CSSProperties } from "react";
import { dispatchAction, Topics, useTopic, type TutorialPresentationData } from "@/bridge";
import { TutorialAnchorRegistry, clipPathInset, type ClipRect, type ResolvedAnchor } from "@/shared/tutorialAnchor";
import { readTutorialHighlightGlowPx } from "./highlightGlowToken";
import styles from "./style.module.css";

type TutorialSession = TutorialPresentationData["sessions"][number];
type TutorialOverlayElement = TutorialSession["elements"][number];
type TutorialOutlineElement = Extract<TutorialOverlayElement, { kind: "outline" }>;
type AckTarget = { tutorialSessionId: string; elementId: string };

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

    // anchorId購読の重複を除去しつつ、同一anchorを指す全highlightをack対象として束ねる
    // Deduplicate anchorIds to subscribe while grouping every highlight pointing at the same anchor
    const anchorIds = new Set<string>();
    const ackTargetsByAnchorId = new Map<string, AckTarget[]>();
    for (const session of presentation.sessions) {
      for (const element of session.elements) {
        if (element.kind !== "outline") {
          anchorIds.add(element.fromAnchorId);
          anchorIds.add(element.toAnchorId);
          continue;
        }
        anchorIds.add(element.anchorId);
        const targets = ackTargetsByAnchorId.get(element.anchorId) ?? [];
        targets.push({ tutorialSessionId: session.tutorialSessionId, elementId: element.elementId });
        ackTargetsByAnchorId.set(element.anchorId, targets);
      }
    }

    // 購読対象から外れたanchorだけ落とし、表示中要素が1フレーム消灯するのを防ぐ
    // Drop only the anchors that left the subscription set so visible elements never blink for a frame
    setResolved((current) => keepSubscribed(current, anchorIds));

    return combine([...anchorIds].map((anchorId) =>
      registry.current!.subscribe(anchorId, (value) => {
        setResolved((current) => {
          if (isSameAnchor(current[anchorId], value)) return current;
          return { ...current, [anchorId]: value };
        });

        // ackはhighlightのみ送る。同一anchorを指す全highlightへ配る
        // Only highlights send an ack, and it fans out to every highlight pointing at that anchor
        const ackKey = `${value.status}:${value.reason}`;
        for (const target of ackTargetsByAnchorId.get(anchorId) ?? []) {
          const ackId = `${target.tutorialSessionId}:${target.elementId}`;
          if (lastAck.current[ackId] === ackKey) continue;
          lastAck.current[ackId] = ackKey;
          void dispatchAction("tutorial.anchor_ack", {
            tutorialSessionId: target.tutorialSessionId, revision: presentation.revision,
            elementId: target.elementId, anchorId,
            status: value.status, reason: value.reason,
          });
        }
      })));
  }, [presentation]);

  if (!presentation) return null;
  return <div className={styles.overlay} data-testid="tutorial-overlay">
    {presentation.sessions.flatMap((session) => session.elements.map((element) => {
      const key = `${session.tutorialSessionId}:${element.elementId}`;
      if (element.kind === "outline") return renderOutline(key, element, resolved[element.anchorId]);
      return renderDragGuide(key, resolved[element.fromAnchorId], resolved[element.toAnchorId]);
    }))}
  </div>;
}

function renderOutline(key: string, element: TutorialOutlineElement, value: ResolvedAnchor | undefined) {
  if (!value || value.status !== "ready") return null;
  const padding = element.paddingPx;
  const box = {
    left: value.rect.left - padding, top: value.rect.top - padding,
    right: value.rect.left + value.rect.width + padding,
    bottom: value.rect.top + value.rect.height + padding,
  };
  // 祖先のoverflowで完全に隠れている間は要素ごと出さず、DOMと見た目を一致させる
  // While ancestor overflow hides it entirely, omit the element so the DOM matches what is painted
  const clipPath = clipPathInset({ box, clip: value.clip, outsetPx: readTutorialHighlightGlowPx() });
  if (clipPath === null) return null;
  return <div key={key} className={styles.highlight} data-kind={element.kind}
    style={{ left: box.left, top: box.top, width: box.right - box.left, height: box.bottom - box.top, clipPath }} />;
}

function renderDragGuide(key: string, from: ResolvedAnchor | undefined, to: ResolvedAnchor | undefined) {
  if (!from || from.status !== "ready" || !to || to.status !== "ready") return null;
  const fromX = from.rect.left + from.rect.width / 2;
  const fromY = from.rect.top + from.rect.height / 2;
  const toX = to.rect.left + to.rect.width / 2;
  const toY = to.rect.top + to.rect.height / 2;
  const dragGuideVars = { "--drag-guide-dx": `${toX - fromX}px`, "--drag-guide-dy": `${toY - fromY}px` } as CSSProperties;
  return <div key={key} className={styles.dragGuide} data-testid="tutorial-drag-guide"
    style={{ left: fromX, top: fromY, ...dragGuideVars }}>
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M6 3 L18 12 L11 13.5 L13.5 20 L10.5 21 L8 14.5 L3 18 Z" />
    </svg>
  </div>;
}

// 矩形とクリップは参照ではなく値で比較する。同値の再解決で再描画させないため
// Compare the rect and the clip by value, not by reference, so a same-valued re-resolve skips the re-render
function isSameAnchor(previous: ResolvedAnchor | undefined, value: ResolvedAnchor) {
  if (!previous || previous.status !== value.status || previous.reason !== value.reason) return false;
  if (previous.status !== "ready" || value.status !== "ready") return true;
  return previous.rect.left === value.rect.left && previous.rect.top === value.rect.top &&
    previous.rect.width === value.rect.width && previous.rect.height === value.rect.height &&
    isSameClip(previous.clip, value.clip);
}

function isSameClip(previous: ClipRect, value: ClipRect) {
  return previous.left === value.left && previous.top === value.top &&
    previous.right === value.right && previous.bottom === value.bottom;
}

function keepSubscribed(current: Record<string, ResolvedAnchor>, anchorIds: Set<string>) {
  const kept: Record<string, ResolvedAnchor> = {};
  for (const anchorId of anchorIds) {
    if (current[anchorId]) kept[anchorId] = current[anchorId];
  }
  if (Object.keys(kept).length === Object.keys(current).length) return current;
  return kept;
}

function combine(disposers: Array<() => void>) {
  return () => disposers.forEach((dispose) => dispose());
}
