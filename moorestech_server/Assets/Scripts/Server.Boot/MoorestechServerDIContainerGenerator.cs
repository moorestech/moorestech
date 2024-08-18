using System.IO;
using Core.Item;
using Core.Item.Config;
using Core.Item.Interface;
using Core.Item.Interface.Config;
using Core.Master;
using Core.Update;
using Game.Block.Config;
using Game.Block.Event;
using Game.Block.Factory;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Event;
using Game.Block.Interface.RecipeConfig;
using Game.Block.RecipeConfig;
using Game.Challenge;
using Game.Context;
using Game.Crafting.Config;
using Game.Crafting.Interface;
using Game.EnergySystem;
using Game.Entity;
using Game.Entity.Interface;
using Game.Gear.Common;
using Game.Map;
using Game.Map.Config;
using Game.Map.Interface.Config;
using Game.Map.Interface.Json;
using Game.Map.Interface.MapObject;
using Game.Map.Interface.Vein;
using Game.PlayerInventory;
using Game.PlayerInventory.Event;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
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
            var initializerCollection = new ServiceCollection();
            var modDirectory = Path.Combine(serverDirectory, "mods");
            var mapPath = Path.Combine(serverDirectory, "map", "map.json");
            
            var modResource = new ModsResource(modDirectory);
            var configJsons = ModJsonStringLoader.GetConfigString(modResource);
            var configJsonFileContainer = new ConfigJsonFileContainer(configJsons);
            initializerCollection.AddSingleton(configJsonFileContainer);
            initializerCollection.AddSingleton<IItemConfig, ItemConfig>();
            initializerCollection.AddSingleton<IBlockConfig, BlockConfig>();
            initializerCollection.AddSingleton<IMachineRecipeConfig, MachineRecipeConfig>();
            initializerCollection.AddSingleton<ICraftingConfig, CraftConfig>();
            initializerCollection.AddSingleton<IItemStackFactory, ItemStackFactory>();
            initializerCollection.AddSingleton<IMapObjectConfig, MapObjectConfig>();
            initializerCollection.AddSingleton<IChallengeConfig, ChallengeConfig>();
            
            initializerCollection.AddSingleton<VanillaIBlockTemplates, VanillaIBlockTemplates>();
            initializerCollection.AddSingleton<IBlockFactory, BlockFactory>();
            
            initializerCollection.AddSingleton<IWorldBlockDatastore, WorldBlockDatastore>();
            initializerCollection.AddSingleton<IWorldBlockUpdateEvent, WorldBlockUpdateEvent>();
            initializerCollection.AddSingleton<IBlockOpenableInventoryUpdateEvent, BlockOpenableInventoryUpdateEvent>();
            initializerCollection.AddSingleton<GearNetworkDatastore>();
            
            initializerCollection.AddSingleton(JsonConvert.DeserializeObject<MapInfoJson>(File.ReadAllText(mapPath)));
            initializerCollection.AddSingleton<IMapVeinDatastore, MapVeinDatastore>();
            
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
            
            services.AddSingleton<IMapObjectDatastore, MapObjectDatastore>();
            services.AddSingleton<IMapObjectFactory, MapObjectFactory>();
            
            services.AddSingleton(configJsonFileContainer);
            services.AddSingleton<ChallengeDatastore, ChallengeDatastore>();
            services.AddSingleton<ChallengeEvent, ChallengeEvent>();
            
            //JSONファイルのセーブシステムの読み込み
            services.AddSingleton(modResource);
            services.AddSingleton<IWorldSaveDataSaver, WorldSaverForJson>();
            services.AddSingleton<IWorldSaveDataLoader, WorldLoaderFromJson>();
            services.AddSingleton(new SaveJsonFileName("save_1.json"));
            services.AddSingleton(JsonConvert.DeserializeObject<MapInfoJson>(File.ReadAllText(mapPath)));
            
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
            serviceProvider.GetService<EnergyConnectUpdaterContainer<EnergySegment, IElectricConsumer, IElectricGenerator, IElectricTransformer>>();
            
            serviceProvider.GetService<ChangeBlockStateEventPacket>();
            serviceProvider.GetService<MapObjectUpdateEventPacket>();
            
            serverContext.SetMainServiceProvider(serviceProvider);
            
            // アップデート時間をリセット
            GameUpdater.ResetTime();
            
            return (packetResponse, serviceProvider);
        }
    }
}