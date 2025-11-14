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
using MessagePack;
using UnityEngine;
using System;
using Server.Protocol;
using UnityEngine.Assertions;

namespace Tests.Util
{
    public readonly struct TrainTestEnvironment
    {
        public TrainTestEnvironment(ServiceProvider serviceProvider, IWorldBlockDatastore worldBlockDatastore, PacketResponseCreator packetResponseCreator)
        {
            ServiceProvider = serviceProvider;
            WorldBlockDatastore = worldBlockDatastore;
            PacketResponseCreator = packetResponseCreator;
        }

        public ServiceProvider ServiceProvider { get; }
        public IWorldBlockDatastore WorldBlockDatastore { get; }
        public PacketResponseCreator PacketResponseCreator { get; }

        public RailGraphDatastore GetRailGraphDatastore()
        {
            return ServiceProvider.GetService<RailGraphDatastore>();
        }
    }

    public static class TrainTestHelper
    {
        public static TrainTestEnvironment CreateEnvironment()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var environment = new TrainTestEnvironment(serviceProvider, ServerContext.WorldBlockDatastore, packet);
            TrainUpdateService.Instance.ResetTrains();
            RailGraphDatastore.ResetInstance();

            //なんであるんだっけ、忘れた。消していいかも(toropippi) TODO
#if UNITY_INCLUDE_TESTS
            TrainUpdateService.Instance.ResetTickAccumulator();
#endif
            return environment;
        }

        public static IBlock PlaceBlock(TrainTestEnvironment environment, BlockId blockId, Vector3Int position,
            BlockDirection direction)
        {
            // テスト設置での生成パラメータを整える
            // Prepare creation parameters for test placement
            var createParams = BuildCreateParams(blockId, direction);
            environment.WorldBlockDatastore.TryAddBlock(blockId, position, direction, createParams, out var block);
            return block;

            #region Internal

            BlockCreateParam[] BuildCreateParams(BlockId targetBlockId, BlockDirection blockDirection)
            {
                // レール系ブロックなら方向情報を付与
                // Attach direction metadata when placing rail blocks
                var blockMasterElement = MasterHolder.BlockMaster.GetBlockMaster(targetBlockId);
                if (!string.Equals(blockMasterElement.BlockType, "TrainRail", StringComparison.Ordinal))
                {
                    return Array.Empty<BlockCreateParam>();
                }

                // BlockDirectionからレール向きを取得
                // Convert block direction into rail heading
                var railVector = RailComponent.ToVector3(blockDirection);
                if (railVector == Vector3.zero)
                {
                    railVector = Vector3.forward;
                }

                var stateDetail = new RailBridgePierComponentStateDetail(railVector);
                var serialized = MessagePackSerializer.Serialize(stateDetail);
                return new[]
                {
                    new BlockCreateParam(RailBridgePierComponentStateDetail.StateDetailKey, serialized)
                };
            }

            #endregion
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

        //ノード同士がつながっているかチェックしてつながってなければアサート
        public static void Node2NodeCheckAndAssert(RailNode start, RailNode end, string nodename_start = "start", string nodename_end = "end")
        {
            //つながったかチェック
            var dist_check = start.GetDistanceToNode(end, UseFindPath: true);
            Assert.AreNotEqual(-1, dist_check, "" + nodename_start + "から" + nodename_end + "までがつながっていません。");
        }
    }
}
