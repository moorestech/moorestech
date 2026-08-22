import { useEffect, useRef, useState } from "react";
import { dispatchAction, Topics, useItemMaster, useTopic } from "@/bridge";
import { challengeTutorialTextKey, useI18n, type TranslationKey } from "@/shared/i18n";
import { TutorialAnchorRegistry, TutorialAnchorDynamicPrefixes, clipPathInset, type ClipRect, type ResolvedAnchor } from "@/shared/tutorialAnchor";
import DragGuide from "./DragGuide";
import HighlightLabel from "./HighlightLabel";
import { readTutorialHighlightGlowPx } from "./highlightGlowToken";
import styles from "./style.module.css";
import { anchoredSubscriptionSignature, assertNever, tutorialElementKey, type TutorialOverlayElement } from "../tutorialElement";

type TutorialOutlineElement = Extract<TutorialOverlayElement, { kind: "outline" }>;
type AckTarget = { tutorialSessionId: string; elementId: string };

export function TutorialOverlay() {
  const presentation = useTopic(Topics.tutorialPresentation);
  const { t } = useI18n();
  // 所持アンカーはguid→itemIdの解決にitem masterが要る。未ロード中の解決結果は所持有無を表さない
  // Owned-item anchors need the item master to resolve guid to itemId, so a resolution taken before it loads says nothing about ownership
  const itemMasterLoaded = useItemMaster() !== null;
  const registry = useRef<TutorialAnchorRegistry | null>(null);
  const lastAck = useRef<Record<string, string>>({});
  // 購読を作り直さないrevision更新でもackが最新revisionを名乗れるよう、値はrefから読む
  // Read through a ref so an ack carries the latest revision even when the subscription is not rebuilt
  const presentationRef = useRef(presentation);
  presentationRef.current = presentation;
  const subscriptionSignature = anchoredSubscriptionSignature(presentation);
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
    const subscribed = presentationRef.current;
    if (!subscribed || !registry.current) return;
    lastAck.current = {};

    // anchorId購読の重複を除去しつつ、同一anchorを指す全highlightをack対象として束ねる
    // Deduplicate anchorIds to subscribe while grouping every highlight pointing at the same anchor
    const anchorIds = new Set<string>();
    const ackTargetsByAnchorId = new Map<string, AckTarget[]>();
    for (const session of subscribed.sessions) {
      for (const element of session.elements) {
        switch (element.kind) {
          case "dragGuide":
            anchorIds.add(element.fromAnchorId);
            anchorIds.add(element.toAnchorId);
            continue;
          case "outline": {
            anchorIds.add(element.anchorId);
            const targets = ackTargetsByAnchorId.get(element.anchorId) ?? [];
            targets.push({ tutorialSessionId: session.tutorialSessionId, elementId: element.elementId });
            ackTargetsByAnchorId.set(element.anchorId, targets);
            continue;
          }
          case "keyControl":
            // keyControlは購読なし
            // keyControl has no subscription
            continue;
          default:
            assertNever(element);
            continue;
        }
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

        // item master未ロード中の所持アンカーは未確定であり、missingを「未所持」としてサーバへ流さない
        // While the item master is unloaded an owned-item anchor is indeterminate, so its missing must not reach the server as "unowned"
        if (!itemMasterLoaded && anchorId.startsWith(TutorialAnchorDynamicPrefixes.inventoryItem)) return;

        // ackはhighlightのみ送る。同一anchorを指す全highlightへ配る
        // Only highlights send an ack, and it fans out to every highlight pointing at that anchor
        const ackKey = `${value.status}:${value.reason}`;
        for (const target of ackTargetsByAnchorId.get(anchorId) ?? []) {
          const ackId = `${target.tutorialSessionId}:${target.elementId}`;
          if (lastAck.current[ackId] === ackKey) continue;
          lastAck.current[ackId] = ackKey;
          void dispatchAction("tutorial.anchor_ack", {
            tutorialSessionId: target.tutorialSessionId, revision: presentationRef.current?.revision ?? subscribed.revision,
            elementId: target.elementId, anchorId,
            status: value.status, reason: value.reason,
          });
        }
      })));
    // master到着で購読を張り直し、抑止していた所持アンカーのackを確定値で送り直す
    // Re-subscribing when the master arrives resends the suppressed owned-item acks with settled values
  }, [subscriptionSignature, itemMasterLoaded]);

  if (!presentation) return null;
  return <div className={styles.overlay} data-testid="tutorial-overlay">
    {presentation.sessions.flatMap((session) => session.elements.map((element) => {
      const key = tutorialElementKey(session.tutorialSessionId, element.elementId);
      switch (element.kind) {
        case "outline":
          return renderOutline(key, element, resolved[element.anchorId], t);
        case "dragGuide":
          return <DragGuide key={key} from={resolved[element.fromAnchorId]} to={resolved[element.toAnchorId]} />;
        case "keyControl":
          // keyControlはanchorを持たず下中央HUDが描画する
          // keyControl has no anchor and is rendered by the bottom-center HUD
          return null;
        default:
          return assertNever(element);
      }
    }))}
  </div>;
}

function renderOutline(key: string, element: TutorialOutlineElement, value: ResolvedAnchor | undefined, t: (key: TranslationKey) => string) {
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
  const outline = <div key={key} className={styles.highlight} data-kind={element.kind}
    style={{ left: box.left, top: box.top, width: box.right - box.left, height: box.bottom - box.top, clipPath }} />;
  if (!element.labelTutorialGuid) return outline;
  // 判定はアンカー実体で行う。boxで見るとpaddingPxのリングが削れただけでラベルが落ち、
  // アンカーが完全に見えていてもクリップ端に接する最上段では必ず消える（ユーザー指摘 2026-08-22）
  // Judge on the anchor itself: judging on box drops the label when only the paddingPx ring is shaved, so a
  // fully visible top row that merely touches the clip edge always loses it (user report 2026-08-22)
  const anchorBox = {
    left: value.rect.left, top: value.rect.top,
    right: value.rect.left + value.rect.width, bottom: value.rect.top + value.rect.height,
  };
  if (isClipped(anchorBox, value.clip)) return outline;
  // 辞書解決が空ならラベル面ごと出さない
  // An empty dictionary result renders no label face at all
  const labelText = t(challengeTutorialTextKey(element.labelTutorialGuid));
  if (!labelText) return outline;
  return [outline, <HighlightLabel key={`${key}:label`} box={box} clip={value.clip} text={labelText} />];
}

// ラベルはclip-pathを持たないため、アンカーが一部でも隠れている時点で描かない判定に使う
// The label carries no clip-path, so this decides to skip it the moment the anchor is partly hidden
function isClipped(box: ClipRect, clip: ClipRect): boolean {
  return clip.left > box.left || clip.top > box.top || clip.right < box.right || clip.bottom < box.bottom;
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
