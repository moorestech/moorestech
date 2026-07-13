using System;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ConnectTool
{
    /// <summary>
    /// 接続ツール1種の定義（アイコン素材・設置ブロックはマスタ参照セレクタ）
    /// One connect tool definition; icon material and place block are master selectors
    /// </summary>
    public sealed class ConnectToolDefinition
    {
        public readonly ConnectToolType ToolType;
        public readonly string DisplayName;

        private readonly Func<Guid?> _iconItemGuidSelector;
        private readonly Func<BlockId?> _placeBlockSelector;

        public ConnectToolDefinition(ConnectToolType toolType, string displayName, Func<Guid?> iconItemGuidSelector, Func<BlockId?> placeBlockSelector)
        {
            ToolType = toolType;
            DisplayName = displayName;
            _iconItemGuidSelector = iconItemGuidSelector;
            _placeBlockSelector = placeBlockSelector;
        }

        // アイコンの素材アイテム。素材未定義のModではnull
        // The material item for the icon; null on mods with no material
        public Guid? SelectIconItemGuid()
        {
            return _iconItemGuidSelector();
        }

        // 空きスペース延長時の自動設置ブロック（レール→橋脚、電線→電柱）
        // Block auto-placed on empty-space extension (rail->pier, wire->pole)
        public bool TryGetPlaceBlock(out BlockId blockId, out BlockMasterElement blockMaster)
        {
            var selectedBlockId = _placeBlockSelector();
            if (selectedBlockId == null)
            {
                blockId = default;
                blockMaster = null;
                return false;
            }

            blockId = selectedBlockId.Value;
            blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            return true;
        }
    }
}
