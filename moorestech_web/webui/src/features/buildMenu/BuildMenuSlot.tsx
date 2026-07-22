import { Tooltip } from "@mantine/core";
import type { BuildMenuEntryData } from "@/bridge";
import { useSlotMouse } from "@/shared/ui";
import styles from "./style.module.css";
import { tutorialAnchor, buildMenuEntryAnchorId } from "@/shared/tutorialAnchor";

type Props = {
  entry: BuildMenuEntryData;
  onLeftClick: () => void;
  // BPエントリのみ右クリック削除を受け付ける
  // Only blueprint entries accept right-click deletion
  onRightClick?: () => void;
};

// アイコン有無で画像/テキストを出し分けるビルドメニュー1スロット
// One build-menu slot, rendering an image or a text label depending on icon presence
export default function BuildMenuSlot({ entry, onLeftClick, onRightClick }: Props) {
  const slotMouse = useSlotMouse(() => onLeftClick(), onRightClick);

  return (
    <Tooltip label={<span className={styles.tooltip}>{entry.tooltip}</span>}>
      <div
        className={styles.slot}
        data-testid={`build-menu-entry-${entry.entryType}-${entry.entryKey}`}
        {...tutorialAnchor(buildMenuEntryAnchorId(entry.entryType, entry.entryKey))}
        onMouseDown={slotMouse.onMouseDown}
        onContextMenu={slotMouse.onContextMenu}
      >
        {entry.iconUrl ? (
          <img src={entry.iconUrl} alt={entry.label} className={styles.icon} draggable={false} />
        ) : (
          <span className={styles.label}>{entry.label}</span>
        )}
      </div>
    </Tooltip>
  );
}
