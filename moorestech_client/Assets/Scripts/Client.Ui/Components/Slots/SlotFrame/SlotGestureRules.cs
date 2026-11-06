using System;
using System.Collections.Generic;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// ボタンと押下ビットから身振りを決める（Web の useSlotMouse）
    /// Decides gestures from button and held bits (the Web useSlotMouse)
    /// </summary>
    internal static class SlotGestureRules
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
            // 左の進入を先、右を後に出す（Web の順）
            // Emit left enter first, right second (the Web order)
            var left = (pressedButtons & LeftHeldBit) != 0;
            var right = (pressedButtons & RightHeldBit) != 0;
            if (left && right) return BothEnters;
            if (left) return LeftEnterOnly;
            if (right) return RightEnterOnly;
            return Array.Empty<SlotGesture>();
        }

        // 追加押下は PointerMove で届き、button の押下ビットが立っているときだけ押下になる
        // An extra press arrives as a PointerMove and counts only when the button's pressed bit is set
        public static bool IsPressTransition(int button, int pressedButtons)
        {
            return (pressedButtons & (1 << button)) != 0;
        }

        // Web の dblclick は左ボタンの 2 回目のクリックで 1 回出る
        // The Web dblclick fires once on the second left click
        public static bool IsDoubleClick(int button, int clickCount)
        {
            return button == LeftButton && clickCount == DoubleClickCount;
        }
    }
}
