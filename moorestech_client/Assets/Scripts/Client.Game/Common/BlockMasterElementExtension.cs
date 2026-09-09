using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.Common
{
    public static class BlockMasterElementExtension
    {
        // 破壊カテゴリーのデフォルト値。単一の定義元はCore.MasterのBlockMasterが持つ
        // Default destruction category; the single source of truth lives in Core.Master's BlockMaster
        public const string DefaultDestructionCategory = BlockMaster.DefaultDestructionCategory;

        // 破壊カテゴリーを取得する。破壊カテゴリ定義から逆引きし、未定義はdefault扱い
        // Get the destruction category by reverse lookup from the category definitions; unlisted blocks are default
        public static string GetDestructionCategory(this BlockMasterElement blockMasterElement)
        {
            return MasterHolder.BlockMaster.GetDestructionCategory(blockMasterElement.BlockGuid);
        }
    }
}
