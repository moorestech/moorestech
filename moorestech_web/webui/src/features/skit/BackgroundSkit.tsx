import { useEffect, useState } from "react";
import { Topics, useTopic } from "@/bridge";
import styles from "./style.module.css";

export function BackgroundSkit() {
  const data = useTopic(Topics.skitPresentation);
  const [visibleBody, setVisibleBody] = useState("");
  const state = data?.presentationState;

  useEffect(() => {
    if (!state || state.mode !== "background") {
      setVisibleBody("");
      return;
    }
    if (state.textReveal.mode === "instant" || state.textReveal.intervalMs === 0) {
      setVisibleBody(state.body);
      return;
    }
    setVisibleBody("");
    let count = 0;
    const timer = window.setInterval(() => {
      count += 1;
      setVisibleBody(state.body.slice(0, count));
      if (count >= state.body.length) window.clearInterval(timer);
    }, state.textReveal.intervalMs);
    return () => window.clearInterval(timer);
  }, [data?.sessionId, data?.sceneRevision, state]);

  if (!state || state.mode !== "background" || !state.textAreaVisible || state.uiHidden) return null;
  return <section className={styles.box} data-testid="background-skit">
    <div className={styles.speaker}>{state.speakerName}</div>
    <div className={styles.body}>{visibleBody}</div>
  </section>;
}
