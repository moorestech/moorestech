using System;
using System.Collections.Generic;
using Core.Const;

namespace Game.Block.Interface.BlockConfig
{
    public interface IBlockConfig
    {
        public IReadOnlyList<BlockConfigData> BlockConfigList { get; }
        
        public BlockConfigData GetBlockConfig(int id);
        public BlockConfigData GetBlockConfig(long blockHash);
        public BlockConfigData GetBlockConfig(string modId, string blockName);
        public int GetBlockConfigCount();
        public List<int> GetBlockIds(string modId);
        
        public bool IsBlock(int itemId);
        public int ItemIdToBlockId(int itemId);
        public BlockConfigData ItemIdToBlockConfig(int itemId);
    }
    
    //TODO
    [Obsolete("実際のBlockConfigに入れる")]
    public class BlockVerticalConfig
    {
        [Obsolete("実際のBlockConfigに入れる")]
        public static readonly Dictionary<(int, BlockVerticalDirection), int> BlockVerticalDictionary = new();
        
        public BlockVerticalConfig(IBlockConfig blockConfig)
        {
            //TODO ここをコンフィグに入れる
            try
            {
                var gearBeltConveyorId = blockConfig.GetBlockConfig(AlphaMod.ModId, "gear belt conveyor").BlockId;
                var gearBeltConveyorUpId = blockConfig.GetBlockConfig(AlphaMod.ModId, "gear belt conveyor up").BlockId;
                BlockVerticalDictionary.Add((gearBeltConveyorId, BlockVerticalDirection.Up), gearBeltConveyorUpId);
                var gearBeltConveyorDownId = blockConfig.GetBlockConfig(AlphaMod.ModId, "gear belt conveyor down").BlockId;
                BlockVerticalDictionary.Add((gearBeltConveyorId, BlockVerticalDirection.Down), gearBeltConveyorDownId);
            }
            catch (Exception e)
            {
                // テストの時はエラーが出るのでtry-catchで囲む
            }
        }
    }
}