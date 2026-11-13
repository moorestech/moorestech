using Client.Localization;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Special.Text
{
    /// <summary>
    /// 特殊族の区画・本文の文。値の書式は Web の各 View と同じ（NetworkSections・GearToElectricSection・ElectricToGearInventory・FilterSplitterInventory・SpaceElevatorSection）
    /// Sentences of the special sections and bodies; value formats match each Web view
    /// </summary>
    public static class SpecialDetailTexts
    {
        public static UiText ElectricNetworkSummary(ElectricNetworkDetail network)
        {
            // 供給率は JS の Math.round と同じく .5 を上へ丸める
            // The supply rate rounds .5 up like JS Math.round
            var rate = UiNumberFormat.Integer(UiNumberFormat.RoundHalfUp(network.PowerRate * 100));
            return With(LocalizationKeys.Ui.BlockInventory.ElectricNetworkSummary,
                Param("generated", JsNumberText.Fixed0(network.TotalGeneratePower)),
                Param("required", JsNumberText.Fixed0(network.TotalRequiredPower)),
                Param("count", network.ConsumerCount.ToString()),
                Param("rate", rate));
        }

        public static UiText GearNetworkSummary(GearNetworkDetail network)
        {
            return With(LocalizationKeys.Ui.BlockInventory.GearNetworkSummary,
                Param("generated", JsNumberText.Fixed0(network.TotalGenerateGearPower)),
                Param("required", JsNumberText.Fixed0(network.TotalRequiredGearPower)));
        }

        public static UiText GearToElectricSuppliedPower(GearToElectricDetail detail)
        {
            return With(LocalizationKeys.Ui.BlockInventory.GearToElectricSuppliedPower,
                Param("current", JsNumberText.PositivePower(detail.SuppliedPower)),
                Param("max", JsNumberText.PositivePower(detail.MaxGeneratedPower)));
        }

        public static UiText ElectricToGearOutputMode(ElectricToGearOutputMode mode)
        {
            return With(LocalizationKeys.Ui.BlockInventory.ElectricToGearOutputMode,
                Param("rpm", UiNumberFormat.JsString(mode.Rpm)),
                Param("torque", UiNumberFormat.JsString(mode.Torque)),
                Param("power", UiNumberFormat.JsString(mode.RequiredPower)));
        }

        public static UiText ElectricToGearConsumedPower(ElectricToGearDetail detail)
        {
            return With(LocalizationKeys.Ui.BlockInventory.ElectricToGearConsumedPower, Param("power", JsNumberText.Fixed0(detail.ConsumedElectricPower)));
        }

        // 方向は 1 始まりで見せる
        // Directions are shown starting at 1
        public static UiText OutputNumber(int directionIndex)
        {
            return With(LocalizationKeys.Ui.BlockInventory.OutputNumber, Param("index", (directionIndex + 1).ToString()));
        }

        public static UiText SpaceElevatorPool(SpaceElevatorDetail detail)
        {
            return With(LocalizationKeys.Ui.BlockInventory.SpaceElevatorPool, Param("pool", detail.CablePoolAmount.ToString()), Param("required", detail.CablesPerElevator.ToString()));
        }

        private static LocalizationTextParam Param(string name, string value)
        {
            return new LocalizationTextParam(name, value);
        }

        private static UiText With(LocalizationKey key, params LocalizationTextParam[] parameters)
        {
            return new UiText(key, parameters);
        }
    }
}
