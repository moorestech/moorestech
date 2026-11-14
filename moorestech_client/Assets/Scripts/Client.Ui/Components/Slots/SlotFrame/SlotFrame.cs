using System;
using Client.Ui.Foundation.Filter;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// 1 マスの枠・状態・身振り（Web の SlotFrame）
    /// One cell frame, states and gestures (the Web SlotFrame)
    /// </summary>
    public sealed class SlotFrame : VisualElement
    {
        private readonly Subject<SlotGesture> _slotGestured = new();
        private readonly VisualElement _content;
        // 進入時に押下ビットが 0 だった間 true。最初に押下ビットつきで届く移動を進入として判定する
        // True while the enter arrived with no held bits; the first move carrying held bits is judged as the enter
        private bool _enterJudgementPending;

        public IObservable<SlotGesture> OnSlotGestured => _slotGestured;
        public override VisualElement contentContainer => _content;

        public SlotFrame(string testid)
        {
            name = testid;
            AddToClassList("slot-frame");

            // 面・帯・リング・中身を 1 つの群に入れ、不足の淡さを Web と同じく群に掛ける（A-222・A-226）。当たり判定は枠自身の箱
            // Face, trim, ring and content share one group so insufficient fading applies to the group as on the Web (A-222, A-226); hit testing stays on the frame box
            var group = new PixelFilterGroup(PixelFilterGroupFlow.Fill);
            hierarchy.Add(group);

            // 面 → 帯 → リング → 中身の順に重ねる（Web の重なり）
            // Stack face, trim, ring, content in order (the Web layering)
            var face = new VisualElement { pickingMode = PickingMode.Ignore };
            face.AddToClassList("slot-frame__face");
            group.Add(face);
            var selectedTrim = new VisualElement { pickingMode = PickingMode.Ignore };
            selectedTrim.AddToClassList("slot-frame__selected-trim");
            group.Add(selectedTrim);
            group.Add(new SlotFrameRing());
            _content = new VisualElement { pickingMode = PickingMode.Ignore };
            _content.AddToClassList("slot-frame__content");
            group.Add(_content);

            // 押下・進入・離脱・クリックを身振りへ訳す
            // Translate press, enter, leave and click into gestures
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
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
            // 枠の中で押したのは進入ではない
            // A press inside the frame is not an enter
            _enterJudgementPending = false;
            if (SlotGestureRules.TryFromPointerDown(evt.button, evt.shiftKey, out var gesture)) _slotGestured.OnNext(gesture);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            // 進入時に 0 だった押下ビットが移動で立ったら、それを進入として 1 回だけ判定する（button<0 の純粋な移動のみ。追加押下は下の経路）
            // If held bits appear on a move after a zero-bit enter, judge it as the enter once (pure moves with button<0 only; extra presses take the path below)
            if (_enterJudgementPending && evt.button < 0 && evt.pressedButtons != 0)
            {
                _enterJudgementPending = false;
                foreach (var enter in SlotGestureRules.FromPointerEnter(evt.pressedButtons)) _slotGestured.OnNext(enter);
                return;
            }
            if (0 <= evt.button) _enterJudgementPending = false;

            // 別ボタンを押したまま追加で押すと UITK は PointerDownEvent でなく PointerMoveEvent で届ける（Web の onMouseDown は毎回出る）
            // While another button is held, an extra press arrives as a PointerMoveEvent, not a PointerDownEvent (Web onMouseDown fires every time)
            if (evt.button < 0 || !SlotGestureRules.IsPressTransition(evt.button, evt.pressedButtons)) return;
            if (SlotGestureRules.TryFromPointerDown(evt.button, evt.shiftKey, out var gesture)) _slotGestured.OnNext(gesture);
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            // 進入を先に、ホバー開始を後に出す（Web の順）
            // Emit enters first, hover start after (the Web order)
            // 押下ビットが 0 なら判定を最初の移動へ回す（PanelSimulator では進入時 0 だった）
            // With no held bits, defer the judgement to the first move (it was 0 on enter in PanelSimulator)
            _enterJudgementPending = evt.pressedButtons == 0;
            foreach (var gesture in SlotGestureRules.FromPointerEnter(evt.pressedButtons)) _slotGestured.OnNext(gesture);
            _slotGestured.OnNext(new SlotGesture(SlotGestureKind.HoverEnter, false));
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            _enterJudgementPending = false;
            _slotGestured.OnNext(new SlotGesture(SlotGestureKind.HoverLeave, false));
        }

        private void OnClick(ClickEvent evt)
        {
            // 番号の意味は SlotGestureRules に集約
            // Button meanings live only in SlotGestureRules
            if (SlotGestureRules.IsDoubleClick(evt.button, evt.clickCount)) _slotGestured.OnNext(new SlotGesture(SlotGestureKind.DoubleClick, false));
        }
    }
}
