using Client.Ui.State.BlockInventory.SectionStack;

namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 区画を作るときの条件（blockType の設定と高さ確定）
    /// Conditions when creating a section (the blockType settings and the fixed-height flag)
    /// </summary>
    public sealed class SectionStackSectionMount
    {
        public SectionStackConfig Config { get; private set; }
        public bool FillsPanelHeight { get; private set; }

        public SectionStackSectionMount(SectionStackConfig config, bool fillsPanelHeight)
        {
            Config = config;
            FillsPanelHeight = fillsPanelHeight;
        }
    }
}
