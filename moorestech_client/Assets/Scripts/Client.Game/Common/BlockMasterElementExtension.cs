using Mooresmaster.Model.BlocksModule;

namespace Client.Game.Common
{
    public static class BlockMasterElementExtension
    {
        public static bool IsBlockOpenable(this BlockMasterElement blockMasterElement)
        {
            // UIのAddressableのパスが指定してあれば開けると判断する
            // If the UI's Addressable path is specified, it is judged that it can be opened
            return !string.IsNullOrEmpty(blockMasterElement.BlockUIAddressablesPath);
        }
    }
}