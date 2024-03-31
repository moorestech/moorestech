using Server.Core.Item;
using Server.Core.Item.Config;
using Game.Block.Factory;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.RecipeConfig;
using Game.Crafting.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol;

namespace Mod.Base
{
    public class ServerModEntryInterface
    {
        public readonly IBlockConfig BlockConfig;

        public readonly BlockFactory BlockFactory;

        public readonly ICraftingConfig CraftingConfig;
        public readonly IItemConfig ItemConfig;
        public readonly ItemStackFactory ItemStackFactory;
        public readonly IMachineRecipeConfig MachineRecipeConfig;

        /// <summary>
        ///     パケットを送信することができるインスタンス
        /// </summary>
        public readonly PacketResponseCreator PacketResponseCreator;


        /// <summary>
        ///     各種サービスを取得できるDIコンテナ
        /// </summary>
        public readonly ServiceProvider ServiceProvider;


        public readonly IWorldBlockDatastore WorldBlockDatastore;


        public ServerModEntryInterface(ServiceProvider serviceProvider, PacketResponseCreator packetResponseCreator)
        {
            ServiceProvider = serviceProvider;
            PacketResponseCreator = packetResponseCreator;

            WorldBlockDatastore = serviceProvider.GetRequiredService<IWorldBlockDatastore>();

            CraftingConfig = serviceProvider.GetRequiredService<ICraftingConfig>();
            MachineRecipeConfig = serviceProvider.GetRequiredService<IMachineRecipeConfig>();
            ItemConfig = serviceProvider.GetRequiredService<IItemConfig>();
            ItemStackFactory = serviceProvider.GetRequiredService<ItemStackFactory>();
            BlockConfig = serviceProvider.GetRequiredService<IBlockConfig>();
            BlockFactory = serviceProvider.GetRequiredService<BlockFactory>();
        }
    }
}