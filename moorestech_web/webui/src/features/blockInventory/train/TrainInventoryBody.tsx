// 列車インベントリ本文（§8.22 PanelTabsで切替）
// Train inventory body (§8.22, switched via PanelTabs)
import { useEffect, useRef, useState } from "react";
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
  // 時刻表タブを選んだ時と、タブ上で「読み込み中」を受けるたびに取得をC#へ頼む（重複はC#側で畳む）
  // Ask C# to fetch on tab select and on every "loading" publish while on the tab (C# folds duplicates)
  // 同一車両の閉→開がデバウンスで畳まれ再マウントされなくても、C#がリセットした取得はここで再開する
  // Even if a same-car close/open is debounced into one publish without a remount, a reset fetch resumes here
  const loadingTimetable = timetable.kind === "loading" ? timetable : null;
  const previousTab = useRef<Tab>(tab);
  useEffect(() => {
    const selectedNow = previousTab.current !== "timetable";
    previousTab.current = tab;
    if (tab === "timetable" && (selectedNow || loadingTimetable)) void dispatchAction("train_timetable.open", {});
  }, [tab, loadingTimetable]);
  const tabs: { value: Tab; label: string; testId: string }[] = [
    { value: "inventory", label: t(L.ui.blockInventory.trainTabInventory), testId: "train-tab-inventory" },
    { value: "timetable", label: t(L.ui.blockInventory.trainTabTimetable), testId: "train-tab-timetable" },
  ];
  return (
    <div className={styles.body}>
      <PanelTabs value={tab} tabs={tabs} onChange={setTab} testId="train-tabs" />
      <ScrollArea className={styles.scroll} type="auto">
        {tab === "inventory" && <BlockItemGrid itemSlots={data.itemSlots} testId="train-inventory-slots" />}
        {tab === "timetable" && timetable.kind === "loading" && (
          <Text size="sm" data-testid="train-timetable-loading">{t(L.ui.blockInventory.timetableLoading)}</Text>
        )}
        {tab === "timetable" && timetable.kind === "unavailable" && (
          <Text size="sm" data-testid="train-timetable-unavailable">{t(L.ui.blockInventory.timetableUnavailable)}</Text>
        )}
        {timetable.kind === "ready" && (
          <div hidden={tab !== "timetable"}>
            <TrainTimetableSection key={timetable.data.trainUnitId} timetable={timetable.data} />
          </div>
        )}
      </ScrollArea>
    </div>
  );
}
