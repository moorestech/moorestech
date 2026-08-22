import { create } from "zustand";
import { FluidMasterDataSchema } from "../contract/schemas";
import type { FluidMasterData, FluidMasterEntry } from "../contract/payloadTypes";
import { fluidMasterUrl } from "../transport/httpEndpoints";
import { createMasterLoader } from "./itemMasterStore";

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

export const ensureFluidMasterLoaded = createMasterLoader<FluidMasterData>({
  url: fluidMasterUrl,
  // 境界の検証は契約スキーマ一本。色書式(#RRGGBB)もguid書式もここで実際に効かせる
  // The contract schema is the only boundary check, so the #RRGGBB and guid formats are actually enforced here
  parse: (data) => {
    const result = FluidMasterDataSchema.safeParse(data);
    return result.success ? result.data : null;
  },
  apply: (data) => useFluidMasterStore.getState().setMaster(new Map(data.fluids.map((f) => [f.fluidGuid, f]))),
});
