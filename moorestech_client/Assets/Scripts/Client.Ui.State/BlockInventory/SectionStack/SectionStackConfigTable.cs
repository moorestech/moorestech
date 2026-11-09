using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;
using T = Mooresmaster.Model.BlocksModule.BlockMasterElement.BlockTypeConst;

namespace Client.Ui.State.BlockInventory.SectionStack
{
    /// <summary>
    /// blockType → 標準の本文の設定（Web configByBlockType と genericConfig の 1 対 1 の写し）
    /// blockType → standard-body settings (one-to-one port of Web configByBlockType and genericConfig)
    /// </summary>
    public static class SectionStackConfigTable
    {
        public static readonly SectionStackConfig Generic = new(Grid("generic-block-grid", false), Row("generic-block-fluids", true));

        private static readonly Dictionary<string, SectionStackConfig> ByBlockType = new()
        {
            [T.FluidPipe] = new SectionStackConfig(SectionStackItemGrid.Hidden, new DetailsSectionStackFluid("fluid-pipe-fluid-slots")),
            [T.Chest] = new SectionStackConfig(Grid("chest-grid", true), SectionStackFluid.Hidden),
            [T.ElectricMachine] = new SectionStackConfig(SectionStackItemGrid.Hidden, Row("machine-fluid-slots", false)),
            [T.GearMachine] = new SectionStackConfig(SectionStackItemGrid.Hidden, Row("machine-fluid-slots", false)),
            [T.ElectricGenerator] = new SectionStackConfig(Grid("generator-fuel-grid", false), Row("generator-fluid-slots", false)),
            [T.FuelGearGenerator] = new SectionStackConfig(Grid("generator-fuel-grid", true), SectionStackFluid.Hidden),
            [T.SimpleGearGenerator] = new SectionStackConfig(Grid("generator-fuel-grid", true), SectionStackFluid.Hidden),
            [T.GearToElectricGenerator] = new SectionStackConfig(SectionStackItemGrid.Hidden, SectionStackFluid.Hidden),
            [T.ElectricMiner] = new SectionStackConfig(Grid("miner-output-grid", true), SectionStackFluid.Hidden),
            [T.GearMiner] = new SectionStackConfig(Grid("gear-miner-output-grid", true), SectionStackFluid.Hidden),
            [T.ElectricPump] = new SectionStackConfig(SectionStackItemGrid.Hidden, Row("pump-fluid-slots", false)),
            [T.GearPump] = new SectionStackConfig(SectionStackItemGrid.Hidden, Row("pump-fluid-slots", false)),
            [T.CleanRoomMachine] = new SectionStackConfig(SectionStackItemGrid.Hidden, Row("machine-fluid-slots", false)),
            [T.CleanRoomAirFilter] = new SectionStackConfig(Grid("clean-room-filter-grid", true), SectionStackFluid.Hidden),
        };

        public static SectionStackConfig Resolve(string blockType)
        {
            // silent-ok: 未登録の blockType は Web の genericConfig と同じ正規の既定表示 / 取りこぼしは BlockRegistryCoverageTest と S4-alpha の録画で見える
            return ByBlockType.TryGetValue(blockType, out var config) ? config : Generic;
        }

        private static SectionStackItemGrid Grid(string gridId, bool renderEmpty) => new ShownSectionStackItemGrid(gridId, renderEmpty);
        private static SectionStackFluid Row(string rowId, bool showProgress) => new RowSectionStackFluid(rowId, showProgress);
    }
}
