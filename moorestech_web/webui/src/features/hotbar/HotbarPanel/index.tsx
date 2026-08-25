import { useTopic, useTopicSelector, dispatchAction, Topics } from "@/bridge";
import type { HotbarSlot } from "@/bridge";
import { PlacementTargetFace, SlotFrame } from "@/shared/ui";
import { useI18n } from "@/shared/i18n";
import { useBlockingSkitActive, uiStateAcceptsHotbarSelect } from "@/shared/uiState";
import { localizeSelectableTargetName, placementTargetOf } from "@/shared/placementTarget";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { useHotbarDragSource } from "../useHotbarDragSource";
import type { HotbarDragSource } from "../hotbarDnd";
import styles from "./style.module.css";

// ホットバーは配置対象を9枠へ割り当てて選ぶHUDで、持ち物のアイテム欄ではない(割当元はビルドメニューのみ)
// The hotbar assigns and selects placement targets across 9 slots; it is not an inventory item bar (only the build menu assigns into it)
// 常時表示の可否は App の合成側が持ち、このHUDは画面名を知らない
// Whether this HUD shows at all is composed in App; the panel itself knows no screen names
// 数字キーは一切listenしない(Unity側HotbarKeyInputへ統一済み)
// Digit keys are unified into the Unity-side HotbarKeyInput, so this panel never listens for keys
export default function HotbarPanel() {
  const hotbar = useTopic(Topics.hotbar);
  const blockingSkitActive = useBlockingSkitActive();

  // 選択を受理しない画面ではタップ判定ごと止め、見た目でも操作不能を示す
  // On screens that reject a selection the tap is disabled outright, and the look says so too
  const selectAccepted = useTopicSelector(Topics.uiState, (data) => uiStateAcceptsHotbarSelect(data?.state ?? null));
  if (blockingSkitActive) return null;

  // 未受信の間はHUD非表示
  // Hide the whole HUD until the first snapshot
  if (!hotbar) return null;

  return (
    <div className={styles.hotbarArea}>
      <div
        className={styles.hotbarFrame}
        data-testid="hotbar-grid"
        data-hotbar-row
        data-wheel-passthrough
        data-select-disabled={selectAccepted ? undefined : "true"}
        {...tutorialAnchor(TutorialAnchorIds.hotbarHud)}
      >
        {hotbar.slots.map((slot, i) => (
          <HotbarCell key={`hotbar-${i}`} index={i} slot={slot} selected={i === hotbar.selectedSlot} selectAccepted={selectAccepted} />
        ))}
      </div>
    </div>
  );
}

type CellProps = { index: number; slot: HotbarSlot | null; selected: boolean; selectAccepted: boolean };

// 1枠: 番号タブ+本体。クリックでselect
// 未解決枠も減光しドラッグ元に残す
// One slot: number tab + slot body. Click selects; only an assigned slot can start a drag
// An unresolved slot (locked target, deleted blueprint) is still assigned: a dimmed face marks it unusable while it stays a drag source
function HotbarCell({ index, slot, selected, selectAccepted }: CellProps) {
  const { t } = useI18n();
  const source: HotbarDragSource | null = slot ? { kind: "hotbarSlot", index } : null;

  // 割当のD&Dは受理外画面でも成立するため、止めるのはタップ側だけ
  // Assignment drag-and-drop still works on rejecting screens, so only the tap is suppressed
  const onTap = () => {
    if (!selectAccepted) return;
    void dispatchAction("hotbar.select", { index });
  };
  const dragHandlers = useHotbarDragSource(source, onTap);

  return (
    <div className={styles.cell} data-hotbar-slot-index={index} data-unresolved={slot?.kind === "unresolved" ? "true" : undefined}>
      <span className={`iconTextOutlineDark ${styles.num}`}>{index + 1}</span>
      <SlotFrame filled={slot !== null} selected={selected} testId={`hotbar-slot-${index}`} {...dragHandlers}>
        {slotBody()}
      </SlotFrame>
    </div>
  );

  // 未解決枠は解決先が無いので面だけ、他はアイコン有無で共通の面へ委ねる
  // An unresolved slot has nothing to resolve so only the face shows; the rest defer to the shared face
  function slotBody() {
    if (slot === null || slot.kind === "unresolved") return null;

    return <PlacementTargetFace iconUrl={slot.iconUrl} displayName={localizeSelectableTargetName(placementTargetOf(slot), t)} />;
  }
}
