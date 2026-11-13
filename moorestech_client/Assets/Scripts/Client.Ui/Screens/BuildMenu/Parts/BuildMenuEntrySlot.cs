using System;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.DragDrop;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.Hotbar;
using Client.Ui.State.Text;
using UniRx;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// エントリ 1 マス（Web BuildMenuSlot の写し）。同じ id のまま Render で中身だけ差し替える
    /// One entry slot (port of the Web BuildMenuSlot); Render swaps contents while the id stays the same
    /// </summary>
    public sealed class BuildMenuEntrySlot
    {
        private const string EntryClass = "build-menu__entry";

        private readonly GameContentPresentation _content;
        private readonly PlacementTargetIcons _icons;
        private readonly PlacementTargetFace _face = new();
        private readonly Guid _targetId;

        public SlotFrame Frame { get; }

        public BuildMenuEntrySlot(BuildMenuEntryViewModel entry, GameContentPresentation content, PlacementTargetIcons icons, IBuildMenuEntryHandler handler, IHotbarCommands hotbarCommands)
        {
            _content = content;
            _icons = icons;
            _targetId = entry.Catalog.TargetId;
            Frame = new SlotFrame(TestIdOf(entry));
            Frame.AddToClassList(EntryClass);
            Frame.SetState(new SlotFrameState(false, true, false, false, false));
            Frame.Add(_face);
            TutorialAnchor.Mark(Frame, AnchorIds.BuildMenuEntry(entry.KindName, _targetId.ToString("D")));

            // 左はドラッグ源（タップ＝選択）、右は BP だけ削除、ホバーは詳細の sticky
            // Left is the drag source (tap selects), right deletes blueprints only, hover feeds the sticky detail
            UiDragDrop.MarkSource(Frame, new BuildMenuEntryDragPayload(_targetId, handler, hotbarCommands));
            var isBlueprint = entry.CanDeleteBlueprint;
            Frame.OnSlotGestured.Subscribe(gesture =>
            {
                if (gesture.Kind == SlotGestureKind.HoverEnter) handler.OnEntryHovered(_targetId);
                if (gesture.Kind == SlotGestureKind.RightDown && isBlueprint) handler.OnBlueprintDeleteRequested(_targetId);
            });
            Render(entry);
        }

        private static string TestIdOf(BuildMenuEntryViewModel entry)
        {
            return $"build-menu-entry-{entry.KindName}-{entry.Catalog.TargetId:D}";
        }

        public void Render(BuildMenuEntryViewModel entry)
        {
            // 絵があれば絵、無ければ名前（Web PlacementTargetFace の iconUrl の有無）
            // A picture when there is one, otherwise the name (the Web PlacementTargetFace iconUrl check)
            var name = UiText.FromVerbatim(entry.Catalog.DisplayLabel);
            if (_icons.TryToUiIcon(entry.Icon, out var icon)) _face.SetIconTarget(icon, name);
            else _face.SetNameTarget(name);

            // 不足があるときだけツールチップの印を付ける。枠は赤くしない（Web と同じ）
            // Mark the tooltip only when something is short; the frame never turns red (same as the Web)
            if (BuildMenuShortageTooltipText.TryCreate(entry, _content, out var tooltip)) HoverTooltipMark.Mark(Frame, tooltip);
            else HoverTooltipMark.Unmark(Frame);
        }
    }
}
