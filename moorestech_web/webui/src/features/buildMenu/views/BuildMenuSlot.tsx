import type { CSSProperties } from "react";
import { HoverTooltip, PlacementTargetFace, SlotFrame } from "@/shared/ui";
import { tutorialAnchor, buildMenuEntryAnchorId } from "@/shared/tutorialAnchor";
import { useHotbarDragSource } from "@/features/hotbar";
import { L, useI18n } from "@/shared/i18n";
import { useMaterialTooltipText } from "@/shared/materialTooltipText";
import type { BuildMenuDisplayEntry } from "../logic/buildMenuGrouping";
import { insufficientItems } from "../logic/buildMenuShortage";

// 共有アイコン規則の余白。従来の私有寸法（枠 - 8px）と同じ見た目になる値
// Padding for the shared icon rule, matching the former private size (frame minus 8px)
const ICON_PAD_STYLE = { "--icon-pad": "4px" } as CSSProperties;

type Props = {
  entry: BuildMenuDisplayEntry;
  onLeftClick: () => void;
  // BPエントリのみ右クリック削除を受け付ける
  // Only blueprint entries accept right-click deletion
  onRightClick?: () => void;
  onHoverChange: (hovering: boolean) => void;
};

// アイコン有無で画像/テキストを出し分け
// 左押下はホットバーD&D共通制御を通す
// One build-menu slot, rendering an image or a text label depending on icon presence.
// The left press routes through the shared hotbar-D&D pointer control (tap = select, past-threshold drag = a hotbar-assign drag source)
export function BuildMenuSlot({ entry, onLeftClick, onRightClick, onHoverChange }: Props) {
  const { t } = useI18n();
  const materialTooltipText = useMaterialTooltipText();
  const dragHandlers = useHotbarDragSource({ kind: "buildMenuEntry", id: entry.id }, onLeftClick);

  // 不足時のみツールチップ表示
  // Show tooltip only when something is short
  const shortages = insufficientItems(entry);
  const shortageTooltip = (
    <span style={{ whiteSpace: "pre-line" }}>
      {[t(L.ui.buildMenu.materialShortageTitle)]
        .concat(shortages.map((item) => materialTooltipText(L.ui.buildMenu.materialShortageLine, item.itemId, item.count, item.held)))
        .join("\n")}
    </span>
  );

  return (
    <HoverTooltip label={shortageTooltip} disabled={shortages.length === 0}>
      <SlotFrame
        filled
        style={ICON_PAD_STYLE}
        testId={`build-menu-entry-${entry.kind}-${entry.id}`}
        onRightDown={onRightClick}
        onHoverChange={onHoverChange}
        {...dragHandlers}
        {...tutorialAnchor(buildMenuEntryAnchorId(entry.kind, entry.id))}
      >
        <PlacementTargetFace iconUrl={entry.iconUrl} displayName={entry.displayLabel} />
      </SlotFrame>
    </HoverTooltip>
  );
}
