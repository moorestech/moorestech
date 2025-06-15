using System.Reflection;

namespace Client.Game.InGame.UI.Inventory.Common
{
    public static class CommonSlotViewExtension
    {
        // ----- GrayOut -----
        public static void SetGrayOut(this CommonSlotView view, bool active) => view.SetSlotViewOption(GetGrayOutOption(active));
        public static void SetGrayOut(this ItemSlotObject obj, bool active)   => obj.SetSlotViewOption(GetGrayOutOption(active));
        private  static CommonSlotViewOption GetGrayOutOption(bool active)    => new() { GrayOut = active };

        // ----- FrameType -----
        public static void SetFrameType(this CommonSlotView view, ItemSlotFrameType frameType) => view.SetSlotViewOption(GetFrameTypeOption(frameType));
        public static void SetFrameType(this ItemSlotObject obj, ItemSlotFrameType frameType)  => obj.SetSlotViewOption(GetFrameTypeOption(frameType));
        private  static CommonSlotViewOption GetFrameTypeOption(ItemSlotFrameType frameType)   => new() { ItemSlotFrameType = frameType };

        // ----- SlotType -----
        public static void SetSlotType(this CommonSlotView view, ItemSlotType slotType) => view.SetSlotViewOption(GetSlotTypeOption(slotType));
        public static void SetSlotType(this ItemSlotObject obj, ItemSlotType slotType)  => obj.SetSlotViewOption(GetSlotTypeOption(slotType));
        private  static CommonSlotViewOption GetSlotTypeOption(ItemSlotType slotType)   => new() { ItemSlotType = slotType };

        // ----- HotBarSelected -----
        public static void SetHotBarSelected(this CommonSlotView view, bool isSelected) => view.SetSlotViewOption(GetHotBarSelectedOption(isSelected));
        public static void SetHotBarSelected(this ItemSlotObject obj, bool isSelected)  => obj.SetSlotViewOption(GetHotBarSelectedOption(isSelected));
        private  static CommonSlotViewOption GetHotBarSelectedOption(bool isSelected)   => new() { HotBarSelected = isSelected };

        // ----- ShowToolTip -----
        public static void SetShowToolTip(this CommonSlotView view, bool isShow) => view.SetSlotViewOption(GetShowToolTipOption(isShow));
        public static void SetShowToolTip(this ItemSlotObject obj, bool isShow)  => obj.SetSlotViewOption(GetShowToolTipOption(isShow));
        private  static CommonSlotViewOption GetShowToolTipOption(bool isShow)  => new() { IsShowToolTip = isShow };
    }
}
