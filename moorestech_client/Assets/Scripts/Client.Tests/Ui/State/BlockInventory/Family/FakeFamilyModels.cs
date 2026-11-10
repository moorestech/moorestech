using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Family;
using Client.Ui.State.BlockInventory.Family.Models;
using Client.Ui.State.BlockInventory.Fluid;

namespace Client.Tests.Ui.State.BlockInventory.Family
{
    /// <summary>
    /// 族ごとの詳細 Model と液体の Model の偽物。Set で値を差し替える
    /// Fakes of the per-family detail models and the fluid model; swap values with Set
    /// </summary>
    public sealed class FakeMinerDetailModel : FakeModel<SectionContent<MinerDetail>>, IMinerDetailModel
    {
        public FakeMinerDetailModel() : base(SectionContent<MinerDetail>.Absent)
        {
        }
    }

    public sealed class FakePumpDetailModel : FakeModel<SectionContent<PumpDetail>>, IPumpDetailModel
    {
        public FakePumpDetailModel() : base(SectionContent<PumpDetail>.Absent)
        {
        }
    }

    public sealed class FakeGeneratorDetailModel : FakeModel<SectionContent<GeneratorDetail>>, IGeneratorDetailModel
    {
        public FakeGeneratorDetailModel() : base(SectionContent<GeneratorDetail>.Absent)
        {
        }
    }

    public sealed class FakeBlockFluidSlotsModel : FakeModel<BlockFluidSlotsViewModel>, IBlockFluidSlotsModel
    {
        public FakeBlockFluidSlotsModel() : base(new BlockFluidSlotsViewModel(new System.Collections.Generic.List<Client.Ui.State.BlockInventory.Logic.Common.BlockFluidSlotState>(), false, 0))
        {
        }
    }
}
