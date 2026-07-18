/* eslint-disable local/no-jsx-visible-literal -- Speaker, body, and fallback choice labels are Unity-owned display data. */
import { useEffect, useMemo, useRef, useState } from "react";
import { dispatchAction, Topics, useConnectionStatus, useTopic } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import { clickOutcome, nextRevealCount, shouldRevealImmediately } from "./interaction";
import styles from "./style.module.css";

export function SkitPresentation() {
  const data = useTopic(Topics.skitPresentation);
  const state = data?.presentationState;
  const bodyCharacters = useMemo(() => Array.from(state?.body ?? ""), [state?.body]);
  const [visibleCount, setVisibleCount] = useState(0);
  const connectionStatus = useConnectionStatus();
  const previousConnectionStatus = useRef(connectionStatus);
  const { t } = useI18n();

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
  const allowed = new Set(data.allowedIntents);
  const base = { sessionId: data.sessionId, sceneRevision: data.sceneRevision };
  const visibleBody = bodyCharacters.slice(0, visibleCount).join("");

  const handleTextIntent = () => {
    const outcome = clickOutcome(visibleCount, bodyCharacters.length, allowed.has("advance"));
    if (outcome === "reveal") setVisibleCount(bodyCharacters.length);
    if (outcome === "advance") void dispatchAction("skit.advance", base);
  };

  const handleKeyDown = (event: React.KeyboardEvent) => {
    if (event.key !== "Enter" && event.key !== " ") return;
    event.preventDefault();
    handleTextIntent();
  };

  return <>
    {state.transitionVisible && <div className={styles.transition} data-testid="skit-transition" />}
    {state.uiHidden ? (
      state.mode === "blocking" && <button className={styles.restore} type="button" data-testid="skit-show-ui"
        onClick={() => void dispatchAction("skit.set_ui_hidden", { ...base, hidden: false })}>
        {t("Show UI")}
      </button>
    ) : state.textAreaVisible && (
      <section className={state.mode === "blocking" ? styles.blockingBox : styles.backgroundBox}
        data-testid={`${state.mode}-skit`} tabIndex={state.mode === "blocking" ? 0 : undefined}
        onClick={state.mode === "blocking" ? handleTextIntent : undefined}
        onKeyDown={state.mode === "blocking" ? handleKeyDown : undefined}>
        <div className={styles.speaker}>{state.speakerName}</div>
        <div className={styles.body}>{visibleBody}</div>
        {state.mode === "blocking" && state.choices.length > 0 && (
          <div className={styles.choices} onClick={(event) => event.stopPropagation()}
            onKeyDown={(event) => event.stopPropagation()}>
            {state.choices.map((choice) => <button type="button" key={choice.choiceId}
              onClick={() => void dispatchAction("skit.select", { ...base, choiceId: choice.choiceId })}>
              {choice.labelKey ? t(choice.labelKey) : choice.label}
            </button>)}
          </div>
        )}
        {state.mode === "blocking" && (
          <div className={styles.controls} onClick={(event) => event.stopPropagation()}
            onKeyDown={(event) => event.stopPropagation()}>
            <button type="button" disabled={!allowed.has("set-auto")}
              aria-pressed={state.autoEnabled}
              onClick={() => void dispatchAction("skit.set_auto", { ...base, enabled: !state.autoEnabled })}>
              {t("Auto")}
            </button>
            <button type="button" disabled={!allowed.has("skip") || state.skipActive}
              onClick={() => void dispatchAction("skit.skip", base)}>{t("Skip")}</button>
            <button type="button" disabled={!allowed.has("set-ui-hidden")}
              onClick={() => void dispatchAction("skit.set_ui_hidden", { ...base, hidden: true })}>
              {t("Hide UI")}
            </button>
          </div>
        )}
      </section>
    )}
  </>;
}
