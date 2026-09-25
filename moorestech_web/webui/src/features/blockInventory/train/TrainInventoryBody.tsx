// 列車インベントリ本文（§8.22 PanelTabsで切替）
// Train inventory body (§8.22, switched via PanelTabs)
import { useState } from "react";
import { ScrollArea, Text } from "@mantine/core";
import styles from "./style.module.css";
import { dispatchAction } from "@/bridge";
import type { BlockInventoryData } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { PanelTabs } from "@/shared/ui";
import BlockItemGrid from "../BlockItemGrid";
import TrainTimetableSection from "./TrainTimetableSection";

type TrainData = Extract<BlockInventoryData, { source: "train" }>;
type Tab = "inventory" | "timetable";

export default function TrainInventoryBody({ data }: { data: TrainData }) {
  const { t } = useI18n();
  const [tab, setTab] = useState<Tab>("inventory");
  const timetable = data.timetable;
  // 時刻表の取得は時刻表タブを選んだときだけC#へ頼む
  // Ask C# to fetch the timetable only when the timetable tab is selected
  const selectTab = (next: Tab) => {
    setTab(next);
    if (next === "timetable") void dispatchAction("train_timetable.open", {});
  };
  const tabs: { value: Tab; label: string; testId: string }[] = [
    { value: "inventory", label: t(L.ui.blockInventory.trainTabInventory), testId: "train-tab-inventory" },
    { value: "timetable", label: t(L.ui.blockInventory.trainTabTimetable), testId: "train-tab-timetable" },
  ];
  return (
    <div className={styles.body}>
      <PanelTabs value={tab} tabs={tabs} onChange={selectTab} testId="train-tabs" />
      <ScrollArea className={styles.scroll} type="auto">
        {tab === "inventory" && <BlockItemGrid itemSlots={data.itemSlots} testId="train-inventory-slots" />}
        {tab === "timetable" && timetable.status === "loading" && (
          <Text size="sm" data-testid="train-timetable-loading">{t(L.ui.blockInventory.timetableLoading)}</Text>
        )}
        {tab === "timetable" && timetable.status === "unavailable" && (
          <Text size="sm" data-testid="train-timetable-unavailable">{t(L.ui.blockInventory.timetableUnavailable)}</Text>
        )}
        {timetable.status === "ready" && (
          <div hidden={tab !== "timetable"}>
            <TrainTimetableSection key={timetable.data.trainUnitId} timetable={timetable.data} />
          </div>
        )}
      </ScrollArea>
    </div>
  );
}
