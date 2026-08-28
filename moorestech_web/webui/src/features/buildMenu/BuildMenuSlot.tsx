import { HoverTooltip, PlacementTargetFace, SlotFrame } from "@/shared/ui";
import { tutorialAnchor, buildMenuEntryAnchorId } from "@/shared/tutorialAnchor";
import { useHotbarDragSource } from "@/features/hotbar";
import { L, useI18n } from "@/shared/i18n";
import { useMaterialTooltipText } from "@/shared/materialTooltipText";
import { shortageItemsOf } from "./logic/buildMenuShortage";
import type { BuildMenuDisplayEntry } from "./logic/buildMenuGrouping";

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
  const shortages = shortageItemsOf(entry);
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
