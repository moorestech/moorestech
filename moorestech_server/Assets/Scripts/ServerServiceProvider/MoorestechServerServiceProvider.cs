using Core.ConfigJson;
using Core.Item.Interface;
using Core.Item.Config;
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

        public readonly ConfigJsonFileContainer ConfigJsonFileContainer;
        public readonly ICraftingConfig CraftingConfig;
        public readonly IItemConfig ItemConfig;

        public readonly IItemStackFactory IItemStackFactory;
        public readonly IMachineRecipeConfig MachineRecipeConfig;

        public readonly ServiceProvider ServiceProvider;

        public MoorestechServerServiceProvider(string serverDirectory)
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(serverDirectory);

            ServiceProvider = serviceProvider;
            CraftingConfig = serviceProvider.GetService<ICraftingConfig>();
            MachineRecipeConfig = serviceProvider.GetService<IMachineRecipeConfig>();
            ItemConfig = serviceProvider.GetService<IItemConfig>();
            IItemStackFactory = serviceProvider.GetService<IItemStackFactory>();
            BlockConfig = serviceProvider.GetService<IBlockConfig>();
            ConfigJsonFileContainer = serviceProvider.GetService<ConfigJsonFileContainer>();
        }
    }
}