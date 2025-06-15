namespace Client.Game.InGame.UI.Inventory.Common
{
    public static class CommonSlotViewExtension
    {
        public static void SetGrayOut(this CommonSlotView commonSlotView, bool active)
        {
            commonSlotView.SetSlotViewOption(new CommonSlotViewOption
            {
                GrayOut = active
            });
        }
    }
}