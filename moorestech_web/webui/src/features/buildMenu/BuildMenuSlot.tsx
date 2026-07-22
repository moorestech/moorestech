import { SlotFrame } from "@/shared/ui";
import type { BuildMenuEntryData } from "@/bridge";
import { tutorialAnchor, buildMenuEntryAnchorId } from "@/shared/tutorialAnchor";
import styles from "./style.module.css";

type Props = {
  entry: BuildMenuEntryData;
  onLeftClick: () => void;
  // BPエントリのみ右クリック削除を受け付ける
  // Only blueprint entries accept right-click deletion
  onRightClick?: () => void;
  onHoverChange: (hovering: boolean) => void;
};

// アイコン有無で画像/テキストを出し分けるビルドメニュー1スロット
// One build-menu slot, rendering an image or a text label depending on icon presence
export function BuildMenuSlot({ entry, onLeftClick, onRightClick, onHoverChange }: Props) {
  return (
    <SlotFrame
      filled
      testId={`build-menu-entry-${entry.entryType}-${entry.entryKey}`}
      onLeftDown={onLeftClick}
      onRightDown={onRightClick}
      onHoverChange={onHoverChange}
      {...tutorialAnchor(buildMenuEntryAnchorId(entry.entryType, entry.entryKey))}
    >
      {entry.iconUrl ? (
        <img className={styles.slotIcon} src={entry.iconUrl} alt={entry.label} draggable={false} />
      ) : (
        <span className={styles.slotLabel}>{entry.label}</span>
      )}
    </SlotFrame>
  );
}
