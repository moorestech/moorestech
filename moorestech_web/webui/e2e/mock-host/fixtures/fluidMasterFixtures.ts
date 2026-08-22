import type { FluidMasterData } from "../../../src/bridge/contract/payloadTypes";
import { WATER_FLUID_GUID } from "./contentLocalizationFixtures";

// Unity側 FluidMasterEndpoint のDTO({ Fluids: [{ FluidGuid, Color }] })をcamelCase化した形と厳密一致させる
// Matches, field-for-field, the camelCased shape of Unity's FluidMasterEndpoint DTO ({ Fluids: [{ FluidGuid, Color }] })
export const fluidMaster = {
  fluids: [{ fluidGuid: WATER_FLUID_GUID, color: "#2A6FE0" }],
} satisfies FluidMasterData;
