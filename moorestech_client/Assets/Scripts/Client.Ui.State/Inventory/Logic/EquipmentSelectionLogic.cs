using System;

namespace Client.Ui.State.Inventory.Logic
{
    /// <summary>
    /// ホイール量の単位（Web WheelEvent.deltaMode の 0・1・2）
    /// Unit of a wheel delta (Web WheelEvent.deltaMode 0, 1, 2)
    /// </summary>
    public enum WheelDeltaUnit
    {
        Pixel,
        Line,
        Page,
    }

    /// <summary>
    /// ホイール累積の結果。進める段数は -1・0・1 のどれか
    /// Result of wheel accumulation; steps is one of -1, 0 or 1
    /// </summary>
    public readonly struct WheelStepResult
    {
        public readonly double Remainder;
        public readonly int Steps;

        public WheelStepResult(double remainder, int steps)
        {
            Remainder = remainder;
            Steps = steps;
        }
    }

    /// <summary>
    /// 装備選択の純ロジック。実スロット間の循環とホイール量の累積（Web equipmentLogic の写し）
    /// Pure equipment-selection logic: cycling across real slots and wheel accumulation (port of Web equipmentLogic)
    /// </summary>
    public static class EquipmentSelectionLogic
    {
        // 0..slotCount-1 を周期 slotCount で循環させる（負の delta は同じ環を逆走）
        // Cycle 0..slotCount-1 with period slotCount; a negative delta walks the same ring backwards
        public static int CycleEquipment(int current, int delta, int slotCount)
        {
            // 装備枠 0 は循環先が無い。呼び出し側が枠 0 で呼ぶのは配線の誤りなので止める
            // Zero equipment slots have nowhere to cycle; calling with zero is a wiring bug, so stop
            if (slotCount <= 0) throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount, "装備枠が 0 のときは循環できない");
            return ((current + delta) % slotCount + slotCount) % slotCount;
        }

        // 累積して閾値を越えたら段数によらず 1 段だけ進める（スクロール加速で 1 ノッチが多段にならない）
        // Accumulate and advance exactly one step past the threshold, so acceleration cannot turn one notch into many
        public static WheelStepResult AccumulateWheelSteps(double remainder, double delta, WheelDeltaUnit unit)
        {
            var normalized = delta / NotchSizeOf(unit);

            // 逆回転では溜まっていた端数を捨て、前の回転の残りで即発火させない
            // On a reversal drop the opposite leftover so the previous rotation cannot fire immediately
            var carried = remainder * normalized < 0 ? 0 : remainder;
            var total = carried + normalized;
            if (1 <= total) return new WheelStepResult(0, 1);
            if (total <= -1) return new WheelStepResult(0, -1);
            return new WheelStepResult(total, 0);
        }

        // 単位ごとの 1 ノッチの量。OS・デバイスで桁が変わるので単位側で正規化する
        // One notch per unit; magnitudes vary by OS and device, so normalize per unit
        private static double NotchSizeOf(WheelDeltaUnit unit)
        {
            switch (unit)
            {
                case WheelDeltaUnit.Pixel: return 100;
                case WheelDeltaUnit.Line: return 3;
                case WheelDeltaUnit.Page: return 1;
                default: throw new ArgumentOutOfRangeException(nameof(unit), unit, "未知のホイール単位");
            }
        }
    }
}
