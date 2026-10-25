import { LedgerRows, topic, ui, type BundleId, type LedgerRow, type ShotSetup } from "./referenceShots";

// 動きでしか意図が伝わらない演出の短い動画
// Short videos for effects whose intent only shows in motion
export type ReferenceVideo = ShotSetup & { id: string; bundle: BundleId; ledgerRow: LedgerRow; durationMs: number };

export const referenceVideos: ReferenceVideo[] = [
  { id: "2-notification-enter-aggregate-exit", bundle: 2, ledgerRow: LedgerRows.notification, controls: [ui("GameScreen")], durationMs: 9000,
    steps: [{ kind: "control", path: topic("notificationItemEarned") }, { kind: "wait", ms: 1500 }, { kind: "control", path: topic("notificationItemEarnedAgain") },
      { kind: "wait", ms: 1000 }, { kind: "control", path: topic("notificationAchievement") }] },
  { id: "2-key-control-hint-pulse", bundle: 2, ledgerRow: LedgerRows.keyControlHint, controls: [topic("tutorialKeyControl"), ui("GameScreen")], durationMs: 3000, steps: [] },
  { id: "2-world-pin-pulse", bundle: 2, ledgerRow: LedgerRows.worldPin, controls: [ui("GameScreen"), "/__worldpin?x=0.6&y=0.4"], durationMs: 3000, steps: [] },
  { id: "4-drag-guide", bundle: 4, ledgerRow: LedgerRows.dragGuide, controls: [topic("tutorialDragGuide"), ui("PlayerInventory")], durationMs: 4000, steps: [] },
  { id: "5-tutorial-pulse", bundle: 5, ledgerRow: LedgerRows.tutorialOverlay, controls: [topic("tutorialOutlineWithLabel"), ui("GameScreen")], durationMs: 3000, steps: [] },
];
