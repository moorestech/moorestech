import type { MouseEvent } from "react";
import { Tooltip } from "@mantine/core";
import type { BuildMenuEntryData } from "@/bridge";
import styles from "./style.module.css";

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
  const onMouseDown = (e: MouseEvent) => {
    e.preventDefault();
    if (e.button === 0) onLeftClick();
    if (e.button === 2) onRightClick?.();
  };

  return (
    <Tooltip label={<span className={styles.tooltip}>{entry.tooltip}</span>}>
      <div
        className={styles.slot}
        data-testid={`build-menu-entry-${entry.entryType}-${entry.entryKey}`}
        onMouseDown={onMouseDown}
        onContextMenu={(e) => e.preventDefault()}
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
