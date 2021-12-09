using Core.Block.Config;
using Core.Block.RecipeConfig;
using Core.Item;
using Core.Item.Config;
using Microsoft.Extensions.DependencyInjection;
using PlayerInventory;
using PlayerInventory.Event;
using Server.Event;
using Server.Event.EventReceive;
using Server.PacketHandle;
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
            services.AddSingleton<IBlockConfig, TestBlockConfig>();
            services.AddSingleton<IMachineRecipeConfig, TestMachineRecipeConfig>();
            services.AddSingleton<IItemConfig, TestItemConfig>();
            services.AddSingleton<ItemStackFactory, ItemStackFactory >();
            
            
            //必要なクラスのインスタンスを生成
            services.AddSingleton<EventProtocolProvider,EventProtocolProvider>();
            services.AddSingleton<WorldBlockDatastore,WorldBlockDatastore>();
            services.AddSingleton<PlayerInventoryDataStore,PlayerInventoryDataStore>();
            
            //イベントを登録
            services.AddSingleton<BlockPlaceEvent,BlockPlaceEvent>();
            services.AddSingleton<PlayerInventoryUpdateEvent,PlayerInventoryUpdateEvent>();
            
            //イベントを実際に送信するクラス
            services.AddSingleton<ReceiveInventoryUpdateEvent,ReceiveInventoryUpdateEvent>();
            services.AddSingleton<ReceivePlaceBlockEvent,ReceivePlaceBlockEvent>();
            
            
            var serviceProvider = services.BuildServiceProvider();
            var packetResponse = new PacketResponseCreator(serviceProvider);
            
            //イベントをインスタンス化する
            serviceProvider.GetService<ReceiveInventoryUpdateEvent>();
            serviceProvider.GetService<ReceivePlaceBlockEvent>();
            
            return (packetResponse,serviceProvider);
        }
    }
}