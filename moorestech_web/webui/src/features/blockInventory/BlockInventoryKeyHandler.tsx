import { useEffect } from "react";
import { dispatchAction, UiStateNames } from "@/bridge";
import { readActiveLayer } from "@/shared/uiState";
import { handleBlockInventoryKeydown } from "./blockInventoryKeydown";

// 最前面がブロックインベントリのときだけEscを閉じる要求へ変換する
// Convert Escape into a close request only when block inventory is frontmost
export default function BlockInventoryKeyHandler() {
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      // 入力欄の操作は奪わず、イベント時点のpin済みtopicからレイヤーを判定する
      // Preserve text input and derive the layer from pinned topics at event time
      const tag = document.activeElement?.tagName;
      if (tag === "INPUT" || tag === "TEXTAREA") return;
      handleBlockInventoryKeydown(event.key, readActiveLayer(), () => {
        void dispatchAction("ui_state.request", { state: UiStateNames.gameScreen });
      });
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);
  return null;
}
