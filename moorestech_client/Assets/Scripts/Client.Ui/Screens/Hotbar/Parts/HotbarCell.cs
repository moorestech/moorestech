using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.DragDrop;
using Client.Ui.State.Hotbar;
using Client.Ui.State.Hotbar.Logic;
using Client.Ui.State.Text;
using Client.Ui.State.Toast;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Hotbar.Parts
{
    /// <summary>
    /// ホットバーの 1 枠（番号タブ＋SlotFrame＋PlacementTargetFace）。Web HotbarCell
    /// One hotbar slot (number tab + SlotFrame + PlacementTargetFace); the Web HotbarCell
    /// </summary>
    public sealed class HotbarCell : VisualElement
    {
        private const string UnresolvedClass = "hotbar-hud__cell--unresolved";

        private readonly int _slotIndex;
        private readonly IHotbarCommands _commands;
        private readonly PlacementTargetIcons _icons;
        private readonly UiCommandResultToaster _toaster;
        private readonly SlotFrame _frame;
        private readonly PlacementTargetFace _face;

        public HotbarCell(int slotIndex, IHotbarCommands commands, PlacementTargetIcons icons, UiCommandResultToaster toaster)
        {
            _slotIndex = slotIndex;
            _commands = commands;
            _icons = icons;
            _toaster = toaster;
            pickingMode = PickingMode.Ignore;
            AddToClassList("hotbar-hud__cell");
            EnableInClassList("hotbar-hud__cell--first", slotIndex == 0);

            // 枠 → タブの順に足し、タブを枠の上に描く（Web .num の z-index 2）
            // Add the frame then the tab so the tab draws above the frame (Web .num z-index 2)
            _frame = new SlotFrame($"hotbar-slot-{slotIndex}");
            _face = new PlacementTargetFace();
            Add(_frame);
            Add(new HotbarNumberTab(slotIndex));

            // 落とし先の印は枠の箱に付ける（Web の data-hotbar-slot-index は .cell。枠もタブもここへ辿る）
            // The drop mark goes on the cell box (the Web data-hotbar-slot-index is on .cell; frame and tab both walk up to it)
            UiDragDrop.MarkTarget<IHotbarDropReceiver>(this, new HotbarSlotDropMark(slotIndex, commands));
        }

        public void Render(HotbarSlotViewModel slot, bool selected)
        {
            // 割当があれば白面（未解決も外せる割当として白面）、未解決は枠ごと減光
            // An assignment shows the white face (unresolved too, as a clearable assignment); unresolved dims the whole cell
            _frame.SetState(new SlotFrameState(selected, slot.IsAssigned, false, false, false));
            EnableInClassList(UnresolvedClass, slot is HotbarUnresolvedSlot);
            RenderFace();

            // 源の値を差し替える（2 度目の MarkSource は値だけを替える）
            // Replace the source value (a second MarkSource only swaps the payload)
            UiDragDrop.MarkSource<IHotbarDropReceiver>(_frame, new HotbarSlotDragPayload(_slotIndex, slot, _commands, _toaster));

            #region Internal

            // 解けた対象だけが絵（無ければ名前）を持ち、未解決と空きは面だけ（Web slotBody）。表示名が空でも Web と同じくそのまま出す（計画 D-38）
            // Only a resolved target has a picture (or its name); unresolved and empty show just the face (Web slotBody). An empty name is shown as is, like the Web (plan D-38)
            void RenderFace()
            {
                if (slot is not HotbarAssignedSlot assigned)
                {
                    _face.RemoveFromHierarchy();
                    return;
                }

                // 絵になるかは Content が判別子で決め、面へ当てる分岐はこの枠が持つ（名前だけ・絵の欠けは名前。Web PlacementTargetFace の !iconUrl）
                // Content decides from the discriminator whether there is a picture; this cell owns the face branch (name-only or missing picture shows the name; Web PlacementTargetFace !iconUrl)
                var name = UiText.FromVerbatim(assigned.DisplayLabel);
                if (_icons.TryToUiIcon(assigned.Icon, out var icon)) _face.SetIconTarget(icon, name);
                else _face.SetNameTarget(name);
                if (_face.parent == null) _frame.Add(_face);
            }

            #endregion
        }
    }
}
