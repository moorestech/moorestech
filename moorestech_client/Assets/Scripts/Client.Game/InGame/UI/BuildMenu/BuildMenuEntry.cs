using System;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Mod.Texture;
using Core.Master;

namespace Client.Game.InGame.UI.BuildMenu
{
    /// <summary>
    /// ビルドメニューの1エントリ（ブロック・車両・接続ツール・BPのいずれか）
    /// One build-menu entry: a block, a train car, a connect tool, or a blueprint
    /// </summary>
    public readonly struct BuildMenuEntry
    {
        public readonly PlacementSelectionType EntryType;
        public readonly BlockId BlockId;
        public readonly Guid TrainCarGuid;
        public readonly string ConnectPlaceMode;
        public readonly string BlueprintName;
        public readonly ItemViewData IconView;
        public readonly string ToolTipText;

        public BuildMenuEntry(PlacementSelectionType entryType, BlockId blockId, Guid trainCarGuid, string connectPlaceMode, ItemViewData iconView, string toolTipText)
        {
            EntryType = entryType;
            BlockId = blockId;
            TrainCarGuid = trainCarGuid;
            ConnectPlaceMode = connectPlaceMode;
            BlueprintName = null;
            IconView = iconView;
            ToolTipText = toolTipText;
        }

        // BP用エントリ（アイコン無し・BP名をそのまま表示に使う）
        // Blueprint entry: no icon, the blueprint name doubles as the display text
        public BuildMenuEntry(string blueprintName)
        {
            EntryType = PlacementSelectionType.Blueprint;
            BlockId = default;
            TrainCarGuid = default;
            ConnectPlaceMode = null;
            BlueprintName = blueprintName;
            IconView = null;
            ToolTipText = blueprintName;
        }
    }
}
