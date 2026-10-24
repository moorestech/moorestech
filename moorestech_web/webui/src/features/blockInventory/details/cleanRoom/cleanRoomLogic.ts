import type { CleanRoomDetailData, CleanRoomSummaryData } from "@/bridge";
import { L, type InterpolationValues, type TranslationKey } from "@/shared/i18n";

export type CleanRoomLine = { key: TranslationKey; params?: InterpolationValues };
export type CleanRoomDisplay = {
  headline: CleanRoomLine;
  headlineInsufficient: boolean;
  air: CleanRoomLine | null;
  filters: CleanRoomLine[];
  noFilter: CleanRoomLine | null;
  haltReason: CleanRoomLine | null;
};

// 部屋欄の各行のキーと引数を作る。赤（不足）は「稼働を妨げている」行だけに付ける
// Builds keys and params for each room row; red (insufficient) only marks rows that block operation
export function cleanRoomDisplay(cleanRoom: CleanRoomDetailData): CleanRoomDisplay {
  const haltReason = haltReasonLine(cleanRoom.haltReason);
  switch (cleanRoom.kind) {
    case "enclosed":
      return { ...enclosedRows(cleanRoom.room), haltReason };
    case "openings":
      return headlineOnly({ key: L.ui.blockInventory.cleanRoomNotSealed, params: { count: String(cleanRoom.openingCount) } }, true, haltReason);
    case "noEnclosure":
      return headlineOnly({ key: L.ui.blockInventory.cleanRoomNoEnclosure }, true, haltReason);
    case "tooLarge":
      return headlineOnly({ key: L.ui.blockInventory.cleanRoomTooLarge, params: { max: String(cleanRoom.maxRoomVolume) } }, true, haltReason);
    case "detecting":
      // 検出待ちは数tickで解消するので赤にしない
      // Pending detection resolves within ticks, so it is not red
      return headlineOnly({ key: L.ui.blockInventory.cleanRoomDetecting }, false, haltReason);
  }
}

function enclosedRows(room: CleanRoomSummaryData): Omit<CleanRoomDisplay, "haltReason"> {
  const filters = room.airFilters.map((filter, index): CleanRoomLine => ({
    key: L.ui.blockInventory.cleanRoomFilter,
    params: {
      index: String(index + 1),
      power: String(Math.round(filter.powerRatio * 100)),
      count: String(filter.filterItemCount),
      remaining: String(Math.round(filter.filterRemainingRatio * 100)),
    },
  }));
  return {
    headline: roomHeadline(room),
    headlineInsufficient: room.headline === "classOut",
    air: { key: L.ui.blockInventory.cleanRoomAir, params: { concentration: room.concentration.toFixed(3), airChangeRate: room.airChangeRate.toFixed(3) } },
    filters,
    noFilter: filters.length === 0 ? { key: L.ui.blockInventory.cleanRoomNoFilter } : null,
  };
}

// 見出しはサーバーが決めた見出しの種別から選ぶ（Out の判定をここで持たない）
// Pick the headline from the server-decided headline kind; no Out decision is made here
function roomHeadline(room: CleanRoomSummaryData): CleanRoomLine {
  switch (room.headline) {
    case "class":
      return { key: L.ui.blockInventory.cleanRoomClass, params: { className: room.className } };
    case "classOut":
      return { key: L.ui.blockInventory.cleanRoomClassOut };
  }
}

function headlineOnly(headline: CleanRoomLine, insufficient: boolean, haltReason: CleanRoomLine | null): CleanRoomDisplay {
  return { headline, headlineInsufficient: insufficient, air: null, filters: [], noFilter: null, haltReason };
}

function haltReasonLine(haltReason: CleanRoomDetailData["haltReason"]): CleanRoomLine | null {
  if (haltReason === "notEnclosed") return { key: L.ui.blockInventory.cleanRoomHaltNotEnclosed };
  if (haltReason === "classOut") return { key: L.ui.blockInventory.cleanRoomHaltClassOut };
  return null;
}
