import { useEffect, useMemo, useRef, useState } from "react";
import { dispatchAction, Topics, useConnectionStatus, useTopic, type ActionPayloads } from "@/bridge";
import { FadeRule, GamePanel } from "@/shared/ui";
import { clickOutcome, nextRevealCount, shouldRevealImmediately } from "./interaction";
import { SkitChoiceList } from "./controls/SkitChoiceList";
import { SkitRestoreButton, SkitToolbar } from "./controls/SkitToolbar";
import { AdvanceMarkerIcon } from "./icons";
import styles from "./style.module.css";

// 背景スキットは正本 BackgroundText と同じ「話者名 : 本文」の1行に畳む
// Background skits collapse into the reference BackgroundText's single "speaker : body" line
const BACKGROUND_SEPARATOR = " : ";

// ポーズ中も表示と自動送りは続けるため、入力可否だけをpropで受ける
// Presentation and auto-advance keep running during pause, so only input acceptance arrives as a prop
export function SkitPresentation({ interactive }: { interactive: boolean }) {
  const data = useTopic(Topics.skitPresentation);
  const state = data?.presentationState;
  const bodyCharacters = useMemo(() => Array.from(state?.body ?? ""), [state?.body]);
  const [visibleCount, setVisibleCount] = useState(0);
  const connectionStatus = useConnectionStatus();
  const previousConnectionStatus = useRef(connectionStatus);

  useEffect(() => {
    const revealImmediately = shouldRevealImmediately(
      connectionStatus, previousConnectionStatus.current, state?.textReveal.mode ?? "instant",
      state?.textReveal.intervalMs ?? 0,
    );
    previousConnectionStatus.current = connectionStatus;
    if (!state || state.mode === "none") {
      setVisibleCount(0);
      return;
    }
    // 初回接続と再接続snapshotは復元表示なので全文を即時表示する
    // Initial and reconnect snapshots restore the view, so reveal the full body immediately
    if (revealImmediately) {
      setVisibleCount(bodyCharacters.length);
      return;
    }
    setVisibleCount(0);
    const timer = window.setInterval(() => {
      setVisibleCount((current) => {
        const next = nextRevealCount(state.body, current);
        if (next >= bodyCharacters.length) window.clearInterval(timer);
        return next;
      });
    }, state.textReveal.intervalMs);
    return () => window.clearInterval(timer);
  }, [connectionStatus, data?.sessionId, state?.body, state?.textReveal.mode, state?.textReveal.intervalMs, bodyCharacters.length]);

  if (!data || !state || state.mode === "none") return null;
  // 入力不可のときは全入力口をまとめて閉じる（会話窓・選択肢・ツールバー・復帰ボタン）
  // When input is refused, every input path closes at once (window, choices, toolbar, restore button)
  const rootClassName = interactive ? styles.root : styles.root + " " + styles.inputBlocked;
  const windowTabIndex = interactive ? 0 : -1;
  const allowedIntents = new Set(data.allowedIntents);
  const base: ActionPayloads["skit.advance"] = { sessionId: data.sessionId, sceneRevision: data.sceneRevision };
  const visibleBody = bodyCharacters.slice(0, visibleCount).join("");

  // クリックの帰結は単一評価器で1回だけ決め、挙動と送り待ちマーカー表示の両方で共有する
  // Evaluate the click outcome once through the single evaluator and share it between behavior and the advance marker
  const clickIntent = clickOutcome(visibleCount, bodyCharacters.length, allowedIntents.has("advance"));
  const handleTextIntent = () => {
    if (clickIntent === "reveal") setVisibleCount(bodyCharacters.length);
    if (clickIntent === "advance") void dispatchAction("skit.advance", base);
  };

  const handleKeyDown = (event: React.KeyboardEvent) => {
    if (event.key !== "Enter" && event.key !== " ") return;
    event.preventDefault();
    handleTextIntent();
  };

  // UI非表示中は復帰アイコンだけを残す
  // While the UI is hidden only the restore icon remains
  if (state.uiHidden) {
    if (state.mode !== "blocking") return null;
    return <div className={rootClassName}><SkitRestoreButton base={base} /></div>;
  }
  // 演出中は会話窓と選択肢だけを畳み、ツールバーは残す（正本もTextAreaとツールは兄弟でTextAreaだけが消える）
  // During staging only the window and choices fold away while the toolbar stays, as in the reference where the TextArea and tools are siblings
  if (!state.textAreaVisible) {
    if (state.mode !== "blocking") return null;
    return (
      <div className={rootClassName}>
        <SkitToolbar base={base} allowedIntents={allowedIntents}
          autoEnabled={state.autoEnabled} skipActive={state.skipActive} />
      </div>
    );
  }

  // 背景スキットは面も枠も持たず、採掘・設置を殺さないよう入力を素通しする
  // Background skits carry no face or frame and pass input through so mining and placing keep working
  if (state.mode === "background") {
    // 初回SetBackgroundText前の空snapshotでは行を出さない（正本は台詞が来るまで非表示）
    // Skip the empty pre-SetBackgroundText snapshot; the reference shows nothing until the first line arrives
    if (state.body === "") return null;
    return (
      <div className={rootClassName}>
        <div className={styles.backgroundLine} data-testid="background-skit">
          {state.speakerName === "" ? visibleBody : state.speakerName + BACKGROUND_SEPARATOR + visibleBody}
        </div>
      </div>
    );
  }

  return (
    <div className={rootClassName}>
      <GamePanel variant="skit">
        <section className={styles.window} data-testid="blocking-skit" tabIndex={windowTabIndex}
          onClick={handleTextIntent} onKeyDown={handleKeyDown}>
          {/* Unity側resolverで解決済みの表示文字列をpushするためt()を通さない */}
          {/* Unity pushes resolver-completed display strings, so they bypass t() */}
          <div className={styles.speaker}>{state.speakerName}</div>
          <div className={styles.rule}><FadeRule /></div>
          <div className={styles.body}>{visibleBody}</div>
          {clickIntent === "advance" && <AdvanceMarkerIcon />}
        </section>
      </GamePanel>
      {state.choices.length > 0 && allowedIntents.has("select") && <SkitChoiceList choices={state.choices} base={base} />}
      <SkitToolbar base={base} allowedIntents={allowedIntents}
        autoEnabled={state.autoEnabled} skipActive={state.skipActive} />
    </div>
  );
}
