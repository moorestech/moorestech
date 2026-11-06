using System;
using System.Collections.Generic;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// ボタンの番号と押下中のビットから身振りを決める（Web の useSlotMouse）
    /// Decides gestures from the button index and held-button bits (the Web useSlotMouse)
    /// </summary>
    public static class SlotGestureRules
    {
        // UITK の button は 0 左・1 右、pressedButtons は bit0 左・bit1 右（Web の button 0/2・buttons 1/2 と同じ意味）
        // UITK button is 0 left / 1 right and pressedButtons is bit0 left / bit1 right (same meaning as Web button 0/2 and buttons 1/2)
        private const int LeftButton = 0;
        private const int RightButton = 1;
        private const int LeftHeldBit = 1;
        private const int RightHeldBit = 2;
        private const int DoubleClickCount = 2;

        private static readonly SlotGesture[] LeftEnterOnly = { new(SlotGestureKind.LeftEnter, false) };
        private static readonly SlotGesture[] RightEnterOnly = { new(SlotGestureKind.RightEnter, false) };
        private static readonly SlotGesture[] BothEnters = { new(SlotGestureKind.LeftEnter, false), new(SlotGestureKind.RightEnter, false) };

        public static bool TryFromPointerDown(int button, bool shift, out SlotGesture gesture)
        {
            switch (button)
            {
                case LeftButton:
                    gesture = new SlotGesture(SlotGestureKind.LeftDown, shift);
                    return true;
                case RightButton:
                    gesture = new SlotGesture(SlotGestureKind.RightDown, false);
                    return true;
            }

            // silent-ok: 中ボタン等は Web でも身振りにならない（契約どおりの無反応） / 契約は SlotGestureRulesTest が固定し、変えれば開発者がテストで気づく
            // silent-ok: middle and other buttons are no gesture on the Web either (the contract) / SlotGestureRulesTest pins it, so a change surfaces in tests
            gesture = default;
            return false;
        }

        public static IReadOnlyList<SlotGesture> FromPointerEnter(int pressedButtons)
        {
            // 左の進入を先に、右を後に出す（Web の onMouseEnter の判定順）
            // Emit the left enter first and the right second (the Web onMouseEnter order)
            var left = (pressedButtons & LeftHeldBit) != 0;
            var right = (pressedButtons & RightHeldBit) != 0;
            if (left && right) return BothEnters;
            if (left) return LeftEnterOnly;
            if (right) return RightEnterOnly;
            return Array.Empty<SlotGesture>();
        }

        // Web の dblclick は左ボタンの 2 回目のクリックで 1 回出る
        // The Web dblclick fires once on the second left click
        public static bool IsDoubleClick(int button, int clickCount)
        {
            return button == LeftButton && clickCount == DoubleClickCount;
        }
    }
}
