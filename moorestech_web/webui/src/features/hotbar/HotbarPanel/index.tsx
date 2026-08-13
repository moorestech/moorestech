import { useTopic, dispatchAction, Topics } from "@/bridge";
import type { HotbarSlot } from "@/bridge";
import { SlotFrame } from "@/shared/ui";
import { useHotbarDragSource } from "../useHotbarDragSource";
import type { DragEndpoint } from "../hotbarDnd";
import styles from "./style.module.css";

// 常時表示ホットバーHUD。local_player.hotbar を購読するだけ(UIStateには依存しない)。
// 数字キーはUnity側HotbarKeyInputに一本化済みのため、ここではキーを一切listenしない
// Always-on hotbar HUD; it only subscribes to local_player.hotbar (independent of UIState).
// Digit keys are unified into the Unity-side HotbarKeyInput, so this panel never listens for keys
export default function HotbarPanel() {
  const hotbar = useTopic(Topics.hotbar);

  // snapshot未受信の間はHUDごと出さない
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

// 1枠: 番号タブ+スロット本体。クリックはselect、割当済みの枠だけがドラッグ元になる
// One slot: number tab + slot body. Click selects; only an assigned slot can start a drag
function HotbarCell({ index, slot, selected }: CellProps) {
  const source: DragEndpoint | null = slot ? { kind: "hotbarSlot", index } : null;
  const dragHandlers = useHotbarDragSource(source, () => void dispatchAction("hotbar.select", { index }));

  return (
    <div className={styles.cell} data-hotbar-slot-index={index}>
      <span className={styles.num}>{index + 1}</span>
      <SlotFrame filled={slot !== null} selected={selected} testId={`hotbar-slot-${index}`} {...dragHandlers}>
        {slot?.iconUrl ? (
          <img className={styles.slotIcon} src={slot.iconUrl} alt={slot.label} draggable={false} />
        ) : slot ? (
          <span className={styles.slotLabel}>{slot.label}</span>
        ) : null}
      </SlotFrame>
    </div>
  );
}
