using Core.Block.Config;
using Core.Block.RecipeConfig;
using Core.ConfigJson;
using Core.Item;
using Core.Item.Config;
using Core.Ore;
using Game.Crafting.Interface;
using Microsoft.Extensions.DependencyInjection;
using Server;
using Server.Boot;
using Server.StartServerSystem;

namespace SinglePlay
{
    public class SinglePlayInterface
    {
        public readonly ICraftingConfig CraftingConfig;
        public readonly IMachineRecipeConfig MachineRecipeConfig;
        public readonly IItemConfig ItemConfig;
        public readonly IBlockConfig BlockConfig;
        public readonly IOreConfig OreConfig;
        
        public readonly ConfigJsonList ConfigJsonList;
        
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
        }
    }
}