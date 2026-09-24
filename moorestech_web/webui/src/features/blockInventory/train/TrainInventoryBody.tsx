// 列車インベントリ本文（§8.22 PanelTabsで切替）
// Train inventory body (§8.22, switched via PanelTabs)
import { useEffect, useState } from "react";
import { ScrollArea, Text } from "@mantine/core";
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
  const tabs: { value: Tab; label: string; testId: string }[] = [
    { value: "inventory", label: t(L.ui.blockInventory.trainTabInventory), testId: "train-tab-inventory" },
    { value: "timetable", label: t(L.ui.blockInventory.trainTabTimetable), testId: "train-tab-timetable" },
  ];
  return (
    <div className={styles.body}>
      <PanelTabs value={tab} tabs={tabs} onChange={setTab} testId="train-tabs" />
      <ScrollArea className={styles.scroll} type="auto">
        {tab === "inventory" && <BlockItemGrid itemSlots={data.itemSlots} testId="train-inventory-slots" />}
        {tab === "timetable" && !data.timetable && (
          <Text size="sm" data-testid="train-timetable-unavailable">{t(L.ui.blockInventory.timetableUnavailable)}</Text>
        )}
        {data.timetable && (
          <div hidden={tab !== "timetable"}>
            <TrainTimetableSection key={data.timetable.trainUnitId} timetable={data.timetable} />
          </div>
        )}
      </ScrollArea>
    </div>
  );
}
