namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 標準の本文の固定順の区画（Web SectionStackView の並び。列挙の順が描く順）
    /// Fixed-order sections of the standard body (Web SectionStackView order; enum order is render order)
    /// </summary>
    public enum SectionStackSlot
    {
        Context,          // 清浄室・抽選・搬入停止（S4-machine。機械では Machine の中へ前置する）
        ItemGrid,         // 項目グリッド（S4-alpha。差し込めない）
        Machine,          // S4-machine
        Miner,            // S4-machine
        Pump,             // S4-machine
        Generator,        // S4-machine
        GearToElectric,   // S4-special
        BeltThroughput,   // S4-special
        Gear,             // S4-special
        ElectricNetwork,  // S4-special
        GearNetwork,      // S4-special
        FluidDetails,     // S4-machine（Fluid/。Config.Fluid が DetailsSectionStackFluid）
        FluidRow,         // S4-machine（Fluid/。Config.Fluid が RowSectionStackFluid で機械以外）
    }
}
