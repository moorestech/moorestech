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
        
        public static void SetFrameType(this CommonSlotView commonSlotView, ItemSlotFrameType frameType)
        {
            commonSlotView.SetSlotViewOption(new CommonSlotViewOption
            {
                ItemSlotFrameType = frameType
            });
        }
        
        public static void SetSlotType(this CommonSlotView commonSlotView, ItemSlotType slotType)
        {
            commonSlotView.SetSlotViewOption(new CommonSlotViewOption
            {
                ItemSlotType = slotType
            });
        }
        
        public static void SetHotBarSelected(this CommonSlotView commonSlotView, bool isSelected)
        {
            commonSlotView.SetSlotViewOption(new CommonSlotViewOption
            {
                HotBarSelected = isSelected
            });
        }
        
        public static void SetShowToolTip(this CommonSlotView commonSlotView, bool isShow)
        {
            commonSlotView.SetSlotViewOption(new CommonSlotViewOption
            {
                IsShowToolTip = isShow
            });
        }
    }
}