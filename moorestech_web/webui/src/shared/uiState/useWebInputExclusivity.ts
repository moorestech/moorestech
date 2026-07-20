import { useEffect } from "react";
import { sendInputState } from "@/bridge";
import { isPointerOverWebUi, isTextInputElement, reduceWebInputState, type WebInputState } from "./activeLayer";

// DOMのヒットテストとテキストフォーカスをUnityへ差分通知する
// Report DOM hit testing and text focus changes to Unity only when state changes
export function useWebInputExclusivity() {
  useEffect(() => {
    let state: WebInputState = { pointerOverUi: false, textInputFocused: false };

    const update = (change: Partial<WebInputState>) => {
      const next = reduceWebInputState(state, change);
      if (next.pointerOverUi === state.pointerOverUi && next.textInputFocused === state.textInputFocused) return;
      state = next;
      sendInputState(state.pointerOverUi, state.textInputFocused);
    };

    const onPointerMove = (event: PointerEvent) => update({ pointerOverUi: isPointerOverWebUi(event.target) });
    const onPointerLeave = () => update({ pointerOverUi: false });
    const onFocusIn = (event: FocusEvent) => update({ textInputFocused: isTextInputElement(event.target) });
    const onFocusOut = () => queueMicrotask(() => update({ textInputFocused: isTextInputElement(document.activeElement) }));
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== "Escape" || !state.textInputFocused) return;
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
    return () => {
      document.removeEventListener("pointermove", onPointerMove, true);
      document.documentElement.removeEventListener("pointerleave", onPointerLeave);
      document.removeEventListener("focusin", onFocusIn, true);
      document.removeEventListener("focusout", onFocusOut, true);
      document.removeEventListener("keydown", onKeyDown, true);
      sendInputState(false, false);
    };
  }, []);
}
