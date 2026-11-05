import { useEffect, useRef } from "react";
import { sendInputState } from "@/bridge";
import { browserDefaultSuppressionFor, isPointerOverWebUi, isTextInputElement, reduceWebInputState, type WebInputState } from "./activeLayer";

// DOMのヒットテストとテキストフォーカスをUnityへ差分通知する
// Report DOM hit testing and text focus changes to Unity only when state changes
export function useWebInputExclusivity(webUiSuspended: boolean) {
  // 休止解除で捨てる送信済みキャッシュと、解除時の再判定に使う最後のポインタ位置
  // Sent-state cache dropped on resume, and the last pointer position used to re-hit-test then
  const sentStateRef = useRef<WebInputState>({ pointerOverUi: false, textInputFocused: false });
  const lastPointerRef = useRef<{ x: number; y: number } | null>(null);
  const wasSuspendedRef = useRef(webUiSuspended);

  // 休止中Unityは両値をfalseへ捨てるので、解除時はキャッシュを使わず実状態を必ず送り直す
  // Unity drops both values while suspended, so on resume always resend the real state, bypassing the cache
  useEffect(() => {
    const resumed = wasSuspendedRef.current && !webUiSuspended;
    wasSuspendedRef.current = webUiSuspended;
    if (!resumed) return;
    const pointer = lastPointerRef.current;
    const pointerOverUi = pointer != null && isPointerOverWebUi(document.elementFromPoint(pointer.x, pointer.y));
    sentStateRef.current = { pointerOverUi, textInputFocused: isTextInputElement(document.activeElement) };
    sendInputState(sentStateRef.current.pointerOverUi, sentStateRef.current.textInputFocused);
  }, [webUiSuspended]);

  useEffect(() => {
    const update = (change: Partial<WebInputState>) => {
      const state = sentStateRef.current;
      const next = reduceWebInputState(state, change);
      if (next.pointerOverUi === state.pointerOverUi && next.textInputFocused === state.textInputFocused) return;
      sentStateRef.current = next;
      sendInputState(next.pointerOverUi, next.textInputFocused);
    };

    const onPointerMove = (event: PointerEvent) => {
      lastPointerRef.current = { x: event.clientX, y: event.clientY };
      update({ pointerOverUi: isPointerOverWebUi(event.target) });
    };
    const onPointerLeave = () => {
      lastPointerRef.current = null;
      update({ pointerOverUi: false });
    };
    const onFocusIn = (event: FocusEvent) => update({ textInputFocused: isTextInputElement(event.target) });
    const onFocusOut = () => queueMicrotask(() => update({ textInputFocused: isTextInputElement(document.activeElement) }));
    // keydown/keyupが同じ判別値を引くので、封じ方の非対称が生まれない
    // Both keydown and keyup read the same discriminant, so the two can never drift apart
    const applySuppression = (event: KeyboardEvent): boolean => {
      const kind = browserDefaultSuppressionFor(event.key, document.activeElement);
      switch (kind) {
        case "allow":
          return false;
        case "preventDefault":
          event.preventDefault();
          return true;
        case "preventDefaultAndStopPropagation":
          event.preventDefault();
          event.stopPropagation();
          return true;
        default: {
          const exhaustive: never = kind;
          return exhaustive;
        }
      }
    };

    const onKeyUp = (event: KeyboardEvent) => {
      applySuppression(event);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (applySuppression(event)) return;
      if (event.key !== "Escape" || !sentStateRef.current.textInputFocused) return;
      (document.activeElement as HTMLElement | null)?.blur();
      event.preventDefault();
      event.stopPropagation();
      update({ textInputFocused: false });
    };

    document.addEventListener("pointermove", onPointerMove, true);
    document.documentElement.addEventListener("pointerleave", onPointerLeave);
    document.addEventListener("focusin", onFocusIn, true);
    document.addEventListener("focusout", onFocusOut, true);
    document.addEventListener("keydown", onKeyDown, true);
    document.addEventListener("keyup", onKeyUp, true);
    return () => {
      document.removeEventListener("pointermove", onPointerMove, true);
      document.documentElement.removeEventListener("pointerleave", onPointerLeave);
      document.removeEventListener("focusin", onFocusIn, true);
      document.removeEventListener("focusout", onFocusOut, true);
      document.removeEventListener("keydown", onKeyDown, true);
      document.removeEventListener("keyup", onKeyUp, true);
      sentStateRef.current = { pointerOverUi: false, textInputFocused: false };
      sendInputState(false, false);
    };
  }, []);
}
