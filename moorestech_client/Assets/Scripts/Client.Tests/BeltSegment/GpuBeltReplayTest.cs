using System;
using Client.Game.InGame.BeltSegment.Gpu;
using Game.BeltSegment;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BeltSegment
{
    public sealed class GpuBeltReplayTest
    {
        static readonly BeltItemState[] empty = Array.Empty<BeltItemState>();
        static readonly BeltReplaySpeedChange[] noSpeeds = Array.Empty<BeltReplaySpeedChange>();
        static readonly int[] noIds = Array.Empty<int>();
        static readonly BeltReplayInsertion[] noInsertions = Array.Empty<BeltReplayInsertion>();

        [Test]
        public void EmptyGraphAndRebuild()
        {
            var snapshot = Snapshot(Array.Empty<BeltReplaySegmentState>());
            var shader = Shader();
            using (var gpu = new GpuBeltSimulation(snapshot, shader))
            {
                gpu.ApplyTick(Tick());
                GpuBeltReplayReadback.AssertMatches(snapshot, gpu);
            }
            using (var gpu = new GpuBeltSimulation(snapshot, shader))
            {
                gpu.ApplyTick(Tick());
                GpuBeltReplayReadback.AssertMatches(snapshot, gpu);
            }
        }

        [Test]
        public void BoundaryInsertionWaitsUntilNextTick()
        {
            var snapshot = new BeltReplaySnapshot(new[] { BeltReplaySegmentState.Normal(2, 64, empty) },
                Array.Empty<BeltReplayLink>(), new[] { new BeltReplayInput(0, BeltDirection.Back) },
                Array.Empty<BeltReplayOutput>());
            using (var gpu = new GpuBeltSimulation(snapshot, Shader()))
            {
                var cpu = new BeltReplaySimulation(snapshot);
                var frame = Tick(insertions: new[] { Insert(0, 64, 7) });
                Apply(cpu, gpu, frame);
                Assert.That(cpu.CaptureSnapshot().Segments[0].Items[0].DistanceToExit, Is.EqualTo(448));
                Apply(cpu, gpu, Tick());
                Assert.That(cpu.CaptureSnapshot().Segments[0].Items[0].DistanceToExit, Is.EqualTo(384));
            }
        }

        [Test]
        public void NormalSelfLinkAndFullStop()
        {
            var snapshot = new BeltReplaySnapshot(new[]
            {
                BeltReplaySegmentState.Normal(4, 64, new[] { State(1, 32) })
            }, new[] { new BeltReplayLink(0, 0, BeltDirection.Front) },
                Array.Empty<BeltReplayInput>(), Array.Empty<BeltReplayOutput>());
            using (var gpu = new GpuBeltSimulation(snapshot, Shader()))
            {
                var cpu = new BeltReplaySimulation(snapshot);
                Apply(cpu, gpu, Tick());
                Assert.That(cpu.CaptureSnapshot().Segments[0].Items[0].DistanceToExit, Is.EqualTo(992));
            }
            snapshot = new BeltReplaySnapshot(new[]
            {
                BeltReplaySegmentState.Normal(2, 128, new[] { State(1, 0), State(2, 256) })
            }, new[] { new BeltReplayLink(0, 0, BeltDirection.Front) },
                Array.Empty<BeltReplayInput>(), Array.Empty<BeltReplayOutput>());
            using (var gpu = new GpuBeltSimulation(snapshot, Shader()))
            {
                var cpu = new BeltReplaySimulation(snapshot);
                Apply(cpu, gpu, Tick());
                Assert.That(cpu.CaptureSnapshot().Segments[0].Items[0].DistanceToExit, Is.Zero);
                Assert.That(cpu.CaptureSnapshot().Segments[0].Items[1].DistanceToExit, Is.EqualTo(256));
            }
        }

        [Test]
        public void RejectsFullNormalLengthBeforeAdvance()
        {
            var snapshot = new BeltReplaySnapshot(new[]
            {
                BeltReplaySegmentState.Normal(2, 128, new[] { State(1, 0) }),
                BeltReplaySegmentState.Normal(2, 0, new[] { State(2, 192) })
            }, new[] { new BeltReplayLink(0, 1, BeltDirection.Front) },
                Array.Empty<BeltReplayInput>(), Array.Empty<BeltReplayOutput>());
            using (var gpu = new GpuBeltSimulation(snapshot, Shader()))
            {
                var cpu = new BeltReplaySimulation(snapshot);
                Apply(cpu, gpu, Tick());
                Assert.That(cpu.CaptureSnapshot().Segments[0].Items[0].DistanceToExit, Is.Zero);
            }
        }

        [Test]
        public void BranchBufferMovesZeroKind()
        {
            var snapshot = new BeltReplaySnapshot(new[]
            {
                BeltReplaySegmentState.Branch(1, 64, 0, empty, new BeltItem { ItemId = 0 })
            }, Array.Empty<BeltReplayLink>(), Array.Empty<BeltReplayInput>(),
                new[] { new BeltReplayOutput(0, BeltDirection.Front) });
            using (var gpu = new GpuBeltSimulation(snapshot, Shader()))
            {
                var cpu = new BeltReplaySimulation(snapshot);
                Apply(cpu, gpu, Tick(outputs: new[] { 0 }));
                Assert.That(cpu.CaptureSnapshot().Segments[0].BufferedItem.HasValue, Is.False);
                Apply(cpu, gpu, Tick());
            }
        }

        [Test]
        public void LastSpeedWinsForRepeatedSegmentId()
        {
            var snapshot = new BeltReplaySnapshot(new[]
            {
                BeltReplaySegmentState.Normal(2, 64, new[] { State(0, 0) })
            }, Array.Empty<BeltReplayLink>(), Array.Empty<BeltReplayInput>(),
                new[] { new BeltReplayOutput(0, BeltDirection.Front) });
            using (var gpu = new GpuBeltSimulation(snapshot, Shader()))
            {
                var cpu = new BeltReplaySimulation(snapshot);
                Apply(cpu, gpu, new BeltReplayTick(new[]
                {
                    new BeltReplaySpeedChange(0, 128), new BeltReplaySpeedChange(0, 0)
                }, noIds, noIds, noInsertions));
                Assert.That(cpu.CaptureSnapshot().Segments[0].Items[0].Item.ItemId, Is.Zero);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ThousandTicksAndPeriodicRebuild(bool serverParallel)
            => GpuBeltReplayScenario.Run(Shader(), serverParallel);

        internal static BeltReplaySnapshot Snapshot(BeltReplaySegmentState[] segments)
            => new BeltReplaySnapshot(segments, Array.Empty<BeltReplayLink>(),
                Array.Empty<BeltReplayInput>(), Array.Empty<BeltReplayOutput>());
        internal static BeltItemState State(int kind, int distance)
            => new BeltItemState(new BeltItem { ItemId = kind }, distance);
        internal static BeltReplayInsertion Insert(int input, int length, int kind)
            => new BeltReplayInsertion(input, length, new BeltItem { ItemId = kind });
        internal static BeltReplayTick Tick(int[] ready = null, int[] outputs = null,
            BeltReplayInsertion[] insertions = null)
            => new BeltReplayTick(noSpeeds, ready ?? noIds, outputs ?? noIds, insertions ?? noInsertions);
        internal static void Apply(BeltReplaySimulation cpu, GpuBeltSimulation gpu, BeltReplayTick tick)
        {
            cpu.ApplyTick(tick, false);
            gpu.ApplyTick(tick);
            GpuBeltReplayReadback.AssertMatches(cpu.CaptureSnapshot(), gpu);
        }
        internal static ComputeShader Shader()
        {
            Assert.That(SystemInfo.supportsComputeShaders, Is.True, "Compute shaders are required for GPU replay tests.");
            var shader = Resources.Load<ComputeShader>("BeltSegment/BeltGpuReplay");
            Assert.That(shader, Is.Not.Null);
#if UNITY_EDITOR
            foreach (var message in UnityEditor.ShaderUtil.GetComputeShaderMessages(shader))
            {
                TestContext.WriteLine($"compute {message.severity}: {message.message}");
                Assert.That(message.severity.ToString(), Is.Not.EqualTo("Error"), message.message);
            }
#endif
            return shader;
        }
    }
}
