// 列車の車両インベントリ本文。インベントリ / 時刻表をPanelTabsで切り替える（§8.22）
// Train car inventory body; PanelTabs switches between inventory and timetable (§8.22)
import { useEffect, useState } from "react";
import { ScrollArea } from "@mantine/core";
import styles from "./style.module.css";
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
  useEffect(() => {
    if (tab === "timetable" && !data.timetable) console.warn("[TrainTimetable] timetable unavailable for train", data.identifier);
  }, [tab, data.timetable, data.identifier]);
  const tabs = [
    { value: "inventory", label: t(L.ui.blockInventory.trainTabInventory), testId: "train-tab-inventory" },
    { value: "timetable", label: t(L.ui.blockInventory.trainTabTimetable), testId: "train-tab-timetable" },
  ];
  return (
    <div className={styles.body}>
      <PanelTabs value={tab} tabs={tabs} onChange={(v) => setTab(v as Tab)} testId="train-tabs" />
      <ScrollArea className={styles.scroll} type="auto">
        {tab === "inventory" && <BlockItemGrid itemSlots={data.itemSlots} testId="train-inventory-slots" />}
        {tab === "timetable" && data.timetable && <TrainTimetableSection key={data.timetable.trainUnitId} timetable={data.timetable} />}
      </ScrollArea>
    </div>
  );
}
