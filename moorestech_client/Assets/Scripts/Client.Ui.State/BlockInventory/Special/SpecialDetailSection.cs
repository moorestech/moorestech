namespace Client.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 特殊族の View が占める場所。出る条件は SpecialDetailVisibility がこの値ごとに決める
    /// Where a special-family view sits; SpecialDetailVisibility decides the showing condition per value
    /// </summary>
    public enum SpecialDetailSection
    {
        Gear,
        GearToElectric,
        ElectricNetwork,
        GearNetwork,
        Body,
        TitleAction,
    }
}
