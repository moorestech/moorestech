using System.Collections.Generic;
using Core.Block.BlockFactory.BlockTemplate;
using Core.Block.Config;
using Core.Block.RecipeConfig;
using Core.Item;

namespace Core.Block.BlockFactory
{
    /// <summary>
    /// バニラのブロックの全てのテンプレートを作るクラス
    /// </summary>
    public class VanillaIBlockTemplates
    {
        public readonly Dictionary<string, IBlockTemplate> BlockTypesDictionary;

        public VanillaIBlockTemplates(IMachineRecipeConfig machineRecipeConfig, ItemStackFactory itemStackFactory)
        {
            BlockTypesDictionary = new Dictionary<string, IBlockTemplate>();
            BlockTypesDictionary.Add(VanillaBlockType.Block,new NormalMachineTemplate(machineRecipeConfig, itemStackFactory));
        }
    }
}