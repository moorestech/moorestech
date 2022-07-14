using Core.Block.BlockFactory;
using Core.Block.BlockInventory;
using Core.Block.Blocks.Miner;
using Core.Block.Config;
using Core.Block.Config.Service;
using Core.Block.Event;
using Core.Block.RecipeConfig;
using Core.ConfigJson;
using Core.Electric;
using Core.Inventory;
using Core.Item;
using Core.Item.Config;
using Core.Ore;
using Core.Ore.Config;
using Game.Crafting;
using Game.Crafting.Config;
using Game.Crafting.Interface;
using Game.Entity;
using Game.Entity.Interface;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Game.Quest;
using Game.Quest.Config;
using Game.Quest.Factory;
using Game.Quest.Interface;
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
using Mod.Config;
using PlayerInventory;
using PlayerInventory.Event;
using Server.Event;
using Server.Event.EventReceive;
using Server.Protocol;
using World.DataStore;
using World.Event;
using World.Service;

namespace Server.Boot
{
    public class PacketResponseCreatorDiContainerGenerators
    {
        public (PacketResponseCreator, ServiceProvider) Create(string modDirectory)
        {
            var services = new ServiceCollection();
            
            //コンフィグ、ファクトリーのインスタンスを登録
            services.AddSingleton(new ConfigJsonList(ModJsonStringLoader.GetConfigString(modDirectory)));
            services.AddSingleton<IMachineRecipeConfig, MachineRecipeConfig>();
            services.AddSingleton<IItemConfig, ItemConfig>();
            services.AddSingleton<ICraftingConfig, CraftConfig>();
            services.AddSingleton<ItemStackFactory, ItemStackFactory>();
            services.AddSingleton<IBlockConfig, BlockConfig>();
            services.AddSingleton<VanillaIBlockTemplates, VanillaIBlockTemplates>();
            services.AddSingleton<BlockFactory, BlockFactory>();
            services.AddSingleton<ItemIdToBlockId, ItemIdToBlockId>();


            //ゲームプレイに必要なクラスのインスタンスを生成
            services.AddSingleton<IIsCreatableJudgementService, IsCreatableJudgementService>();
            services.AddSingleton<EventProtocolProvider, EventProtocolProvider>();
            services.AddSingleton<IWorldBlockDatastore, WorldBlockDatastore>();
            services.AddSingleton<IWorldBlockComponentDatastore<IBlockElectric>, WorldBlockComponentDatastore<IBlockElectric>>();
            services.AddSingleton<IWorldBlockComponentDatastore<IElectricPole>, WorldBlockComponentDatastore<IElectricPole>>();
            services.AddSingleton<IWorldBlockComponentDatastore<IPowerGenerator>, WorldBlockComponentDatastore<IPowerGenerator>>();
            services.AddSingleton<IWorldBlockComponentDatastore<IBlockInventory>, WorldBlockComponentDatastore<IBlockInventory>>();
            services.AddSingleton<IWorldBlockComponentDatastore<IOpenableInventory>, WorldBlockComponentDatastore<IOpenableInventory>>();
            services.AddSingleton<IWorldBlockComponentDatastore<IMiner>, WorldBlockComponentDatastore<IMiner>>();
            services.AddSingleton<IPlayerInventoryDataStore, PlayerInventoryDataStore>();
            services.AddSingleton<IBlockInventoryOpenStateDataStore, BlockInventoryOpenStateDataStore>();
            services.AddSingleton<IWorldElectricSegmentDatastore, WorldElectricSegmentDatastore>();
            services.AddSingleton<MaxElectricPoleMachineConnectionRange, MaxElectricPoleMachineConnectionRange>();
            services.AddSingleton<IOreConfig, OreConfig>();
            services.AddSingleton<VeinGenerator, VeinGenerator>();
            services.AddSingleton<WorldMapTile, WorldMapTile>();
            services.AddSingleton(new Seed(1337));
            services.AddSingleton<IElectricSegmentMergeService, ElectricSegmentMergeService>();
            services.AddSingleton<IEntitiesDatastore, EntitiesDatastore>();
            services.AddSingleton<IEntityFactory, EntityFactory>();
            services.AddSingleton<IQuestDataStore, QuestDatastore>();
            services.AddSingleton<IQuestConfig, QuestConfig>();
            services.AddSingleton<QuestFactory, QuestFactory>();
            

            //JSONファイルのセーブシステムの読み込み
            services.AddSingleton<ISaveRepository, SaveJsonFile>();
            services.AddSingleton<ILoadRepository, LoadJsonFile>();
            services.AddSingleton(new SaveJsonFileName("save_1.json"));

            //イベントを登録
            services.AddSingleton<IBlockPlaceEvent, BlockPlaceEvent>();
            services.AddSingleton<IBlockRemoveEvent, BlockRemoveEvent>();
            services.AddSingleton<IBlockOpenableInventoryUpdateEvent, BlockOpenableInventoryUpdateEvent>();
            services.AddSingleton<IMainInventoryUpdateEvent, MainInventoryUpdateEvent>();
            services.AddSingleton<ICraftInventoryUpdateEvent, CraftInventoryUpdateEvent>();
            services.AddSingleton<IGrabInventoryUpdateEvent, GrabInventoryUpdateEvent>();
            services.AddSingleton<ICraftingEvent, CraftingEvent>();

            //イベントレシーバーを登録
            services.AddSingleton<MainInventoryUpdateToSetEventPacket>();
            services.AddSingleton<CraftingInventoryUpdateToSetEventPacket>();
            services.AddSingleton<OpenableBlockInventoryUpdateToSetEventPacket>();
            services.AddSingleton<GrabInventoryUpdateToSetEventPacket>();
            services.AddSingleton<PlaceBlockToSetEventPacket>();
            services.AddSingleton<RemoveBlockToSetEventPacket>();
            services.AddSingleton<BlockPlaceEventToBlockInventoryConnect>();
            services.AddSingleton<BlockRemoveEventToBlockInventoryDisconnect>();
            services.AddSingleton<ConnectElectricPoleToElectricSegment>();
            services.AddSingleton<DisconnectElectricPoleToFromElectricSegment>();
            services.AddSingleton<ConnectMachineToElectricSegment>();
            services.AddSingleton<SetMiningItemToMiner>();
            services.AddSingleton<DisconnectTwoOreMoreElectricPoleFromSegmentService>();
            services.AddSingleton<DisconnectOneElectricPoleFromSegmentService>();

            //データのセーブシステム
            services.AddSingleton<AssembleSaveJsonText, AssembleSaveJsonText>();


            var serviceProvider = services.BuildServiceProvider();
            var packetResponse = new PacketResponseCreator(serviceProvider);

            //イベントレシーバーをインスタンス化する
            //TODO この辺を解決するDIコンテナを探す VContinerのRegisterEntryPoint的な
            serviceProvider.GetService<MainInventoryUpdateToSetEventPacket>();
            serviceProvider.GetService<CraftingInventoryUpdateToSetEventPacket>();
            serviceProvider.GetService<OpenableBlockInventoryUpdateToSetEventPacket>();
            serviceProvider.GetService<GrabInventoryUpdateToSetEventPacket>();
            serviceProvider.GetService<PlaceBlockToSetEventPacket>();
            serviceProvider.GetService<RemoveBlockToSetEventPacket>();
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