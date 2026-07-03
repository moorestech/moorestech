using Mooresmaster.Model.BlocksModule;

namespace Client.Game.Common
{
    public static class BlockMasterElementExtension
    {
        // カテゴリー未設定ブロックの破壊カテゴリー。デフォルト同士は従来どおり複数選択可能
        // Destruction category for blocks with none set; defaults can still be multi-selected together
        public const string DefaultDestructionCategory = "default";

        public static bool IsBlockOpenable(this BlockMasterElement blockMasterElement)
        {
            // UIのAddressableのパスが指定してあれば開けると判断する
            // If the UI's Addressable path is specified, it is judged that it can be opened
            return !string.IsNullOrEmpty(blockMasterElement.BlockUIAddressablesPath);
        }

        // 破壊カテゴリーを取得する。未設定はdefault扱い
        // Get the destruction category; treat an unset value as default
        public static string GetDestructionCategory(this BlockMasterElement blockMasterElement)
        {
            return string.IsNullOrEmpty(blockMasterElement.DestructionCategory)
                ? DefaultDestructionCategory
                : blockMasterElement.DestructionCategory;
        }
    }
}
