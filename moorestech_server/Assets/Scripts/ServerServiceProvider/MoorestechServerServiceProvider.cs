using Core.ConfigJson;
using Core.Item;
using Core.Item.Config;
using Core.Ore;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.RecipeConfig;
using Game.Crafting.Interface;
using Microsoft.Extensions.DependencyInjection;
using Server.Boot;

namespace ServerServiceProvider
{
    public class MoorestechServerServiceProvider
    {
        public readonly IBlockConfig BlockConfig;

        public readonly ConfigJsonList ConfigJsonList;
        public readonly ICraftingConfig CraftingConfig;
        public readonly IItemConfig ItemConfig;

        public readonly ItemStackFactory ItemStackFactory;
        public readonly IMachineRecipeConfig MachineRecipeConfig;
        public readonly IOreConfig OreConfig;

        public readonly ServiceProvider ServiceProvider;

        public MoorestechServerServiceProvider(string serverDirectory)
        {
            var (_, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(serverDirectory);

            ServiceProvider = serviceProvider;
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