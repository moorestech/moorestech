using Core.Block.BlockFactory;
using Core.Block.BlockInventory;
using Core.Block.Blocks.Miner;
using Core.Block.Config;
using Core.Block.RecipeConfig;
using Core.Electric;
using Core.Inventory;
using Core.Item;
using Core.Item.Config;
using Core.Ore;
using Core.Ore.Config;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Game.Save.Interface;
using Game.Save.Json;
using Game.World.EventHandler;
using Game.World.EventHandler.Service;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;
using Game.World.Interface.Service;
using Game.WorldMap;
using Game.WorldMap.EventListener;
using Microsoft.Extensions.DependencyInjection;
using PlayerInventory;
using PlayerInventory.Event;
using Server.Event;
using Server.Event.EventReceive;
using Server.Protocol;
using World.DataStore;
using World.Event;
using World.Service;

namespace Server
{
    public class PacketResponseCreatorDiContainerGenerators
    {
        public (PacketResponseCreator, ServiceProvider) Create()
        {
            var services = new ServiceCollection();
            //テスト用のコンフィグ、ファクトリーのインスタンスを登録
            services.AddSingleton<IMachineRecipeConfig, TestMachineRecipeConfig>();
            services.AddSingleton<IItemConfig, TestItemConfig>();
            services.AddSingleton<ItemStackFactory, ItemStackFactory>();
            services.AddSingleton<IBlockConfig, BlockConfig>();
            services.AddSingleton<VanillaIBlockTemplates, VanillaIBlockTemplates>();
            services.AddSingleton<BlockFactory, BlockFactory>();


            //ゲームプレイに必要なクラスのインスタンスを生成
            services.AddSingleton<EventProtocolProvider, EventProtocolProvider>();
            services.AddSingleton<IWorldBlockDatastore, WorldBlockDatastore>();
            services.AddSingleton<IWorldBlockComponentDatastore<IBlockElectric>, WorldBlockComponentDatastore<IBlockElectric>>();
            services.AddSingleton<IWorldBlockComponentDatastore<IElectricPole>, WorldBlockComponentDatastore<IElectricPole>>();
            services.AddSingleton<IWorldBlockComponentDatastore<IPowerGenerator>, WorldBlockComponentDatastore<IPowerGenerator>>();
            services.AddSingleton<IWorldBlockComponentDatastore<IBlockInventory>, WorldBlockComponentDatastore<IBlockInventory>>();
            services.AddSingleton<IWorldBlockComponentDatastore<IInventory>, WorldBlockComponentDatastore<IInventory>>();
            services.AddSingleton<IWorldBlockComponentDatastore<IMiner>, WorldBlockComponentDatastore<IMiner>>();
            services.AddSingleton<IPlayerInventoryDataStore, PlayerInventoryDataStore>();
            services.AddSingleton<IWorldElectricSegmentDatastore, WorldElectricSegmentDatastore>();
            services.AddSingleton<MaxElectricPoleMachineConnectionRange, MaxElectricPoleMachineConnectionRange>();
            services.AddSingleton<IOreConfig, OreConfig>();
            services.AddSingleton<VeinGenerator, VeinGenerator>();
            services.AddSingleton(new Seed(1337));
            services.AddSingleton<IElectricSegmentMergeService, ElectricSegmentMergeService>();

            //JSONファイルのセーブシステムの読み込み
            services.AddSingleton<ISaveRepository, SaveJsonFile>();
            services.AddSingleton<ILoadRepository, LoadJsonFile>();
            services.AddSingleton(new SaveJsonFileName("save_1.json"));

            //イベントを登録
            services.AddSingleton<IBlockPlaceEvent, BlockPlaceEvent>();
            services.AddSingleton<IBlockRemoveEvent, BlockRemoveEvent>();
            services.AddSingleton<IPlayerInventoryUpdateEvent, PlayerInventoryUpdateEvent>();

            //イベントレシーバーを登録
            services.AddSingleton<ReceiveInventoryUpdateEvent, ReceiveInventoryUpdateEvent>();
            services.AddSingleton<ReceivePlaceBlockEvent, ReceivePlaceBlockEvent>();
            services.AddSingleton<ReceiveRemoveBlockEvent, ReceiveRemoveBlockEvent>();
            services.AddSingleton<BlockPlaceEventToBlockInventoryConnect, BlockPlaceEventToBlockInventoryConnect>();
            services.AddSingleton<BlockRemoveEventToBlockInventoryDisconnect, BlockRemoveEventToBlockInventoryDisconnect>();
            services.AddSingleton<ConnectElectricPoleToElectricSegment, ConnectElectricPoleToElectricSegment>();
            services.AddSingleton<DisconnectElectricPoleToFromElectricSegment, DisconnectElectricPoleToFromElectricSegment>();
            services.AddSingleton<ConnectMachineToElectricSegment, ConnectMachineToElectricSegment>();
            services.AddSingleton<SetMiningItemToMiner, SetMiningItemToMiner>();
            services.AddSingleton<DisconnectTwoOreMoreElectricPoleFromSegmentService, DisconnectTwoOreMoreElectricPoleFromSegmentService>();
            services.AddSingleton<DisconnectOneElectricPoleFromSegmentService, DisconnectOneElectricPoleFromSegmentService>();

            //データのセーブシステム
            services.AddSingleton<AssembleSaveJsonText, AssembleSaveJsonText>();


            var serviceProvider = services.BuildServiceProvider();
            var packetResponse = new PacketResponseCreator(serviceProvider);

            //イベントレシーバーをインスタンス化する
            serviceProvider.GetService<ReceiveInventoryUpdateEvent>();
            serviceProvider.GetService<ReceivePlaceBlockEvent>();
            serviceProvider.GetService<ReceiveRemoveBlockEvent>();
            serviceProvider.GetService<BlockPlaceEventToBlockInventoryConnect>();
            serviceProvider.GetService<BlockRemoveEventToBlockInventoryDisconnect>();
            serviceProvider.GetService<ConnectElectricPoleToElectricSegment>();
            serviceProvider.GetService<DisconnectElectricPoleToFromElectricSegment>();
            serviceProvider.GetService<ConnectMachineToElectricSegment>();
            serviceProvider.GetService<SetMiningItemToMiner>();

            return (packetResponse, serviceProvider);
        }
    }
}