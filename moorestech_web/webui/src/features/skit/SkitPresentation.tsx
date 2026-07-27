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

export function SkitPresentation() {
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
  const allowedIntents = new Set<string>(data.allowedIntents);
  const base: ActionPayloads["skit.advance"] = { sessionId: data.sessionId, sceneRevision: data.sceneRevision };
  const visibleBody = bodyCharacters.slice(0, visibleCount).join("");

  const handleTextIntent = () => {
    const outcome = clickOutcome(visibleCount, bodyCharacters.length, allowedIntents.has("advance"));
    if (outcome === "reveal") setVisibleCount(bodyCharacters.length);
    if (outcome === "advance") void dispatchAction("skit.advance", base);
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
    return <div className={styles.root}><SkitRestoreButton base={base} /></div>;
  }
  if (!state.textAreaVisible) return null;

  // 背景スキットは面も枠も持たず、採掘・設置を殺さないよう入力を素通しする
  // Background skits carry no face or frame and pass input through so mining and placing keep working
  if (state.mode === "background") {
    return (
      <div className={styles.root}>
        <div className={styles.backgroundLine} data-testid="background-skit">
          {state.speakerName + BACKGROUND_SEPARATOR + visibleBody}
        </div>
      </div>
    );
  }

  const revealCompleted = visibleCount >= bodyCharacters.length;

  return (
    <div className={styles.root}>
      <GamePanel variant="skit">
        <section className={styles.window} data-testid="blocking-skit" tabIndex={0}
          onClick={handleTextIntent} onKeyDown={handleKeyDown}>
          <div className={styles.speaker}>{state.speakerName}</div>
          <div className={styles.rule}><FadeRule /></div>
          <div className={styles.body}>{visibleBody}</div>
          {revealCompleted && allowedIntents.has("advance") && <AdvanceMarkerIcon />}
        </section>
      </GamePanel>
      {state.choices.length > 0 && <SkitChoiceList choices={state.choices} base={base} />}
      <SkitToolbar base={base} allowedIntents={allowedIntents}
        autoEnabled={state.autoEnabled} skipActive={state.skipActive} />
    </div>
  );
}
