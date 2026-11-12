using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.Text;
using Core.Master;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;

namespace Client.Ui.State.BlockInventory.Family
{
    /// <summary>
    /// ポンプの詳細を状態詳細と param から組む
    /// Builds the pump detail from state detail and param; pure: an unknown state or param is returned as an anomaly text, not thrown or logged (the Model reports it once)
    /// </summary>
    public static class PumpDetailConversion
    {
        public static PumpDetailReading Build(PumpBlockStateDetail pump, IBlockParam param, CommonMachineBlockStateDetail common)
        {
            // 種別の正はマスタの param
            // The master's param settles the kind, never common's presence
            switch (param)
            {
                case ElectricPumpBlockParam:
                    // silent-ok: 油井の電力値は CommonMachine だけが持ち、揃うまで出さない（Web ホストと同じ） / 揃えば状態変化の購読で出る
                    if (common == null) return PumpDetailReading.Waiting();
                    if (!MachineStateTypeConversion.TryConvert(common.CurrentStateType, out var state)) return PumpDetailReading.Anomalous($"[PumpDetailConversion] unknown CurrentStateType: '{common.CurrentStateType}'");
                    return PumpDetailReading.Built(new ElectricPumpDetail(state, UiNumberFormat.FromServerFloat(common.CurrentPower), UiNumberFormat.FromServerFloat(common.RequestPower), Fluids()));
                case GearPumpBlockParam:
                    return PumpDetailReading.Built(new GearPumpDetail(Fluids()));
                default:
                    // 未知の param は投げず異常で返す
                    // Return unknown param as anomaly, do not throw
                    return PumpDetailReading.Anomalous($"[PumpDetailConversion] unsupported pump param: {param?.GetType().Name}");
            }

            #region Internal

            IReadOnlyList<PumpingFluid> Fluids()
            {
                var fluids = new List<PumpingFluid>();
                foreach (var pumping in pump.PumpingFluids) fluids.Add(new PumpingFluid(new FluidId(pumping.FluidId), pumping.AmountPerSecond * BlockDetailMath.SecondsPerMinute));
                return fluids;
            }

            #endregion
        }
    }

    /// <summary>
    /// ポンプの変換結果。組めた・静かな待ち・異常の 1 つ
    /// Pump result: built, silent wait or anomaly
    /// </summary>
    public readonly struct PumpDetailReading
    {
        private readonly PumpDetail _detail;
        private readonly string _anomaly;

        private PumpDetailReading(PumpDetail detail, string anomaly)
        {
            _detail = detail;
            _anomaly = anomaly;
        }

        public static PumpDetailReading Built(PumpDetail detail)
        {
            return new PumpDetailReading(detail, null);
        }

        public static PumpDetailReading Waiting()
        {
            return new PumpDetailReading(null, null);
        }

        public static PumpDetailReading Anomalous(string anomaly)
        {
            return new PumpDetailReading(null, anomaly);
        }

        public bool TryGetDetail(out PumpDetail detail)
        {
            detail = _detail;
            return _detail != null;
        }

        public bool TryGetAnomaly(out string anomaly)
        {
            anomaly = _anomaly;
            return _anomaly != null;
        }
    }
}
