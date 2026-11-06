using Client.Localization;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.Detail
{
    // 歯車ブロックの現在値。役割ごとに sealed 派生で持ち、発電機は基準 RPM を型として持たない（ADR 0056・判断記録 D-5）
    // A gear block's current values as sealed per-role subtypes; generators have no base RPM at the type level (ADR 0056, D-5)
    public abstract class GearDetail
    {
        public double CurrentRpm { get; }
        public double CurrentTorque { get; }

        private protected GearDetail(double currentRpm, double currentTorque)
        {
            CurrentRpm = currentRpm;
            CurrentTorque = currentTorque;
        }

        public static GearDetail Consumer(double currentRpm, double currentTorque, double baseRpm)
        {
            return new ConsumerGearDetail(currentRpm, currentTorque, baseRpm);
        }

        public static GearDetail Generator(double currentRpm, double currentTorque)
        {
            return new GeneratorGearDetail(currentRpm, currentTorque);
        }
    }

    public sealed class ConsumerGearDetail : GearDetail
    {
        public double BaseRpm { get; }

        internal ConsumerGearDetail(double currentRpm, double currentTorque, double baseRpm) : base(currentRpm, currentTorque)
        {
            BaseRpm = baseRpm;
        }
    }

    public sealed class GeneratorGearDetail : GearDetail
    {
        internal GeneratorGearDetail(double currentRpm, double currentTorque) : base(currentRpm, currentTorque)
        {
        }
    }

    /// <summary>
    /// 歯車行（トルク・RPM）の文を役割から 1 回で決める（Web gearRowDisplay の写し）
    /// Settles both gear rows (torque, RPM) once from the role (port of Web gearRowDisplay)
    /// </summary>
    public sealed class GearRowDisplay
    {
        public UiText Torque { get; }
        public UiText Rpm { get; }

        private GearRowDisplay(UiText torque, UiText rpm)
        {
            Torque = torque;
            Rpm = rpm;
        }

        public static GearRowDisplay From(GearDetail gear)
        {
            var torqueParams = new[] { new LocalizationTextParam("value", UiNumberFormat.Fixed(gear.CurrentTorque, 1)) };
            var current = new LocalizationTextParam("current", UiNumberFormat.Fixed(gear.CurrentRpm, 1));

            // 消費側は基準 RPM 付き、発電側は現在値だけ
            // Consumers also show the base RPM; generators show only the current value
            if (gear is ConsumerGearDetail consumer)
            {
                var baseRpm = new LocalizationTextParam("base", UiNumberFormat.Fixed(consumer.BaseRpm, 1));
                return new GearRowDisplay(
                    new UiText(LocalizationKeys.Ui.BlockInventory.GearConsumedTorque, torqueParams),
                    new UiText(LocalizationKeys.Ui.BlockInventory.GearRpmWithBase, new[] { current, baseRpm }));
            }
            return new GearRowDisplay(
                new UiText(LocalizationKeys.Ui.BlockInventory.GearGeneratedTorque, torqueParams),
                new UiText(LocalizationKeys.Ui.BlockInventory.GearRpmCurrent, new[] { current }));
        }
    }
}
