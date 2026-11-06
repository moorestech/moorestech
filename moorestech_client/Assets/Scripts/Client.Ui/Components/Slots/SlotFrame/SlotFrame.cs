using System;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// 1 マスの枠・ベベル・面と 5 状態、マウスの身振り（Web の SlotFrame と useSlotMouse）
    /// One cell's frame, bevel, face, five states and mouse gestures (the Web SlotFrame and useSlotMouse)
    /// </summary>
    public sealed class SlotFrame : VisualElement
    {
        private readonly Subject<SlotGesture> _slotGestured = new();
        private readonly VisualElement _content;

        public IObservable<SlotGesture> OnSlotGestured => _slotGestured;
        public override VisualElement contentContainer => _content;

        public SlotFrame(string testid)
        {
            name = testid;
            AddToClassList("slot-frame");

            // 下端の帯 → リング → 中身の順に重ね、Web の背景・box-shadow・子の重なりにそろえる
            // Stack the bottom trim, ring and content in this order to match the Web background, box-shadow and child layering
            var selectedTrim = new VisualElement { pickingMode = PickingMode.Ignore };
            selectedTrim.AddToClassList("slot-frame__selected-trim");
            hierarchy.Add(selectedTrim);
            hierarchy.Add(new SlotFrameRing());
            _content = new VisualElement { pickingMode = PickingMode.Ignore };
            _content.AddToClassList("slot-frame__content");
            hierarchy.Add(_content);

            // 押下・進入・離脱・クリックを身振りへ訳す
            // Translate press, enter, leave and click into gestures
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            RegisterCallback<PointerLeaveEvent>(_ => _slotGestured.OnNext(new SlotGesture(SlotGestureKind.HoverLeave, false)));
            RegisterCallback<ClickEvent>(OnClick);
        }

        public void SetState(SlotFrameState state)
        {
            EnableInClassList("slot-frame--selected", state.Selected);
            EnableInClassList("slot-frame--filled", state.Filled);
            EnableInClassList("slot-frame--catalog", state.Catalog);
            EnableInClassList("slot-frame--insufficient", state.Insufficient);
            EnableInClassList("slot-frame--ghost", state.Ghost);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (SlotGestureRules.TryFromPointerDown(evt.button, evt.shiftKey, out var gesture)) _slotGestured.OnNext(gesture);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            // 別ボタンを押したまま追加で押すと UITK は PointerDownEvent でなく PointerMoveEvent で届ける（Web の onMouseDown は毎回出る）
            // While another button is held, an extra press arrives as a PointerMoveEvent, not a PointerDownEvent (Web onMouseDown fires every time)
            if (evt.button < 0 || (evt.pressedButtons & (1 << evt.button)) == 0) return;
            if (SlotGestureRules.TryFromPointerDown(evt.button, evt.shiftKey, out var gesture)) _slotGestured.OnNext(gesture);
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            // 押したままの進入を先に、ホバーの開始を後に出す（Web の onMouseEnter の順）
            // Emit held-button enters first and the hover start after (the Web onMouseEnter order)
            foreach (var gesture in SlotGestureRules.FromPointerEnter(evt.pressedButtons)) _slotGestured.OnNext(gesture);
            _slotGestured.OnNext(new SlotGesture(SlotGestureKind.HoverEnter, false));
        }

        private void OnClick(ClickEvent evt)
        {
            // ボタンの番号の意味は SlotGestureRules だけが持つ
            // Only SlotGestureRules knows what the button numbers mean
            if (SlotGestureRules.IsDoubleClick(evt.button, evt.clickCount)) _slotGestured.OnNext(new SlotGesture(SlotGestureKind.DoubleClick, false));
        }
    }
}
