import { useTopic, useTopicSelector, dispatchAction, Topics } from "@/bridge";
import type { HotbarSlot } from "@/bridge";
import { SlotFrame } from "@/shared/ui";
import { useI18n } from "@/shared/i18n";
import { localizeSelectableTargetName, placementTargetOf } from "@/shared/placementTarget";
import { useHotbarDragSource } from "../useHotbarDragSource";
import type { DragEndpoint } from "../hotbarDnd";
import styles from "./style.module.css";

// 常時表示のホットバーHUD
// 数字キーは一切listenしない
// Always-on hotbar HUD; it only subscribes to local_player.hotbar (independent of UIState).
// Digit keys are unified into the Unity-side HotbarKeyInput, so this panel never listens for keys
export default function HotbarPanel() {
  const hotbar = useTopic(Topics.hotbar);
  // 会話中はHUDを退ける
  // Withdraw the HUD during blocking skits so the dialogue presentation owns the screen (precedent: CurrentChallengeHud)
  const skitMode = useTopicSelector(Topics.skitPresentation, (value) => value?.presentationState.mode ?? "none");
  if (skitMode === "blocking") return null;

  // 未受信の間はHUD非表示
  // Hide the whole HUD until the first snapshot
  if (!hotbar) return null;

  return (
    <div className={styles.hotbarArea}>
      <div className={styles.hotbarFrame} data-testid="hotbar-grid" data-hotbar-row data-wheel-passthrough>
        {hotbar.slots.map((slot, i) => (
          <HotbarCell key={`hotbar-${i}`} index={i} slot={slot} selected={i === hotbar.selectedSlot} />
        ))}
      </div>
    </div>
  );
}

type CellProps = { index: number; slot: HotbarSlot | null; selected: boolean };

// 1枠: 番号タブ+本体。クリックでselect
// 未解決枠も減光しドラッグ元に残す
// One slot: number tab + slot body. Click selects; only an assigned slot can start a drag
// An unresolved slot (locked target, deleted blueprint) is still assigned: a dimmed face marks it unusable while it stays a drag source
function HotbarCell({ index, slot, selected }: CellProps) {
  const { t } = useI18n();
  const source: DragEndpoint | null = slot ? { kind: "hotbarSlot", index } : null;
  const dragHandlers = useHotbarDragSource(source, () => void dispatchAction("hotbar.select", { index }));

  return (
    <div className={styles.cell} data-hotbar-slot-index={index} data-unresolved={slot?.kind === "unresolved" ? "true" : undefined}>
      <span className={styles.num}>{index + 1}</span>
      <SlotFrame filled={slot !== null} selected={selected} testId={`hotbar-slot-${index}`} {...dragHandlers}>
        {slotBody()}
      </SlotFrame>
    </div>
  );

  // アイコン種別は画像、他は名前を描画
  // Kinds with an icon draw the image, the rest draw their name; an unresolved slot has nothing to resolve so only the face shows
  function slotBody() {
    if (slot === null || slot.kind === "unresolved") return null;

    const displayName = localizeSelectableTargetName(placementTargetOf(slot), t);
    if (slot.kind === "blueprint" || slot.kind === "blueprintCopy") return <span className={styles.slotLabel}>{displayName}</span>;
    return <img className={styles.slotIcon} src={slot.iconUrl} alt={displayName} draggable={false} />;
  }
}
