using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Core.Master;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Family
{
    /// <summary>
    /// ポンプの状態詳細と param から詳細を組む（Web ホスト PumpDetailDtoBuilder の写し。未知の param は例外でなく Error ログ）
    /// Builds the pump detail from its state detail and param (port of the Web host's PumpDetailDtoBuilder; an unknown param logs an error instead of throwing)
    /// </summary>
    public static class PumpDetailConversion
    {
        public static bool TryBuild(PumpBlockStateDetail pump, IBlockParam param, CommonMachineBlockStateDetail common, out PumpDetail detail)
        {
            detail = null;
            // 種別の正はマスタの param（common の有無で代用しない）
            // The master's param settles the kind (never stand in common's presence)
            switch (param)
            {
                case ElectricPumpBlockParam:
                    // silent-ok: 油井の電力値は CommonMachine だけが持ち、揃うまで出さない（Web ホストと同じ） / 揃えば状態変化の購読で出る
                    if (common == null) return false;
                    if (!MachineStateTypeConversion.TryConvert(common.CurrentStateType, out var state))
                    {
                        Debug.LogError($"[PumpDetailConversion] unknown CurrentStateType: '{common.CurrentStateType}'");
                        return false;
                    }
                    detail = new ElectricPumpDetail(state, common.CurrentPower, common.RequestPower, Fluids());
                    return true;
                case GearPumpBlockParam:
                    detail = new GearPumpDetail(Fluids());
                    return true;
                default:
                    // 未知の param はコードの不整合。購読を殺さないよう投げずに開発者ログへ出す
                    // An unknown param is a code inconsistency; log for developers instead of throwing so the subscription survives
                    Debug.LogError($"[PumpDetailConversion] unsupported pump param: {param?.GetType().Name}");
                    return false;
            }

            #region Internal

            IReadOnlyList<PumpingFluid> Fluids()
            {
                var fluids = new List<PumpingFluid>();
                foreach (var pumping in pump.PumpingFluids) fluids.Add(new PumpingFluid(new FluidId(pumping.FluidId), pumping.AmountPerSecond * 60));
                return fluids;
            }

            #endregion
        }
    }
}
