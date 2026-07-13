using System;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ConnectTool
{
    /// <summary>
    /// 接続ツール1種の定義。アイコン素材と延長時の設置ブロックはマスタを引くセレクタで表現する
    /// One connect tool definition; the icon material and the extension place block are expressed as master selectors
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

        // ビルドメニューに出すアイコンの素材アイテム。素材未定義のModではnull
        // The material item used as the build-menu icon; null on mods that define no material
        public Guid? SelectIconItemGuid()
        {
            return _iconItemGuidSelector();
        }

        // 空きスペースへ延長する際に自動設置するブロック（レール接続→橋脚、電線接続→電柱）
        // The block auto-placed when extending into empty space (rail connect -> pier, wire connect -> pole)
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
