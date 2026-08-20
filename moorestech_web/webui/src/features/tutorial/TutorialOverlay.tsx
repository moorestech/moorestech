import { useEffect, useRef, useState, type CSSProperties } from "react";
import { dispatchAction, Topics, useTopic, type TutorialPresentationData } from "@/bridge";
import { challengeTutorialTextKey, useI18n } from "@/shared/i18n";
import { TutorialAnchorRegistry, type ResolvedAnchor } from "@/shared/tutorialAnchor";
import styles from "./style.module.css";

type TutorialSession = TutorialPresentationData["sessions"][number];
type TutorialOverlayElement = TutorialSession["elements"][number];
type TutorialOutlineElement = Extract<TutorialOverlayElement, { kind: "outline" }>;
type AckTarget = { tutorialSessionId: string; elementId: string };

export function TutorialOverlay() {
  const presentation = useTopic(Topics.tutorialPresentation);
  const { t } = useI18n();
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
        // keyControlはuiState一致で出す要素でanchor購読を持たない。A6でHUD描画を足す
        // keyControl shows on uiState match and has no anchor to subscribe; A6 adds its HUD rendering
        if (element.kind === "dragGuide") {
          anchorIds.add(element.fromAnchorId);
          anchorIds.add(element.toAnchorId);
          continue;
        }
        if (element.kind !== "outline") continue;
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
      if (element.kind === "outline") return renderOutline(key, element, resolved[element.anchorId], t);
      if (element.kind === "dragGuide") return renderDragGuide(key, resolved[element.fromAnchorId], resolved[element.toAnchorId]);
      // keyControlはanchorを持たず、下中央HUD(KeyControlHintHud)が描く
      // keyControl has no anchor; the bottom-center HUD (KeyControlHintHud) renders it
      return null;
    }))}
  </div>;
}

type Translate = ReturnType<typeof useI18n>["t"];

function renderOutline(key: string, element: TutorialOutlineElement, value: ResolvedAnchor | undefined, t: Translate) {
  if (!value || value.status !== "ready") return null;
  const padding = element.paddingPx;
  const left = value.rect.left - padding;
  const outline = <div key={key} className={styles.highlight} data-kind={element.kind}
    style={{ left, top: value.rect.top - padding,
      width: value.rect.width + padding * 2, height: value.rect.height + padding * 2 }} />;
  if (!element.labelTutorialGuid) return outline;
  // ラベルは枠線の下辺外側に左揃えで置き、文言はtutorialGuid導出キーで辞書解決する
  // The label sits left-aligned just below the outline; its text resolves via the tutorialGuid-derived key
  const label = <div key={`${key}:label`} className={styles.highlightLabel} data-testid="tutorial-highlight-label"
    style={{ left, top: value.rect.top + value.rect.height + padding }}>
    {t(challengeTutorialTextKey(element.labelTutorialGuid))}
  </div>;
  return [outline, label];
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

// 矩形は参照ではなく4値で比較する。同値の再解決で再描画させないため
// Compare rects by their four values, not by reference, so a same-valued re-resolve skips the re-render
function isSameAnchor(previous: ResolvedAnchor | undefined, value: ResolvedAnchor) {
  if (!previous || previous.status !== value.status || previous.reason !== value.reason) return false;
  if (previous.status !== "ready" || value.status !== "ready") return true;
  return previous.rect.left === value.rect.left && previous.rect.top === value.rect.top &&
    previous.rect.width === value.rect.width && previous.rect.height === value.rect.height;
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
