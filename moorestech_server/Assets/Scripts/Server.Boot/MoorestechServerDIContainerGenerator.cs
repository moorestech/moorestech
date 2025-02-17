using System.Collections.Generic;
using System.IO;
using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Event;
using Game.Block.Factory;
using Game.Block.Interface;
using Game.Block.Interface.Event;
using Game.Challenge;
using Game.Context;
using Game.CraftChainer;
using Game.CraftChainer.Util;
using Game.Crafting.Interface;
using Game.EnergySystem;
using Game.Entity;
using Game.Entity.Interface;
using Game.Gear.Common;
using Game.Map;
using Game.Map.Interface.Json;
using Game.Map.Interface.MapObject;
using Game.Map.Interface.Vein;
using Game.PlayerInventory;
using Game.PlayerInventory.Event;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.Train.RailGraph;
using Game.World;
using Game.World.DataStore;
using Game.World.DataStore.WorldSettings;
using Game.World.EventHandler.EnergyEvent;
using Game.World.EventHandler.EnergyEvent.EnergyService;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json;
using Server.Event;
using Server.Event.EventReceive;
using Server.Protocol;

namespace Server.Boot
{
    public class MoorestechServerDIContainerGenerator
    {
        //TODO セーブファイルのディレクトリもここで指定できるようにする
        public (PacketResponseCreator, ServiceProvider) Create(string serverDirectory)
        {
            GameUpdater.ResetUpdate();
            
            //必要な各種インスタンスを手動で作成
            var modDirectory = Path.Combine(serverDirectory, "mods");
            
            // マスターをロード
            var modResource = new ModsResource(modDirectory);
            Dictionary<string, MasterJsonCpntens> configJsons = ModJsonStringLoader.GetConfigString(modResource);
            var configJsonFileContainer = new MasterJsonFileContainer(configJsons);
            MasterHolder.Load(configJsonFileContainer);
            
            var initializerCollection = new ServiceCollection();
            initializerCollection.AddSingleton(configJsonFileContainer);
            initializerCollection.AddSingleton<IItemStackFactory, ItemStackFactory>();
            initializerCollection.AddSingleton<VanillaIBlockTemplates, VanillaIBlockTemplates>();
            initializerCollection.AddSingleton<IBlockFactory, BlockFactory>();
            
            initializerCollection.AddSingleton<IWorldBlockDatastore, WorldBlockDatastore>();
            initializerCollection.AddSingleton<IWorldBlockUpdateEvent, WorldBlockUpdateEvent>();
            initializerCollection.AddSingleton<IBlockOpenableInventoryUpdateEvent, BlockOpenableInventoryUpdateEvent>();
            initializerCollection.AddSingleton<GearNetworkDatastore>();
            initializerCollection.AddSingleton<RailGraphDatastore>();
            
            var mapPath = Path.Combine(serverDirectory, "map", "map.json");
            initializerCollection.AddSingleton(JsonConvert.DeserializeObject<MapInfoJson>(File.ReadAllText(mapPath)));
            initializerCollection.AddSingleton<IMapVeinDatastore, MapVeinDatastore>();
            initializerCollection.AddSingleton<IMapObjectDatastore, MapObjectDatastore>();
            initializerCollection.AddSingleton<IMapObjectFactory, MapObjectFactory>();
            
            var initializerProvider = initializerCollection.BuildServiceProvider();
            var serverContext = new ServerContext(initializerProvider);
            
            
            //コンフィグ、ファクトリーのインスタンスを登録
            var services = new ServiceCollection();
            
            //ゲームプレイに必要なクラスのインスタンスを生成
            services.AddSingleton<EventProtocolProvider, EventProtocolProvider>();
            services.AddSingleton<IWorldSettingsDatastore, WorldSettingsDatastore>();
            services.AddSingleton<IPlayerInventoryDataStore, PlayerInventoryDataStore>();
            services.AddSingleton<IBlockInventoryOpenStateDataStore, BlockInventoryOpenStateDataStore>();
            services.AddSingleton<IWorldEnergySegmentDatastore<EnergySegment>, WorldEnergySegmentDatastore<EnergySegment>>();
            services.AddSingleton<MaxElectricPoleMachineConnectionRange, MaxElectricPoleMachineConnectionRange>();
            services.AddSingleton<IEntitiesDatastore, EntitiesDatastore>();
            services.AddSingleton<IEntityFactory, EntityFactory>(); // TODO これを削除してContext側に加える？
            services.AddSingleton<GearNetworkDatastore>();
            services.AddSingleton<RailGraphDatastore>();
            
            services.AddSingleton<ItemRecipeViewerDataContainer>();
            
            services.AddSingleton(configJsonFileContainer);
            services.AddSingleton<ChallengeDatastore, ChallengeDatastore>();
            services.AddSingleton<ChallengeEvent, ChallengeEvent>();
            
            //JSONファイルのセーブシステムの読み込み
            services.AddSingleton(modResource);
            services.AddSingleton<IWorldSaveDataSaver, WorldSaverForJson>();
            services.AddSingleton<IWorldSaveDataLoader, WorldLoaderFromJson>();
            services.AddSingleton(new SaveJsonFileName("save_1.json"));
            
            //イベントを登録
            services.AddSingleton<IMainInventoryUpdateEvent, MainInventoryUpdateEvent>();
            services.AddSingleton<IGrabInventoryUpdateEvent, GrabInventoryUpdateEvent>();
            services.AddSingleton<CraftEvent, CraftEvent>();
            
            //イベントレシーバーを登録
            services.AddSingleton<ChangeBlockStateEventPacket>();
            services.AddSingleton<MainInventoryUpdateEventPacket>();
            services.AddSingleton<OpenableBlockInventoryUpdateEventPacket>();
            services.AddSingleton<GrabInventoryUpdateEventPacket>();
            services.AddSingleton<PlaceBlockEventPacket>();
            services.AddSingleton<RemoveBlockToSetEventPacket>();
            services.AddSingleton<CompletedChallengeEventPacket>();
            
            services.AddSingleton<EnergyConnectUpdaterContainer<EnergySegment, IElectricConsumer, IElectricGenerator, IElectricTransformer>>();
            
            services.AddSingleton<MapObjectUpdateEventPacket>();
            
            //データのセーブシステム
            services.AddSingleton<AssembleSaveJsonText, AssembleSaveJsonText>();
            
            
            var serviceProvider = services.BuildServiceProvider();
            var packetResponse = new PacketResponseCreator(serviceProvider);
            
            //イベントレシーバーをインスタンス化する
            //TODO この辺を解決するDIコンテナを探す VContinerのRegisterEntryPoint的な
            serviceProvider.GetService<MainInventoryUpdateEventPacket>();
            serviceProvider.GetService<OpenableBlockInventoryUpdateEventPacket>();
            serviceProvider.GetService<GrabInventoryUpdateEventPacket>();
            serviceProvider.GetService<PlaceBlockEventPacket>();
            serviceProvider.GetService<RemoveBlockToSetEventPacket>();
            serviceProvider.GetService<CompletedChallengeEventPacket>();
            
            serviceProvider.GetService<GearNetworkDatastore>();
            serviceProvider.GetService<RailGraphDatastore>();
            serviceProvider.GetService<EnergyConnectUpdaterContainer<EnergySegment, IElectricConsumer, IElectricGenerator, IElectricTransformer>>();
            
            serviceProvider.GetService<ChangeBlockStateEventPacket>();
            serviceProvider.GetService<MapObjectUpdateEventPacket>();
            
            serverContext.SetMainServiceProvider(serviceProvider);
            
            // CraftChainerの初期化
            CraftChainerEntryPoint.Entry();
            
            // アップデート時間をリセット
            GameUpdater.ResetTime();
            
            return (packetResponse, serviceProvider);
        }
    }
}