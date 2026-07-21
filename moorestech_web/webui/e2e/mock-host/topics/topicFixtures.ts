import { Topics } from "../../../src/bridge/transport/protocol";
import type { PlayerInventoryData } from "../../../src/bridge/contract/payloadTypes";
import * as fx from "../fixtures";
import { state } from "../state";

export const demoMode = process.env.MOCK_DEMO === "1";

export function topicData(topic: string, inventory: PlayerInventoryData, demo: boolean): unknown {
  if (state.topicOverrides.has(topic)) return state.topicOverrides.get(topic);
  if (topic === Topics.inventory) return inventory;
  if (topic === Topics.craftRecipes) return fx.craftRecipes;
  if (topic === Topics.machineRecipes) return fx.machineRecipes;
  if (topic === Topics.itemList) return demo ? fx.demoItemList : fx.itemList;
  if (topic === Topics.blockInventory) return state.currentBlock;
  if (topic === Topics.modal) return { modal: state.currentModal };
  if (topic === Topics.progress) return demo ? fx.demoProgress : fx.progressSample;
  if (topic === Topics.uiState) return state.currentUiState;
  if (topic === Topics.researchTree) return state.researchTree;
  if (topic === Topics.buildMenu) return fx.buildMenu;
  if (topic === Topics.localization) return { locale: "japanese" };
  if (topic === Topics.challengeTree) return fx.challengeTree;
  if (topic === Topics.challengeCurrent) return fx.challengeCurrent;
  if (topic === Topics.pauseMenu) return { disconnected: false };
  if (topic === Topics.placementMode) return { selectedName: "", height: 0, unavailableReason: "", energizedRangeVisible: false };
  if (topic === Topics.deleteMode) return { unavailableReason: "" };
  if (topic === Topics.crosshair) return { visible: true };
  if (topic === Topics.uiVisibility) return { visible: true };
  if (topic === Topics.miningHud) return { visible: false, targetName: "", mining: false, progress: 0 };
  if (topic === Topics.tooltip) return { visible: false, textKey: "", fontSize: 14 };
  if (topic === Topics.gameState) return state.gameState;
  if (topic === Topics.tutorialPresentation) return fx.tutorialPresentation;
  if (topic === Topics.skitPresentation) return state.skitPresentation;
  if (topic === Topics.trainRiding) return state.trainRiding;
  return undefined;
}
