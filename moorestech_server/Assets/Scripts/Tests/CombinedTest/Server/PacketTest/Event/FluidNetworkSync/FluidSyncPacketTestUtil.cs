using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid.Sync.Replica;
using Game.Fluid.Sync.State;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Loop;
using Server.Event;
using Server.Event.EventReceive.FluidNetworkSync;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest.Event.FluidNetworkSync
{
    // 流体同期パケットのテスト用に、ポンプがパイプへ押し込みボイドが吸う世界の構築とイベントの仕分けをまとめる
    // Builds a world where a pump pushes into pipes and a void drains them for the fluid sync packet tests, and classifies events
    public static class FluidSyncPacketTestUtil
    {
        public const int WarmUpTicks = 8;
        // 歯車ポンプの西側へ伸ばすパイプ列。末端の北隣にボイドを置く(ボイドの受け入れ面は-Z側)
        // The pipe run extending west of the gear pump; the void sits north of the far end (its inflow face is on -Z)
        public const int PipeRunLength = 3;

        public static ServiceProvider CreateServer()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return serviceProvider;
        }

        // 無限トルクの歯車発電機→歯車ポンプ→パイプ3本→ボイドを置き、ポンプが押し込みボイドが吸う状態まで回す
        // Place an infinite-torque gear generator, a gear pump, three pipes and a void, then tick until the pump pushes and the void drains
        public static void BuildPumpFedWorldAndWarmUp()
        {
            var world = ServerContext.WorldBlockDatastore;
            Place(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East);
            Place(ForUnitTestModBlockId.GearPump, Vector3Int.zero, BlockDirection.North);
            for (var x = 1; x <= PipeRunLength; x++) Place(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-x, 0, 0), BlockDirection.North);
            Place(ForUnitTestModBlockId.VoidPipe, new Vector3Int(-PipeRunLength, 0, 1), BlockDirection.North);
            for (var i = 0; i < WarmUpTicks; i++) GameUpdater.UpdateOneTick();

            Assert.Greater(world.GetBlock(new Vector3Int(-1, 0, 0)).GetComponent<FluidPipeComponent>().Node.Amount, 0, "the pump pushed into the first pipe during the warm-up");
        }

        // パイプ列を西へ1本延ばす。設置で網が汚れ、tick末尾で作り直される
        // Extend the pipe run one cell further west; the placement dirties the network, rebuilt at the tick end
        public static void ExtendPipeRun(int x)
        {
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-x, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _), $"placed pipe at x={-x}");
        }

        public static FluidNetworkFullState CaptureServer()
        {
            return ServerContext.GetService<FluidNetworkDatastore>().CaptureFullState();
        }

        public static uint HashOf(FluidNetworkReplica replica)
        {
            return FluidNetworkStateHash.Compute(replica.CaptureFullState());
        }

        public static bool IsFluidTickEvent(EventMessagePack eventMessagePack)
        {
            return eventMessagePack.Tag == FluidNetworkTickEventPacket.TickDiffEventTag || eventMessagePack.Tag == FluidNetworkTickEventPacket.RebuiltFullStateEventTag;
        }

        // 他スレッドから積まれた操作としてtick末尾にパイプを置く。本番の設置と同じ位置(操作適用→再構築→ハッシュ)で網が作り直される
        // Places a pipe at the tick end as work queued from another thread; the network is rebuilt at the same point as a real placement (apply, rebuild, hash)
        public sealed class ExtendPipeRunAtTickEnd : IServerThreadAction
        {
            private readonly int _x;

            public ExtendPipeRunAtTickEnd(int x)
            {
                _x = x;
            }

            public void Run()
            {
                ExtendPipeRun(_x);
            }

            public void OnServerStopped()
            {
            }
        }

        private static void Place(BlockId blockId, Vector3Int position, BlockDirection direction)
        {
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(blockId, position, direction, Array.Empty<BlockCreateParam>(), out _), $"placed {blockId} at {position}");
        }
    }
}
