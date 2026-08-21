import { useEffect } from "react";
import type { FluidMasterEntry } from "../contract/payloadTypes";
import { ensureFluidMasterLoaded, useFluidMasterStore } from "./fluidMasterStore";

// 液体マスタを購読する React フック。未ロード中は null（ロード完了時に自動再レンダー）
// React hook subscribing to the fluid master; null while unloaded, re-renders automatically on load
export function useFluidMaster(): Map<string, FluidMasterEntry> | null {
  useEffect(() => {
    ensureFluidMasterLoaded();
  }, []);
  return useFluidMasterStore((s) => s.master);
}

// イベントハンドラから購読せず最新マスタを読む
// Read the latest master from event handlers without subscribing
export function readFluidMaster(): Map<string, FluidMasterEntry> | null {
  return useFluidMasterStore.getState().master;
}
