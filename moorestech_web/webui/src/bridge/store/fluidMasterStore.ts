import { create } from "zustand";
import type { FluidMasterData, FluidMasterEntry } from "../contract/payloadTypes";
import { fluidMasterUrl } from "../transport/httpEndpoints";
import { useTopicStore } from "./topicStore";

type FluidMasterState = {
  master: Map<string, FluidMasterEntry> | null;
  setMaster: (master: Map<string, FluidMasterEntry>) => void;
};

// 液体マスタの zustand ストア（itemMasterStore.ts を踏襲）。guid をキーにする点だけが異なる
// Zustand store for the fluid master (mirrors itemMasterStore.ts); only the key differs (guid, not id)
export const useFluidMasterStore = create<FluidMasterState>((set) => ({
  master: null,
  setMaster: (master) => set({ master }),
}));

// ゲーム起動前の 503 やネットワーク断は、マウントに依存せず一定間隔で自動再試行する
// Retry on a fixed interval independent of mounts (e.g. 503 before game start, network drop)
const RETRY_INTERVAL_MS = 3000;
let started = false;
let loading = false;
let reconnectObserved = false;

// HTTP 由来の各液体に必須フィールド型が揃うことを検証する
// Validate required field types for each fluid received over HTTP
function isFluidMasterEntry(fluid: unknown): fluid is FluidMasterEntry {
  return (
    typeof fluid === "object" &&
    fluid !== null &&
    "fluidId" in fluid &&
    typeof fluid.fluidId === "number" &&
    "fluidGuid" in fluid &&
    typeof fluid.fluidGuid === "string" &&
    "color" in fluid &&
    typeof fluid.color === "string"
  );
}

// コンテナ形状と全要素を検証して不正データの流入を防ぐ
// Validate the container and every entry to keep malformed data out of the store
function isFluidMasterData(data: unknown): data is FluidMasterData {
  return (
    typeof data === "object" &&
    data !== null &&
    "fluids" in data &&
    Array.isArray(data.fluids) &&
    data.fluids.every(isFluidMasterEntry)
  );
}

export function ensureFluidMasterLoaded(): void {
  if (started) return;
  started = true;
  useTopicStore.subscribe((state) => {
    if (state.status === "reconnecting") reconnectObserved = true;
    if (state.status === "restoring" && reconnectObserved) {
      reconnectObserved = false;
      void requestLoad();
    }
  });
  void requestLoad();
}

async function requestLoad(): Promise<void> {
  if (loading) return;
  loading = true;
  await loadWithRetry();
  loading = false;
}

async function loadWithRetry(): Promise<void> {
  for (;;) {
    const res = await fetch(fluidMasterUrl).catch(() => null);
    if (res?.ok) {
      const data: unknown = await res.json().catch(() => null);
      if (isFluidMasterData(data)) {
        useFluidMasterStore.getState().setMaster(new Map(data.fluids.map((f) => [f.fluidGuid, f])));
        return;
      }
    }
    await new Promise((resolve) => setTimeout(resolve, RETRY_INTERVAL_MS));
  }
}
