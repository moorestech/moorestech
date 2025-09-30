using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Context;
using Game.Train.RailGraph;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.Util
{
    public readonly struct TrainTestEnvironment
    {
        public TrainTestEnvironment(ServiceProvider serviceProvider, IWorldBlockDatastore worldBlockDatastore)
        {
            ServiceProvider = serviceProvider;
            WorldBlockDatastore = worldBlockDatastore;
        }

        public ServiceProvider ServiceProvider { get; }
        public IWorldBlockDatastore WorldBlockDatastore { get; }

        public RailGraphDatastore GetRailGraphDatastore()
        {
            return ServiceProvider.GetService<RailGraphDatastore>();
        }
    }

    public static class TrainTestHelper
    {
        public static TrainTestEnvironment CreateEnvironment()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return new TrainTestEnvironment(serviceProvider, ServerContext.WorldBlockDatastore);
        }

        public static RailComponent PlaceRail(TrainTestEnvironment environment, Vector3Int position, BlockDirection direction)
        {
            environment.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, position, direction, out var railBlock);
            return railBlock.ComponentManager.GetComponent<RailComponent>();
        }
    }
}
