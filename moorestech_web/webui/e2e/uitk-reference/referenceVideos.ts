import { NOTIFICATION_DISPLAY_MS } from "../../src/features/notification/notificationStore";
import { LedgerRows, topic, ui, visible, content, type BundleId, type LedgerRow, type ShotSetup } from "./referenceShots";

// 動きでしか意図が伝わらない演出の短い動画
// Short videos for effects whose intent only shows in motion
type ReferenceVideo = ShotSetup & { id: string; bundle: BundleId; ledgerRow: LedgerRow; durationMs: number };

// 通知は既存の生存尺に、集約操作と前後の余白5秒を加える
// Add five seconds for aggregation steps and margins to the existing notification lifetime
export const referenceVideos: ReferenceVideo[] = [
  { id: "2-notification-enter-aggregate-exit", bundle: 2, ledgerRow: LedgerRows.notification, controls: [ui("GameScreen")], durationMs: NOTIFICATION_DISPLAY_MS + 5000,
    steps: [visible("hotbar-grid"), { kind: "control", path: topic("notificationItemEarned") }, content("notification-row", ["5"]), { kind: "wait", ms: 1500 }, { kind: "control", path: topic("notificationItemEarnedAgain") }, content("notification-row", ["8"]),
      { kind: "wait", ms: 1000 }, { kind: "control", path: topic("notificationAchievement") }, content("notification-row", ["8", "研究完了"]), content("notification-row", [])] },
  { id: "2-key-control-hint-pulse", bundle: 2, ledgerRow: LedgerRows.keyControlHint, controls: [topic("tutorialKeyControl"), ui("GameScreen")], durationMs: 3000, steps: [visible("key-control-hint")] },
  { id: "2-world-pin-pulse", bundle: 2, ledgerRow: LedgerRows.worldPin, controls: [ui("GameScreen"), "/__worldpin?x=0.6&y=0.4"], durationMs: 3000, steps: [visible("world-pin-map-object-pin")] },
  { id: "4-drag-guide", bundle: 4, ledgerRow: LedgerRows.dragGuide, controls: [topic("tutorialDragGuide"), ui("PlayerInventory")], durationMs: 4000, steps: [visible("tutorial-drag-guide")] },
  { id: "5-tutorial-pulse", bundle: 5, ledgerRow: LedgerRows.tutorialOverlay, controls: [topic("tutorialOutlineWithLabel"), ui("GameScreen")], durationMs: 3000, steps: [visible("tutorial-highlight-label")] },
];
