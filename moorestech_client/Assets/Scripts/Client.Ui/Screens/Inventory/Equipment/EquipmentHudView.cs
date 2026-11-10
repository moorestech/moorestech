using System.Collections.Generic;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Panel;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Inventory.Equipment
{
    /// <summary>
    /// 装備 HUD。選択はホイール、クリックは移動
    /// Equipment HUD; wheel selects, clicks move items
    /// </summary>
    public sealed class EquipmentHudView : VisualElement
    {
        private readonly PlayerSlotGestureRouter _gestures;
        private readonly GameContentPresentation _content;
        private readonly List<ItemSlot> _slots = new();
        private bool _clicksAccepted;

        public EquipmentHudView(PlayerSlotGestureRouter gestures, GameContentPresentation content)
        {
            _gestures = gestures;
            _content = content;
            name = "equipment-slots";
            AddToClassList("equipment-hud");

            // HUD 自体のアンカーは常に出す。列は実 UI として拾う（Web pointer-events: auto）
            // The HUD's own anchor is always present; the column picks as real UI (Web pointer-events: auto)
            TutorialAnchor.Mark(this, AnchorIds.EquipmentHud);
            pickingMode = PickingMode.Position;

            // 実 UI だがスクロールを持たないゲーム操作の場なので、ホイールだけ装備へ素通す（Web data-wheel-passthrough）
            // Real UI without scrolling that belongs to the game, so only the wheel passes through to equipment (Web data-wheel-passthrough)
            WheelPassthroughMark.Mark(this);
            SetShown(false);
        }

        public void SetClicksAccepted(bool accepted)
        {
            _clicksAccepted = accepted;
        }

        public void SetShown(bool shown)
        {
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Render(PlayerInventoryViewModel viewModel)
        {
            // 購読は Presenter。ここは描くだけ
            // The presenter subscribes; this only draws
            if (_slots.Count != viewModel.EquipmentSlots.Count) Rebuild(viewModel.EquipmentSlots.Count);
            for (var i = 0; i < _slots.Count; i++)
            {
                // 選択枠はホイールで動くので、選択アンカーもその枠へ移す
                // The selected slot moves with the wheel, so the selection anchor moves with it
                var selected = i == viewModel.SelectedEquipment;
                _slots[i].SetContent(SlotItemContent.From(_content, viewModel.EquipmentSlots[i], selected));
                _slots[i].EnableInClassList(AnchorIds.EquipmentSelectedSlot, selected);
            }

            #region Internal

            void Rebuild(int count)
            {
                foreach (var slot in _slots) slot.RemoveFromHierarchy();
                _slots.Clear();
                for (var i = 0; i < count; i++)
                {
                    var index = i;
                    var slot = new ItemSlot($"equipment-slot-{index}");
                    slot.AddToClassList("equipment-hud__slot");
                    if (0 < index) slot.AddToClassList("equipment-hud__slot--after-first");
                    TutorialAnchor.Mark(slot, AnchorIds.EquipmentSlot(index));
                    slot.OnSlotGestured.Subscribe(gesture => OnGestured(index, gesture));
                    Add(slot);
                    _slots.Add(slot);
                }
            }

            void OnGestured(int index, SlotGesture gesture)
            {
                // silent-ok: 掴めない画面では装備の枠はクリックを受けない（Web はハンドラごと付けない） / EquipmentHudViewTest が見る
                if (!_clicksAccepted) return;
                _gestures.Route(PlayerSlotRef.Equipment(index), gesture);
            }

            #endregion
        }
    }
}
