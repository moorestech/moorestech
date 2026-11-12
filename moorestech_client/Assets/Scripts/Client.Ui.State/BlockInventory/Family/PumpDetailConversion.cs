using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Core.Master;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using Client.Ui.State.Text;

namespace Client.Ui.State.BlockInventory.Family
{
    /// <summary>
    /// ポンプの詳細を状態詳細と param から組む
    /// Builds the pump detail from state detail and param; pure: an unknown state or param is returned as an anomaly text, not thrown or logged (the Model reports it once)
    /// </summary>
    public static class PumpDetailConversion
    {
        public static bool TryBuild(PumpBlockStateDetail pump, IBlockParam param, CommonMachineBlockStateDetail common, out PumpDetail detail, out string anomaly)
        {
            detail = null;
            anomaly = null;
            // 種別の正はマスタの param
            // The master's param settles the kind, never common's presence
            switch (param)
            {
                case ElectricPumpBlockParam:
                    // silent-ok: 油井の電力値は CommonMachine だけが持ち、揃うまで出さない（Web ホストと同じ） / 揃えば状態変化の購読で出る
                    if (common == null) return false;
                    if (!MachineStateTypeConversion.TryConvert(common.CurrentStateType, out var state))
                    {
                        anomaly = $"[PumpDetailConversion] unknown CurrentStateType: '{common.CurrentStateType}'";
                        return false;
                    }
                    detail = new ElectricPumpDetail(state, UiNumberFormat.FromServerFloat(common.CurrentPower), UiNumberFormat.FromServerFloat(common.RequestPower), Fluids());
                    return true;
                case GearPumpBlockParam:
                    detail = new GearPumpDetail(Fluids());
                    return true;
                default:
                    // 未知の param は投げず異常として返す
                    // An unknown param is a code inconsistency; return it as an anomaly instead of throwing so the subscription survives
                    anomaly = $"[PumpDetailConversion] unsupported pump param: {param?.GetType().Name}";
                    return false;
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
}
