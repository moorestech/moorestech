using Core.Block;
using Core.Block.BlockFactory;
using Core.Block.BlockInventory;
using Core.Block.Config;
using Core.Block.RecipeConfig;
using Core.Inventory;
using Core.Item;
using Core.Item.Config;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Game.Save.Interface;
using Game.Save.Json;
using Game.World.Interface;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;
using Microsoft.Extensions.DependencyInjection;
using PlayerInventory;
using PlayerInventory.Event;
using Server.Event;
using Server.Event.EventReceive;
using Server.PacketHandle;
using Server.Protocol;
using World;
using World.DataStore;
using World.Event;
using World.EventListener;

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
            
            
            //ゲームプレイに必要なクラスのインスタンスを生成
            services.AddSingleton<EventProtocolProvider,EventProtocolProvider>();
            services.AddSingleton<IWorldBlockDatastore,WorldBlockDatastore>();
            services.AddSingleton<IWorldBlockComponentDatastore<IBlockInventory>,WorldBlockInventoryDatastore>();
            services.AddSingleton<IPlayerInventoryDataStore,PlayerInventoryDataStore>();
            services.AddSingleton<IWorldElectricSegmentDatastore,WorldElectricSegmentDatastore>();
            
            //JSONファイルのセーブシステムの読み込み
            services.AddSingleton<ISaveRepository, SaveJsonFile>();
            services.AddSingleton<ILoadRepository, LoadJsonFile>();
            services.AddSingleton(new SaveJsonFileName("save_1.json"));
            
            //イベントを登録
            services.AddSingleton<IBlockPlaceEvent,BlockPlaceEvent>();
            services.AddSingleton<IBlockRemoveEvent,BlockRemoveEvent>();
            services.AddSingleton<IPlayerInventoryUpdateEvent,PlayerInventoryUpdateEvent>();

            //イベントレシーバーを登録
            services.AddSingleton<ReceiveInventoryUpdateEvent,ReceiveInventoryUpdateEvent>();
            services.AddSingleton<ReceivePlaceBlockEvent,ReceivePlaceBlockEvent>();
            services.AddSingleton<ReceiveRemoveBlockEvent,ReceiveRemoveBlockEvent>();
            services.AddSingleton<BlockPlaceEventToBlockInventoryConnect,BlockPlaceEventToBlockInventoryConnect>();
            services.AddSingleton<BlockRemoveEventToBlockInventoryDisconnect,BlockRemoveEventToBlockInventoryDisconnect>();
            
            //データのセーブシステム
            services.AddSingleton<AssembleSaveJsonText,AssembleSaveJsonText>();
            
            
            var serviceProvider = services.BuildServiceProvider();
            var packetResponse = new PacketResponseCreator(serviceProvider);
            
            //イベントレシーバーをインスタンス化する
            serviceProvider.GetService<ReceiveInventoryUpdateEvent>();
            serviceProvider.GetService<ReceivePlaceBlockEvent>();
            serviceProvider.GetService<ReceiveRemoveBlockEvent>();
            serviceProvider.GetService<BlockPlaceEventToBlockInventoryConnect>();
            serviceProvider.GetService<BlockRemoveEventToBlockInventoryDisconnect>();
            
            return (packetResponse,serviceProvider);
        }
    }
}