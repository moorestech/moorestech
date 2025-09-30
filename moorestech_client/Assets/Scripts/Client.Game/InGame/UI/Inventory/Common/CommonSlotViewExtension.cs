using System.Reflection;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Common
{
    public static class CommonSlotViewExtension
    {
        // ----- GrayOut -----
        public static void SetGrayOut(this CommonSlotView view, bool active) => view.SetSlotViewOption(GetGrayOutOption(active));
        public static void SetGrayOut(this ItemSlotView obj, bool active)   => obj.SetSlotViewOption(GetGrayOutOption(active));
        private  static CommonSlotViewOption GetGrayOutOption(bool active)    => new() { GrayOut = active };

        // ----- FrameType -----
        public static void SetFrameType(this CommonSlotView view, ItemSlotFrameType frameType) => view.SetSlotViewOption(GetFrameTypeOption(frameType));
        public static void SetFrameType(this ItemSlotView obj, ItemSlotFrameType frameType)  => obj.SetSlotViewOption(GetFrameTypeOption(frameType));
        private  static CommonSlotViewOption GetFrameTypeOption(ItemSlotFrameType frameType)   => new() { ItemSlotFrameType = frameType };

        // ----- SlotType -----
        public static void SetSlotType(this CommonSlotView view, ItemSlotType slotType) => view.SetSlotViewOption(GetSlotTypeOption(slotType));
        public static void SetSlotType(this ItemSlotView obj, ItemSlotType slotType)  => obj.SetSlotViewOption(GetSlotTypeOption(slotType));
        private  static CommonSlotViewOption GetSlotTypeOption(ItemSlotType slotType)   => new() { ItemSlotType = slotType };

        // ----- HotBarSelected -----
        public static void SetHotBarSelected(this CommonSlotView view, bool isSelected) => view.SetSlotViewOption(GetHotBarSelectedOption(isSelected));
        public static void SetHotBarSelected(this ItemSlotView obj, bool isSelected)  => obj.SetSlotViewOption(GetHotBarSelectedOption(isSelected));
        private  static CommonSlotViewOption GetHotBarSelectedOption(bool isSelected)   => new() { HotBarSelected = isSelected };

        // ----- ShowToolTip -----
        public static void SetShowToolTip(this CommonSlotView view, bool isShow) => view.SetSlotViewOption(GetShowToolTipOption(isShow));
        public static void SetShowToolTip(this ItemSlotView obj, bool isShow)  => obj.SetSlotViewOption(GetShowToolTipOption(isShow));
        private  static CommonSlotViewOption GetShowToolTipOption(bool isShow)  => new() { IsShowToolTip = isShow };
        
        // ----- CountTextFontSize -----
        public static void SetCountTextFontSize(this CommonSlotView view, int fontSize) => view.SetSlotViewOption(GetCountTextFontSizeOption(fontSize));
        public static void SetCountTextFontSize(this ItemSlotView obj, int fontSize)  => obj.SetSlotViewOption(GetCountTextFontSizeOption(fontSize));
        private  static CommonSlotViewOption GetCountTextFontSizeOption(int fontSize)   => new() { CountTextFontSize = fontSize };
        
        // ----- SizeDelta -----
        public static void SetSizeDelta(this CommonSlotView view, Vector2 sizeDelta) => view.SetSlotViewOption(GetSizeDeltaOption(sizeDelta));
        public static void SetSizeDelta(this ItemSlotView obj, Vector2 sizeDelta)  => obj.SetSlotViewOption(GetSizeDeltaOption(sizeDelta));
        private  static CommonSlotViewOption GetSizeDeltaOption(Vector2 sizeDelta)   => new() { SizeDelta = sizeDelta };
    }
}
