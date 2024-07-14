using Game.Context;

namespace Game.Challenge
{
    public class BlockPlaceTaskParam : IChallengeTaskParam
    {
        public const string TaskCompletionType = "blockPlace";
        
        public readonly int BlockId;
        
        public BlockPlaceTaskParam(int blockId)
        {
            BlockId = blockId;
        }
        
        public static IChallengeTaskParam Create(dynamic param)
        {
            string blockModId = param.blockModId;
            string blockName = param.blockName;
            
            var blockId = ServerContext.BlockConfig.GetBlockConfig(blockModId, blockName).BlockId;
            
            return new BlockPlaceTaskParam(blockId);
        }
    }
}