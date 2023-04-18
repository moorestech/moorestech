using Core.Block.BlockFactory;
using Core.Block.Config;
using Core.Block.Config.Service;
using Core.Block.RecipeConfig;
using Core.ConfigJson;
using Core.Item;
using Core.Item.Config;
using Core.Ore;
using Game.Crafting.Interface;
using Game.Quest.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol;

namespace Mod.Base
{
    public class ServerModEntryInterface
    {
        /// <summary>
        /// パケットを送信することができるインスタンス
        /// </summary>
        public readonly PacketResponseCreator PacketResponseCreator;
        /// <summary>
        /// 各種サービスを取得できるDIコンテナ
        /// </summary>
        public readonly ServiceProvider ServiceProvider;

        
        
        public readonly IWorldBlockDatastore WorldBlockDatastore;
        
        public readonly ICraftingConfig CraftingConfig;
        public readonly IMachineRecipeConfig MachineRecipeConfig;
        public readonly IItemConfig ItemConfig;
        public readonly IBlockConfig BlockConfig;
        public readonly IOreConfig OreConfig;
        public readonly IQuestConfig QuestConfig;
        
        public readonly BlockFactory BlockFactory;
        public readonly ItemStackFactory ItemStackFactory;


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
            OreConfig = serviceProvider.GetRequiredService<IOreConfig>();
            QuestConfig = serviceProvider.GetRequiredService<IQuestConfig>();
            BlockFactory = serviceProvider.GetRequiredService<BlockFactory>();
        }
    }
}