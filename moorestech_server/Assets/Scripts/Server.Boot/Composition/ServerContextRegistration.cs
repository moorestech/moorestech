using System.IO;
using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Action;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Blocks.Fluid;
using Game.Block.Event;
using Game.Block.Factory;
using Game.Block.Interface;
using Game.Block.Interface.Event;
using Game.Blueprint;
using Game.Challenge;
using Game.CleanRoom;
using Game.Construction;
using Server.Protocol.PacketResponse.Util.Construction;
using Game.Context;
using Game.Crafting.Interface;
using Game.EnergySystem;
using Game.Entity;
using Game.Entity.Interface;
using Game.Gear.Common;
using Game.Hotbar;
using Game.Map;
using Game.Map.Interface;
using Game.Map.Interface.Json;
using Game.Map.Interface.MapObject;
using Game.Map.Interface.Vein;
using Game.Block.Interface.Extension;
using Game.PlacementTarget;
using Game.Paths;
using Game.PlayerConnection;
using Game.PlayerInventory;
using Game.PlayerInventory.Event;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Game.PlayerInventory.Interface.Subscription;
using Game.PlayerRiding;
using Game.PlayerRiding.Interface;
using Game.Research;
using Game.SaveLoad;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Json.WorldVersions;
using Game.SaveLoad.Migration;
using Game.SaveLoad.Migration.Steps;
using Game.SaveLoad.Pruning;
using Game.SaveLoad.Snapshot;
using Game.SaveLoad.Writer;
using Game.Train.Diagram;
using Game.Train.Event;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.SaveLoad;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Game.UnlockState;
using Game.World;
using Game.World.DataStore;
using Game.World.DataStore.WorldSettings;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using Game.World.Interface.DataStore;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.DependencyInjection;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json;
using Server.Event;
using Server.Event.EventReceive;
using Server.Event.Notification;
using Server.Event.EventReceive.UnifiedInventoryEvent;
using Server.Boot.Loop.PacketProcessing;
using Server.Boot.Loop;
using Server.Protocol;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Server.Util.MessagePack;

using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;
namespace Server.Boot.Composition
{
    internal static class ServerContextRegistration
    {
        internal static ServiceProvider Create(WorldDataDirectory worldDataDirectory, MasterJsonFileContainer masterJsonFileContainer)
        {
            // ServerContext用のインスタンスを登録
            // Register instances used by ServerContext.
            var initializerCollection = new ServiceCollection();
            initializerCollection.AddSingleton(masterJsonFileContainer);
            initializerCollection.AddSingleton<IItemStackFactory, ItemStackFactory>();
            initializerCollection.AddSingleton<VanillaIBlockTemplates, VanillaIBlockTemplates>();
            initializerCollection.AddSingleton<IBlockFactory, BlockFactory>();

            initializerCollection.AddSingleton<IWorldBlockDatastore, WorldBlockDatastore>();
            initializerCollection.AddSingleton<IWorldBlockUpdateEvent, WorldBlockUpdateEvent>();
            initializerCollection.AddSingleton<IBlockOpenableInventoryUpdateEvent, BlockOpenableInventoryUpdateEvent>();
            initializerCollection.AddSingleton<GearNetworkDatastore>();
            initializerCollection.AddSingleton<FluidNetworkDatastore>();
            initializerCollection.AddSingleton<BeltTransportDatastore>();
            initializerCollection.AddSingleton<CleanRoomDatastore>();
            initializerCollection.AddSingleton<RailGraphDatastore>();
            initializerCollection.AddSingleton<IRailGraphDatastore>(provider => provider.GetService<RailGraphDatastore>());
            initializerCollection.AddSingleton<TrainUnitDatastore>();
            initializerCollection.AddSingleton<ITrainUnitMutationDatastore>(provider => provider.GetService<TrainUnitDatastore>());
            initializerCollection.AddSingleton<ITrainUnitLookupDatastore>(provider => provider.GetService<TrainUnitDatastore>());
            initializerCollection.AddSingleton<TrainDiagramManager>();
            initializerCollection.AddSingleton<TrainRailPositionManager>();
            initializerCollection.AddSingleton<IRailGraphNodeRemovalListener>(provider => provider.GetService<TrainDiagramManager>());
            initializerCollection.AddSingleton<IRailGraphNodeRemovalListener>(provider => provider.GetService<TrainRailPositionManager>());

            var mapPath = worldDataDirectory.MapJsonFilePath;
            initializerCollection.AddSingleton(JsonConvert.DeserializeObject<MapInfoJson>(File.ReadAllText(mapPath)));
            initializerCollection.AddSingleton<IItemMapVeinDatastore, ItemMapVeinDatastore>();
            initializerCollection.AddSingleton<IFluidMapVeinDatastore, FluidMapVeinDatastore>();
            initializerCollection.AddSingleton<IMapObjectDatastore, MapObjectDatastore>();
            initializerCollection.AddSingleton<IMapObjectFactory, MapObjectFactory>();

            var initializerProvider = initializerCollection.BuildServiceProvider();
            return initializerProvider;
        }
    }
}
