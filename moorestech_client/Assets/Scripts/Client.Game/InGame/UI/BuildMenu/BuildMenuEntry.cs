using System;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Mod.Texture;
using Core.Master;

namespace Client.Game.InGame.UI.BuildMenu
{
    /// <summary>
    /// ビルドメニューの1エントリ（ブロック・車両・接続ツールのいずれか）
    /// One build-menu entry: a block, a train car, or a connect tool
    /// </summary>
    public readonly struct BuildMenuEntry
    {
        public readonly PlacementSelectionType EntryType;
        public readonly BlockId BlockId;
        public readonly Guid TrainCarGuid;
        public readonly string ConnectPlaceMode;
        public readonly ItemViewData IconView;
        public readonly string ToolTipText;

        public BuildMenuEntry(PlacementSelectionType entryType, BlockId blockId, Guid trainCarGuid, string connectPlaceMode, ItemViewData iconView, string toolTipText)
        {
            EntryType = entryType;
            BlockId = blockId;
            TrainCarGuid = trainCarGuid;
            ConnectPlaceMode = connectPlaceMode;
            IconView = iconView;
            ToolTipText = toolTipText;
        }
    }
}
