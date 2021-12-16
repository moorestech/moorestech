using System.Collections.Generic;
using Core.Block.RecipeConfig;
using Core.Item;

namespace Core.Block
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
            BlockTypesDictionary.Add("Machine",new NormalMachineTemplate(machineRecipeConfig, itemStackFactory));
        }
    }
}