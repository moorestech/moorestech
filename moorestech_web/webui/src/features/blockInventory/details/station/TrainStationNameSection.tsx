// 駅ブロックの名前欄（§8.9の素input）。「決定」で送信し、表示の正本はブロック状態
// Station name field (bare input per §8.9); sent on "Set", block state remains the display source of truth
import { useState } from "react";
import { Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { BlockInventoryOpen } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { PanelActionButton } from "@/shared/ui";
import styles from "../../train/style.module.css";

export default function TrainStationNameSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  const detail = data.trainStation;
  const [name, setName] = useState(detail?.name ?? "");
  const [previousName, setPreviousName] = useState(detail?.name);
  // 編集していない入力欄へ同期済みの駅名を反映する
  // Reflect synchronized station names when the input has no local edits
  if (previousName !== detail?.name) {
    setPreviousName(detail?.name);
    if (name === (previousName ?? "")) setName(detail?.name ?? "");
  }
  if (!detail) return null;
  return (
    <div className={styles.row} data-testid="train-station-name-section">
      <Text size="sm">{t(L.ui.blockInventory.stationNameLabel)}</Text>
      <input
        className={styles.nameInput}
        type="text"
        aria-label={t(L.ui.blockInventory.stationNameLabel)}
        value={name}
        placeholder={t(L.ui.blockInventory.stationNamePlaceholder)}
        onChange={(e) => setName(e.currentTarget.value)}
        data-testid="train-station-name-input"
      />
      <PanelActionButton testId="train-station-name-apply" onClick={() => { void dispatchAction("train_station.set_name", { name }); }}>
        {t(L.ui.blockInventory.stationNameApply)}
      </PanelActionButton>
    </div>
  );
}
