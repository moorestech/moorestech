import type { BeltThroughputWindowData } from "@/bridge";
import { L, type InterpolationValues, type TranslationKey } from "@/shared/i18n";

// 既定の窓は180tick(9秒)。バニラ3速度の搬出周期(20・9・3tick)の最小公倍数で、満載時の個数が位相でぶれない最短の窓(ユーザー裁定 2026-10-20)
// The default window is 180 ticks (9 s): the least common multiple of the three vanilla output periods (20, 9, 3 ticks), the shortest window whose full-belt count never wobbles with phase (user ruling 2026-10-20)
export const DEFAULT_BELT_WINDOW_TICKS = 180;
// 棒グラフは1秒平均を9本(ユーザー裁定 2026-10-20)
// The bar chart shows nine one-second averages (user ruling 2026-10-20)
export const RECENT_SECOND_BAR_COUNT = 9;
const TICKS_PER_SECOND = 20;
const TICKS_PER_MINUTE = TICKS_PER_SECOND * 60;

export type BeltThroughputRowDisplay = {
  key: TranslationKey;
  params: InterpolationValues;
  // 計測中はnull
  // Null while still measuring
  itemsPerMinute: string | null;
};

// 選んだ長さの窓。ホストの一覧に無ければ先頭の窓へ落とす
// The window of the chosen length, falling back to the first one when the host's list lacks it
export function selectBeltWindow(windows: readonly BeltThroughputWindowData[], ticks: number): BeltThroughputWindowData | undefined {
  return windows.find((window) => window.ticks === ticks) ?? windows[0];
}

// 一覧の中で1つ隣の窓へ移る。端では止まり、一覧に無い長さからは先頭扱いで動く
// Move to the neighbouring window in the list; stops at the ends, and an unlisted length counts as the first
export function cycleBeltWindow(windows: readonly BeltThroughputWindowData[], currentTicks: number, step: -1 | 1): number {
  if (windows.length === 0) return currentTicks;
  const currentIndex = Math.max(0, windows.findIndex((window) => window.ticks === currentTicks));
  const nextIndex = Math.min(windows.length - 1, Math.max(0, currentIndex + step));
  return windows[nextIndex].ticks;
}

export function beltThroughputRowDisplay(window: BeltThroughputWindowData): BeltThroughputRowDisplay {
  const seconds = formatCount(window.ticks / TICKS_PER_SECOND);
  if (!window.measured) {
    return { key: L.ui.blockInventory.beltThroughputMeasuring, params: { seconds }, itemsPerMinute: null };
  }

  // 停止中(最大0)は割合を出せないので0%にする
  // A stopped belt (max 0) has no ratio, so it shows 0%
  const percent = window.maxCount > 0 ? Math.round((window.exitCount / window.maxCount) * 100) : 0;
  return {
    key: L.ui.blockInventory.beltThroughputWindow,
    params: { seconds, count: window.exitCount, max: formatCount(window.maxCount), percent },
    itemsPerMinute: ((window.exitCount * TICKS_PER_MINUTE) / window.ticks).toFixed(1),
  };
}

// 古い順の1秒ごとの搬出数を、最大数に対する高さ(0..1)の棒barCount本に写す。まだ無い秒は先頭側をnullで埋める
// Maps the oldest-first per-second counts onto barCount bar heights (0..1) against the maximum; seconds not yet available are null at the front
export function beltRecentSecondBars(counts: readonly number[], maxCountPerSecond: number, barCount: number): (number | null)[] {
  const recent = counts.slice(-barCount);
  const bars: (number | null)[] = Array.from({ length: barCount - recent.length }, () => null);
  for (const count of recent) {
    // 停止中(最大0)は0。整数の粗さで最大を超えた秒は満杯に丸める
    // A stopped belt (max 0) gives 0; a second that exceeds the maximum through integer coarseness rounds to full
    bars.push(maxCountPerSecond > 0 ? Math.min(1, count / maxCountPerSecond) : 0);
  }
  return bars;
}

// 整数はそのまま、端数は小数1桁(90tick=4.5秒、速度によっては最大数も端数になる)
// Whole numbers as they are, fractions to one decimal (90 ticks = 4.5 s; some speeds also give a fractional maximum)
function formatCount(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
}
