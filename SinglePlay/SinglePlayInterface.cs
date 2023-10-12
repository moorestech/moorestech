using Game.Block.Config;
using Game.Block.Config.Service;
using Game.Block.RecipeConfig;
using Core.ConfigJson;
using Core.Item;
using Core.Item.Config;
using Core.Ore;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.RecipeConfig;
using Game.Crafting.Interface;
using Game.Quest.Interface;
using Microsoft.Extensions.DependencyInjection;
using Server;
using Server.Boot;


namespace SinglePlay
{
    public class SinglePlayInterface
    {
        public readonly ICraftingConfig CraftingConfig;
        public readonly IMachineRecipeConfig MachineRecipeConfig;
        public readonly IItemConfig ItemConfig;
        public readonly IBlockConfig BlockConfig;
        public readonly IOreConfig OreConfig;
        public readonly IQuestConfig QuestConfig;

        public readonly ConfigJsonList ConfigJsonList;

        public readonly ItemIdToBlockId ItemIdToBlockId;

        public readonly ItemStackFactory ItemStackFactory;


        public SinglePlayInterface(string configPath)
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(configPath);

            CraftingConfig = serviceProvider.GetService<ICraftingConfig>();
            MachineRecipeConfig = serviceProvider.GetService<IMachineRecipeConfig>();
            ItemConfig = serviceProvider.GetService<IItemConfig>();
            ItemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            BlockConfig = serviceProvider.GetService<IBlockConfig>();
            ConfigJsonList = serviceProvider.GetService<ConfigJsonList>();
            OreConfig = serviceProvider.GetService<IOreConfig>();
            QuestConfig = serviceProvider.GetService<IQuestConfig>();
            ItemIdToBlockId = serviceProvider.GetService<ItemIdToBlockId>();
        }
    }
}