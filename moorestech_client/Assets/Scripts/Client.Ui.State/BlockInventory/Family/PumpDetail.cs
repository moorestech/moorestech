using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Core.Master;

namespace Client.Ui.State.BlockInventory.Family
{
    /// <summary>
    /// 汲み上げ中の液体 1 つと毎分の量
    /// One pumped fluid and its per-minute amount
    /// </summary>
    public sealed class PumpingFluid
    {
        public FluidId FluidId { get; }
        public double AmountPerMinute { get; }

        public PumpingFluid(FluidId fluidId, double amountPerMinute)
        {
            FluidId = fluidId;
            AmountPerMinute = amountPerMinute;
        }
    }

    /// <summary>
    /// ポンプの詳細。電気と歯車の 2 種
    /// Snapshot of a pump's detail, closed over electric and gear (Web kinds)
    /// </summary>
    public abstract class PumpDetail
    {
        public IReadOnlyList<PumpingFluid> PumpingFluids { get; }

        private protected PumpDetail(IReadOnlyList<PumpingFluid> pumpingFluids)
        {
            PumpingFluids = pumpingFluids;
        }
    }

    public sealed class ElectricPumpDetail : PumpDetail
    {
        public MachineProcessDisplayState State { get; }
        public double CurrentPower { get; }
        public double RequestPower { get; }

        public ElectricPumpDetail(MachineProcessDisplayState state, double currentPower, double requestPower, IReadOnlyList<PumpingFluid> pumpingFluids) : base(pumpingFluids)
        {
            State = state;
            CurrentPower = currentPower;
            RequestPower = requestPower;
        }
    }

    public sealed class GearPumpDetail : PumpDetail
    {
        public GearPumpDetail(IReadOnlyList<PumpingFluid> pumpingFluids) : base(pumpingFluids)
        {
        }
    }
}
