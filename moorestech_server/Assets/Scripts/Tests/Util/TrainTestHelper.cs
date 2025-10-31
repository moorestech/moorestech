using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Server.Boot;
using Tests.Module.TestMod;
using Core.Master;
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
            var environment = new TrainTestEnvironment(serviceProvider, ServerContext.WorldBlockDatastore);

#if UNITY_INCLUDE_TESTS
            TrainUpdateService.Instance.ResetTickAccumulator();
#endif
            return environment;
        }

        public static IBlock PlaceBlock(TrainTestEnvironment environment, BlockId blockId, Vector3Int position,
            BlockDirection direction)
        {
            environment.WorldBlockDatastore.TryAddBlock(blockId, position, direction, out var block, System.Array.Empty<BlockCreateParam>());
            return block;
        }

        public static (IBlock Block, TComponent Component) PlaceBlockWithComponent<TComponent>(
            TrainTestEnvironment environment,
            BlockId blockId,
            Vector3Int position,
            BlockDirection direction)
            where TComponent : class, IBlockComponent
        {
            var block = PlaceBlock(environment, blockId, position, direction);
            return (block, block?.ComponentManager.GetComponent<TComponent>());
        }

        public static RailComponent PlaceRail(TrainTestEnvironment environment, Vector3Int position, BlockDirection direction)
        {
            var (_, component) = PlaceBlockWithComponent<RailComponent>(environment, ForUnitTestModBlockId.TestTrainRail, position, direction);
            return component;
        }

        public static RailComponent PlaceRail(TrainTestEnvironment environment, Vector3Int position, BlockDirection direction,
            out IBlock railBlock)
        {
            var (block, component) = PlaceBlockWithComponent<RailComponent>(environment, ForUnitTestModBlockId.TestTrainRail, position, direction);
            railBlock = block;
            return component;
        }
    }
}
