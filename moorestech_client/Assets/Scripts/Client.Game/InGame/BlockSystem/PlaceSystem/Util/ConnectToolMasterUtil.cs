using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 接続ツールエントリに紐づく自動設置ブロック（橋脚・電柱）を解決する
    /// Resolves the auto-placed block (pier / pole) bound to a connect tool entry
    /// </summary>
    public static class ConnectToolMasterUtil
    {
        public static bool TryGetPlaceBlock(string placeMode, out BlockId blockId, out BlockMasterElement blockMaster)
        {
            blockId = default;
            blockMaster = null;

            // placeMode一致かつPlaceBlockGuid定義済みのエントリから設置ブロックを解決する
            // Resolve the place block from the entry matching placeMode with a defined PlaceBlockGuid
            foreach (var element in MasterHolder.PlaceSystemMaster.PlaceSystem.Data)
            {
                if (element.PlaceMode != placeMode || element.PlaceBlockGuid == null) continue;
                blockId = MasterHolder.BlockMaster.GetBlockId(element.PlaceBlockGuid.Value);
                blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
                return true;
            }

            return false;
        }
    }
}
