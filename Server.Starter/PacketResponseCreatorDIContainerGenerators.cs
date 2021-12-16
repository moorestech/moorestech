using Core.Block;
using Core.Block.BlockFactory;
using Core.Block.Config;
using Core.Block.RecipeConfig;
using Core.Item;
using Core.Item.Config;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Game.Save.Interface;
using Game.Save.Json;
using Game.World.Interface;
using Game.World.Interface.Event;
using Microsoft.Extensions.DependencyInjection;
using PlayerInventory;
using PlayerInventory.Event;
using Server.Event;
using Server.Event.EventReceive;
using Server.PacketHandle;
using Server.Protocol;
using World;
using World.Event;

namespace Server
{
    public class PacketResponseCreatorDiContainerGenerators
    {
        public (PacketResponseCreator,ServiceProvider) Create()
        {
            
            var services = new ServiceCollection();
            //テスト用のコンフィグ、ファクトリーのインスタンスを登録
            services.AddSingleton<IMachineRecipeConfig, TestMachineRecipeConfig>();
            services.AddSingleton<IItemConfig, TestItemConfig>();
            services.AddSingleton<ItemStackFactory, ItemStackFactory >();
            services.AddSingleton<IBlockConfig,BlockConfig>();
            services.AddSingleton<VanillaIBlockTemplates, VanillaIBlockTemplates>();
            services.AddSingleton<BlockFactory, BlockFactory>();
            
            
            //必要なクラスのインスタンスを生成
            services.AddSingleton<EventProtocolProvider,EventProtocolProvider>();
            services.AddSingleton<IWorldBlockDatastore,WorldBlockDatastore>();
            services.AddSingleton<IPlayerInventoryDataStore,PlayerInventoryDataStore>();
            
            //JSONファイルのセーブシステムの読み込み
            services.AddSingleton<ISaveRepository, SaveJsonFile>();
            //TODO ファイルパスを変更する
            services.AddSingleton(new SaveJsonFilePath(""));
            
            //イベントを登録
            services.AddSingleton<IBlockPlaceEvent,BlockPlaceEvent>();
            services.AddSingleton<IPlayerInventoryUpdateEvent,PlayerInventoryUpdateEvent>();
            
            //イベントを実際に送信するクラスIPlayerInventoryDataStore
            services.AddSingleton<ReceiveInventoryUpdateEvent,ReceiveInventoryUpdateEvent>();
            services.AddSingleton<ReceivePlaceBlockEvent,ReceivePlaceBlockEvent>();
            
            //データのセーブシステム
            services.AddSingleton<AssembleSaveJsonText,AssembleSaveJsonText>();
            
            
            var serviceProvider = services.BuildServiceProvider();
            var packetResponse = new PacketResponseCreator(serviceProvider);
            
            //イベントをインスタンス化する
            serviceProvider.GetService<ReceiveInventoryUpdateEvent>();
            serviceProvider.GetService<ReceivePlaceBlockEvent>();
            
            return (packetResponse,serviceProvider);
        }
    }
}