using System;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Family;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Core.Master;
using T = Mooresmaster.Model.BlocksModule.BlockMasterElement.BlockTypeConst;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine.Regression
{
    /// <summary>
    /// Web e2e の mock-host の setBlock の fixture を、本レーンの Model の値へ写した 1 件（blockType と各 Model の値）
    /// One Web e2e mock-host setBlock fixture ported to this lane's model values (blockType and each model's value)
    /// </summary>
    public sealed class SectionStackRegressionEntry
    {
        public string BlockType { get; private set; }
        public SectionContent<MachineSectionViewModel> Machine { get; private set; } = SectionContent<MachineSectionViewModel>.Absent;
        public SectionContent<MinerDetail> Miner { get; private set; } = SectionContent<MinerDetail>.Absent;
        public SectionContent<PumpDetail> Pump { get; private set; } = SectionContent<PumpDetail>.Absent;
        public SectionContent<GeneratorDetail> Generator { get; private set; } = SectionContent<GeneratorDetail>.Absent;
        public BlockFluidSlotsViewModel Fluids { get; private set; }

        public SectionStackRegressionEntry(string blockType, BlockFluidSlotsViewModel fluids)
        {
            BlockType = blockType;
            Fluids = fluids;
        }

        public SectionStackRegressionEntry SetMachine(MachineSectionViewModel machine)
        {
            Machine = SectionContent<MachineSectionViewModel>.Present(machine);
            return this;
        }

        public SectionStackRegressionEntry SetMiner(MinerDetail miner)
        {
            Miner = SectionContent<MinerDetail>.Present(miner);
            return this;
        }

        public SectionStackRegressionEntry SetPump(PumpDetail pump)
        {
            Pump = SectionContent<PumpDetail>.Present(pump);
            return this;
        }

        public SectionStackRegressionEntry SetGenerator(GeneratorDetail generator)
        {
            Generator = SectionContent<GeneratorDetail>.Present(generator);
            return this;
        }
    }

    /// <summary>
    /// sectionStack.spec.ts の cases の entry.type → 本レーンの Model の値（moorestech_web/webui/e2e/mock-host/fixtures の写し。マスタ読み込み済みが前提）
    /// sectionStack.spec.ts cases entry.type → this lane's model values (port of moorestech_web/webui/e2e/mock-host/fixtures; master must be loaded)
    /// </summary>
    public static class SectionStackRegressionEntries
    {
        public static readonly Guid WaterGuid = new("00000000-0000-0000-1234-000000000001");
        public static readonly Guid ItemGuid = new("00000000-0000-0000-1234-000000000001");

        private static FluidId Water => MasterHolder.FluidMaster.GetFluidId(WaterGuid);
        private static ItemId Item => MasterHolder.ItemMaster.GetItemId(ItemGuid);

        public static SectionStackRegressionEntry For(string entryType)
        {
            // blockType は fixture の値そのまま（tank・UnregisteredBlock は汎用の設定に落ちる）
            // blockType is the fixture's own value (tank and UnregisteredBlock fall to the generic setting)
            return entryType switch
            {
                "chest" => new SectionStackRegressionEntry(T.Chest, NoFluids()),
                "tank" => new SectionStackRegressionEntry("tank", FluidsOf(true, 0.5f, BlockFluidSlotState.Filled(Water, 500, 1000), BlockFluidSlotState.Empty(1000))),
                "machine" => MachineEntry(T.ElectricMachine, new MachineSlotLayout(2, 1, 1), false, BlockFluidSlotState.Empty(100), BlockFluidSlotState.Empty(100)),
                "gearMachine" => MachineEntry(T.GearMachine, new MachineSlotLayout(1, 1, 0), true),
                "miner" => new SectionStackRegressionEntry(T.ElectricMiner, NoFluids()).SetMiner(new MinerDetail(50, 100, 0, false, new[] { new MinerMiningItem(Item, 12) })),
                "gearMiner" => new SectionStackRegressionEntry(T.GearMiner, NoFluids()).SetMiner(new MinerDetail(20, 40, 0, true, new[] { new MinerMiningItem(Item, 6) })),
                "generator" => new SectionStackRegressionEntry(T.ElectricGenerator, NoFluids()).SetGenerator(new GeneratorDetail(12.5, 30, 0.75)),
                "generic" => new SectionStackRegressionEntry("UnregisteredBlock", FluidsOf(false, 0, BlockFluidSlotState.Filled(Water, 10, 20))),
                "filterSplitter" => new SectionStackRegressionEntry(T.FilterSplitter, NoFluids()),
                "pump" => new SectionStackRegressionEntry(T.ElectricPump, FluidsOf(false, 0, BlockFluidSlotState.Filled(Water, 120, 200)))
                    .SetPump(new ElectricPumpDetail(MachineProcessDisplayState.Processing, 10, 10, new[] { new PumpingFluid(Water, 3600) })),
                "gearPump" => new SectionStackRegressionEntry(T.GearPump, FluidsOf(false, 0, BlockFluidSlotState.Filled(Water, 30, 100)))
                    .SetPump(new GearPumpDetail(new[] { new PumpingFluid(Water, 120) })),
                // 風車は generator を持たない（Web fixture は gear と gearNetwork だけ）
                // The windmill has no generator detail (the Web fixture carries only gear and gearNetwork)
                "gearGenerator" => new SectionStackRegressionEntry(T.SimpleGearGenerator, NoFluids()),
                _ => throw new ArgumentOutOfRangeException(nameof(entryType), entryType, null),
            };
        }

        // 機械の fixture（加工中・入力の先頭に 1 品・液体は機械の本体が持つ）
        // Machine fixture (processing, one item in the first input, fluids carried by the machine body)
        private static SectionStackRegressionEntry MachineEntry(string blockType, MachineSlotLayout layout, bool gearDriven, params BlockFluidSlotState[] fluids)
        {
            var slots = new SlotItem[layout.Input + layout.Output + layout.Module];
            for (var i = 0; i < slots.Length; i++) slots[i] = new SlotItem(new ItemId(0), 0);
            slots[0] = new SlotItem(Item, 5);
            var detail = new MachineDetail(MachineRecipeSelection.Unselected, MachineProcessDisplayState.Processing, 80, 100, 0.42f, false, gearDriven, 15, Array.Empty<RecipeOutputItem>(), new MachineBindingDetail(layout, Array.Empty<MachineItemSlotBinding>(), Array.Empty<MachineTankBinding>()));
            var machine = MachineScreenViewModels.Build(detail, slots, MachineRecipeCatalog.Empty, 1, fluids);
            return new SectionStackRegressionEntry(blockType, FluidsOf(true, 0.42f, fluids)).SetMachine(machine);
        }

        private static BlockFluidSlotsViewModel NoFluids()
        {
            return FluidsOf(false, 0, Array.Empty<BlockFluidSlotState>());
        }

        private static BlockFluidSlotsViewModel FluidsOf(bool hasProgress, float progress, params BlockFluidSlotState[] slots)
        {
            return new BlockFluidSlotsViewModel(slots, hasProgress, progress);
        }
    }
}
