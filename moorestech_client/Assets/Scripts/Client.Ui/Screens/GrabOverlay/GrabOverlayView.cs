using Client.Ui.Foundation.Input.Pointer;
using Client.Ui.Foundation.Input;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.State.Inventory.Grab;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.GrabOverlay
{
    /// <summary>
    /// 掴んだアイテムをポインタ中心に描く絵（Web GrabOverlay の写し）。表示の判断は受け取った ViewModel に従い、何も拾わない
    /// Picture of the held item centered on the pointer (port of Web GrabOverlay); follows the given view model and picks nothing
    /// </summary>
    public sealed class GrabOverlayView : VisualElement
    {
        private const string RootClass = "grab-overlay";
        private const string ShownClass = "grab-overlay--shown";

        private readonly IUiScreenPointer _pointer;
        private readonly GameContentPresentation _content;
        private readonly ItemSlot _slot = new("grab-overlay-slot");
        private bool _shown;

        public GrabOverlayView(IUiScreenPointer pointer, GameContentPresentation content)
        {
            _pointer = pointer;
            _content = content;
            name = "grab-overlay";
            AddToClassList(RootClass);
            Add(_slot);
            UiPickingIgnore.ApplyToSubtree(this);

            // 入力のイベントと同じフレームで位置を書く（PointerMoveEvent は拾わない所で届かない。D-2）
            // Write the position in the same frame as the input event (PointerMoveEvent misses non-pickable areas; D-2)
            _pointer.OnPointerMovedOnScreen.Subscribe(MoveTo);
        }

        public void Render(GrabOverlayViewModel model)
        {
            if (model is not GrabOverlayHolding holding)
            {
                _shown = false;
                EnableInClassList(ShownClass, false);
                return;
            }

            // 中身の差し替えで子が増えても拾わないよう毎回かけ直す
            // Re-apply after each content change so newly created children never pick
            _slot.SetContent(new ItemSlotContent(_content.Item(holding.ItemId), holding.Count, new ItemSlotMarks(false, false, false), null, null, null));
            UiPickingIgnore.ApplyToSubtree(this);
            var appearing = !_shown;
            _shown = true;
            EnableInClassList(ShownClass, true);

            // 出た瞬間は今のポインタへ合わせる（Web は最後の mousedown の位置へ同期）
            // On appearing, snap to the current pointer (Web syncs to the last mousedown position)
            if (appearing) MoveTo(_pointer.CurrentPointerOnScreen());
        }

        private void MoveTo(Vector2 pointerOnScreen)
        {
            // silent-ok: 隠している間と載る前は位置を持たず、出る瞬間に合わせ直す / ずれは GrabOverlayViewTest と録画が捕まえる
            if (!_shown || panel == null) return;
            var inParent = parent.WorldToLocal(_pointer.ScreenToPanel(panel, pointerOnScreen));
            style.left = inParent.x;
            style.top = inParent.y;
        }
    }
}
