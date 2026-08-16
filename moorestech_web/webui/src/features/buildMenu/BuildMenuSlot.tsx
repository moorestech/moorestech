import { PlacementTargetFace, SlotFrame } from "@/shared/ui";
import { tutorialAnchor, buildMenuEntryAnchorId } from "@/shared/tutorialAnchor";
import { useHotbarDragSource } from "@/features/hotbar";
import type { BuildMenuDisplayEntry } from "./buildMenuGrouping";

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
  const dragHandlers = useHotbarDragSource({ kind: "buildMenuEntry", id: entry.id }, onLeftClick);
  return (
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
  );
}
