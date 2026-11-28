using System;
using System.Collections.Generic;
using S = Mooresmaster.Model.BlocksModule.BlockMasterElement.BlockScreenConst;

namespace Client.Ui.State.BlockInventory.SectionStack
{
    /// <summary>
    /// ブロック画面IDに対応する標準本文の設定
    /// Standard-body settings keyed by block screen ID
    /// </summary>
    public static class SectionStackConfigTable
    {
        private static readonly Dictionary<string, SectionStackConfig> ByScreen = new()
        {
            [S.Generic] = new SectionStackConfig(Grid("generic-block-grid", false), Row("generic-block-fluids", true)),
            [S.FluidPipe] = new SectionStackConfig(SectionStackItemGrid.Hidden, new DetailsSectionStackFluid("fluid-pipe-fluid-slots")),
            [S.Chest] = new SectionStackConfig(Grid("chest-grid", true), SectionStackFluid.Hidden),
            [S.Machine] = new SectionStackConfig(SectionStackItemGrid.Hidden, Row("machine-fluid-slots", false)),
            [S.ElectricGenerator] = new SectionStackConfig(Grid("generator-fuel-grid", false), Row("generator-fluid-slots", false)),
            [S.FuelGearGenerator] = new SectionStackConfig(Grid("generator-fuel-grid", true), SectionStackFluid.Hidden),
            [S.GearToElectricGenerator] = new SectionStackConfig(SectionStackItemGrid.Hidden, SectionStackFluid.Hidden),
            [S.ElectricMiner] = new SectionStackConfig(Grid("miner-output-grid", true), SectionStackFluid.Hidden),
            [S.GearMiner] = new SectionStackConfig(Grid("gear-miner-output-grid", true), SectionStackFluid.Hidden),
            [S.Pump] = new SectionStackConfig(SectionStackItemGrid.Hidden, Row("pump-fluid-slots", false)),
            [S.CleanRoomAirFilter] = new SectionStackConfig(Grid("clean-room-filter-grid", true), SectionStackFluid.Hidden),
        };

        public static SectionStackConfig Resolve(string blockScreen)
        {
            // 標準の本文を持つIDだけが載る。専用本文のIDで呼ぶのは配線の誤り
            // Only IDs with the standard body are listed; calling with a dedicated-body ID is a wiring error
            if (blockScreen != null && ByScreen.TryGetValue(blockScreen, out var config)) return config;
            throw new InvalidOperationException($"ブロック画面ID {blockScreen} の標準の本文の設定が無い");
        }

        private static SectionStackItemGrid Grid(string gridId, bool renderEmpty) => new ShownSectionStackItemGrid(gridId, renderEmpty);
        private static SectionStackFluid Row(string rowId, bool showProgress) => new RowSectionStackFluid(rowId, showProgress);
    }
}
