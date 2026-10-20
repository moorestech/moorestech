import { useEffect, useRef, useState } from "react";
import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { L, LocalizedShortcutHint, useI18n } from "@/shared/i18n";
import { isTextInputElement, readActiveLayer } from "@/shared/uiState";
import { BELT_WINDOW_SHORTCUT, DEFAULT_BELT_WINDOW_TICKS, beltThroughputRowDisplay, cycleBeltWindow, selectBeltWindow } from "./beltThroughputLogic";

// ベルト: 選んだ窓の搬出数と最大数・稼働率、分間換算、窓を切り替えるキーのヒント。ホストが送る全窓から1つを選んで出す
// Belt: the chosen window's exit count, maximum and utilization, the per-minute figure, and the key hint for switching windows; one of the windows the host sends is shown
export default function BeltThroughputSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  const [windowTicks, setWindowTicks] = useState(DEFAULT_BELT_WINDOW_TICKS);

  // リスナーは1回だけ張り、最新の窓一覧はrefで読む(useGameLayerKeydownと同じ形。層だけblockInventory)
  // Attach the listener once and read the latest window list through a ref (same shape as useGameLayerKeydown, for the blockInventory layer)
  const windowsRef = useRef(data.beltThroughput?.windows ?? []);
  windowsRef.current = data.beltThroughput?.windows ?? [];
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (isTextInputElement(document.activeElement)) return;
      if (readActiveLayer() !== "blockInventory") return;
      const step = event.key === "ArrowLeft" ? -1 : event.key === "ArrowRight" ? 1 : 0;
      if (step === 0) return;
      setWindowTicks((current) => cycleBeltWindow(windowsRef.current, current, step));
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  if (!data.beltThroughput) return null;
  const selected = selectBeltWindow(data.beltThroughput.windows, windowTicks);
  if (!selected) return null;
  const row = beltThroughputRowDisplay(selected);
  return (
    <Stack gap={2} data-testid="belt-throughput-section">
      <Text size="sm" c="var(--text-default)" data-testid="belt-throughput-window">
        {t(row.key, row.params)}
      </Text>
      {row.itemsPerMinute !== null ? (
        <Text size="sm" c="var(--text-default)" data-testid="belt-throughput-per-minute">
          {t(L.ui.blockInventory.itemsPerMinute, { itemsPerMinute: row.itemsPerMinute })}
        </Text>
      ) : null}
      <Text className="keyHintText" data-testid="belt-throughput-window-hint">
        <LocalizedShortcutHint layout="inline" shortcut={BELT_WINDOW_SHORTCUT} translationKey={L.ui.blockInventory.beltThroughputWindowHint} />
      </Text>
    </Stack>
  );
}
