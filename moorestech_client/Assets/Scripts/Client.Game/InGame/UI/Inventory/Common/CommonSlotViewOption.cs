namespace Client.Game.InGame.UI.Inventory.Common
{
    /// <summary>
    /// スロットの設定を変更するクラス。非nullの値のみ変更を実施する
    /// A class to change slot settings. Only non-null values are changed.
    /// </summary>
    public class CommonSlotViewOption
    {
        public bool? GrayOut;
        public ItemSlotFrameType? ItemSlotFrameType;
        public ItemSlotType? ItemSlotType;
        public bool? HotBarSelected;
        public bool? ShowToolTip;
    }
    
    public enum ItemUIEventType
    {
        RightClickDown,
        LeftClickDown,
        RightClickUp,
        LeftClickUp,
        
        CursorEnter,
        CursorExit,
        CursorMove,
        
        DoubleClick,
    }
    
    public enum ItemSlotType
    {
        Normal, // 通常のアイテム表示
        NoneCross, // アイテムが何もないクロス表示
    }
    
    public enum ItemSlotFrameType
    {
        Normal,
        MachineSlot,
        CraftRecipe,
    }
}