import { describe, expect, it } from "vitest";
import type { RocketLaunchPadData } from "@/bridge";
import { L } from "@/shared/i18n";
import { rocketCommandButton, rocketStatusKey } from "./rocketLaunchPadLogic";

const detail: RocketLaunchPadData = {
  phase: "readyToLaunch", waitReason: "none", progress: 0,
  materialSlots: [0], payloadSlots: [1], outputSlots: [2],
  slotBindings: [{ slot: 0, acceptedItemIds: [3] }, { slot: 1, acceptedItemIds: [4] }],
};

describe("rocket launch pad presentation", () => {
  it.each([
    ["idle", L.ui.blockInventory.rocketStatusIdle, null],
    ["awaitingBuildCommand", L.ui.blockInventory.rocketStatusAwaitingBuild, "build"],
    ["building", L.ui.blockInventory.rocketStatusBuilding, null],
    ["awaitingLaunchCommand", L.ui.blockInventory.rocketStatusAwaitingLaunch, "launch"],
    ["readyToLaunch", L.ui.blockInventory.rocketStatusReadyToLaunch, null],
    ["launching", L.ui.blockInventory.rocketStatusLaunching, null],
  ] as const)("%s has a status and only command waits have a button", (phase, key, command) => {
    const state = { ...detail, phase };
    expect(rocketStatusKey(state)).toBe(key);
    expect((rocketCommandButton(state)?.value ?? null)).toBe(command);
  });

  it.each([
    ["missingMaterials", L.ui.blockInventory.rocketWaitMissingMaterials],
    ["missingPayload", L.ui.blockInventory.rocketWaitMissingPayload],
    ["outputFull", L.ui.blockInventory.rocketWaitOutputFull],
    ["noPower", L.ui.blockInventory.rocketWaitNoPower],
  ] as const)("%s takes precedence over the phase", (waitReason, key) => {
    expect(rocketStatusKey({ ...detail, waitReason })).toBe(key);
  });
});
