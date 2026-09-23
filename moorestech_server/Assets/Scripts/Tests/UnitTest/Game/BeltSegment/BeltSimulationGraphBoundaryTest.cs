using System;
using Game.BeltSegment;
using NUnit.Framework;
using static Tests.UnitTest.Game.BeltSegment.BeltExternalReplayScenario;

namespace Tests.UnitTest.Game.BeltSegment
{
    public sealed class BeltSimulationGraphBoundaryTest
    {
        [TestCase(false), TestCase(true)]
        public void NormalSourceProxyTracksSpeedAndSuccessfulRemoval(bool parallel)
        {
            var receiver = new InspectingReceiver();
            var snapshot = Snapshot(new[] { BeltReplaySegmentState.Normal(1, 64, new[] { State(1, 0) }) },
                Array.Empty<BeltReplayLink>(), Array.Empty<BeltReplayInput>(), new[] { new BeltReplayOutput(0, BeltDirection.Right) });
            var graph = new BeltSimulationGraph(snapshot, Array.Empty<IBeltSource>(), new[] { receiver });
            Assert.That(receiver.Direction, Is.EqualTo(BeltDirection.Left));
            Assert.That(receiver.Source.TryGetOutput(receiver.Direction), Is.False);

            // 拒否後も供給照会は生存。
            // Supply queries remain live after rejection.
            // 次tickの停止を反映。
            // They reflect a stop on the next tick.
            graph.Tick(parallel);
            Assert.That(receiver.ReadyOnReceive, Is.True);
            Assert.That(receiver.Source.TryGetOutput(receiver.Direction), Is.True);
            graph.SetSpeed(0, 0);
            graph.Tick(parallel);
            Assert.That(graph.GetSpeed(0), Is.Zero);
            Assert.That(receiver.Source.TryGetOutput(receiver.Direction), Is.False);

            // 再開搬出は方向・長さを維持。
            // Restarted output preserves direction and length.
            // 同一品を搬出し供給元を空にする。
            // It removes the same item from the source.
            receiver.SetAccept(true);
            graph.SetSpeed(0, 128);
            graph.Tick(parallel);
            Assert.That(receiver.Length, Is.EqualTo(128));
            Assert.That(receiver.Item.Guid, Is.EqualTo(State(1, 0).Item.Guid));
            Assert.That(receiver.Source.TryGetOutput(receiver.Direction), Is.False);
            Assert.That(graph.CaptureSnapshot().Segments[0].Items, Is.Empty);
        }

        [TestCase(false), TestCase(true)]
        public void BufferSourceProxiesPreserveDirectionPriorityAndLiveState(bool parallel)
        {
            var receivers = new[] { new InspectingReceiver(), new InspectingReceiver() };
            var snapshot = Snapshot(new[] { BeltReplaySegmentState.Branch(1, 64, 1, Array.Empty<BeltItemState>(), State(1, 0).Item) },
                Array.Empty<BeltReplayLink>(), new[] { new BeltReplayInput(0, BeltDirection.Back) },
                new[] { new BeltReplayOutput(0, BeltDirection.Right), new BeltReplayOutput(0, BeltDirection.Front) });
            var graph = new BeltSimulationGraph(snapshot, new[] { new Source() }, receivers);
            Assert.That(receivers[0].Direction, Is.EqualTo(BeltDirection.Left));
            Assert.That(receivers[1].Direction, Is.EqualTo(BeltDirection.Back));

            // proxyは問い合わせ方向を保持。
            // Proxies retain query direction.
            // 保存RRのみ供給候補。
            // Only saved RR is offered.
            Assert.That(receivers[0].Source.TryGetOutput(BeltDirection.Left), Is.False);
            Assert.That(receivers[0].Source.TryGetOutput(BeltDirection.Back), Is.True);
            Assert.That(receivers[1].Source.TryGetOutput(BeltDirection.Back), Is.True);
            receivers[1].SetAccept(true);
            graph.Tick(parallel);
            Assert.That(receivers[1].ReadyOnReceive, Is.True);
            Assert.That(receivers[1].Length, Is.EqualTo(64));
            Assert.That(graph.CaptureSnapshot().Segments[0].PriorityIndex, Is.Zero);
            Assert.That(receivers[0].Source.TryGetOutput(BeltDirection.Left), Is.False);

            // 次回も保持proxyを使う。
            // Use the retained proxy again.
            // 新bufferとRRを参照。
            // It reads the new buffer and RR.
            Assert.That(graph.TryInsert(0, 256, State(2, 0).Item), Is.True);
            receivers[0].SetAccept(true);
            graph.Tick(parallel);
            Assert.That(receivers[0].ReadyOnReceive, Is.True);
            Assert.That(receivers[0].Item.Guid, Is.EqualTo(State(2, 0).Item.Guid));
            Assert.That(graph.CaptureSnapshot().Segments[0].PriorityIndex, Is.EqualTo(1));
        }

        [TestCase(false), TestCase(true)]
        public void GraphOwnsTopologyAndKeepsNormalTransfersSingleStep(bool parallel)
        {
            var receiver = new InspectingReceiver();
            var snapshot = Snapshot(new[]
            {
                BeltReplaySegmentState.Normal(2, 64, new[] { State(1, 0) }),
                BeltReplaySegmentState.Normal(2, 64, Array.Empty<BeltItemState>())
            }, new[] { new BeltReplayLink(0, 1, BeltDirection.Front) },
                new[] { new BeltReplayInput(0, BeltDirection.Back) }, new[] { new BeltReplayOutput(1, BeltDirection.Right) });
            var graph = new BeltSimulationGraph(snapshot, new[] { new Source() }, new[] { receiver });
            var captured = graph.CaptureSnapshot();
            Corrupt(snapshot); Corrupt(captured);

            // 元の内部接続を維持。
            // Preserve the original internal link.
            // 配列変更後も一度だけ搬送。
            // Transfer only once after array changes.
            graph.Tick(parallel);
            var result = graph.CaptureSnapshot();
            Assert.That(graph.SegmentCount, Is.EqualTo(2));
            Assert.That(result.Links[0], Is.EqualTo(new BeltReplayLink(0, 1, BeltDirection.Front)));
            Assert.That(result.Outputs[0], Is.EqualTo(new BeltReplayOutput(1, BeltDirection.Right)));
            Assert.That(result.Segments[0].Items, Is.Empty);
            Assert.That(result.Segments[1].Items[0].DistanceToExit, Is.EqualTo(448));
            Assert.That(result.Segments[1].Items[0].Item.Guid, Is.EqualTo(State(1, 0).Item.Guid));

            // 入力IDはGraph表を使う。
            // Input IDs use graph-owned entries.
            // 可変Coreは公開しない。
            // Mutable Core stays private.
            Assert.That(graph.GetInputOffer(0), Is.EqualTo(512));
            Assert.That(graph.TryInsert(0, 64, State(2, 0).Item), Is.True);
            Assert.That(graph.CaptureSnapshot().Segments[0].Items[0].Item.Guid, Is.EqualTo(State(2, 0).Item.Guid));
            Assert.That(typeof(BeltSimulationGraph).GetProperty("Segments"), Is.Null);
            Assert.That(typeof(BeltSimulationGraph).GetProperty("Simulation"), Is.Null);

            #region Internal
            void Corrupt(BeltReplaySnapshot value)
            {
                value.Links[0] = new BeltReplayLink(1, 0, BeltDirection.Left);
                value.Inputs[0] = new BeltReplayInput(1, BeltDirection.Left);
                value.Outputs[0] = new BeltReplayOutput(0, BeltDirection.Left);
                value.Segments[0] = BeltReplaySegmentState.Normal(1, 0, Array.Empty<BeltItemState>());
            }
            #endregion
        }

        [TestCase(0), TestCase(257)]
        public void GraphRejectsInvalidBoundaryInsertionLength(int length)
        {
            var snapshot = Snapshot(new[] { BeltReplaySegmentState.Normal(1, 64, Array.Empty<BeltItemState>()) },
                Array.Empty<BeltReplayLink>(), new[] { new BeltReplayInput(0, BeltDirection.Back) }, Array.Empty<BeltReplayOutput>());
            var graph = new BeltSimulationGraph(snapshot, new[] { new Source() }, Array.Empty<IBeltReceiver>());
            Assert.Throws<ArgumentOutOfRangeException>(() => graph.TryInsert(0, length, State(1, 0).Item));
            Assert.That(graph.CaptureSnapshot().Segments[0].Items, Is.Empty);
        }

        private sealed class InspectingReceiver : IBeltReceiver
        {
            private bool accept;
            internal IBeltSource Source { get; private set; }
            internal BeltDirection Direction { get; private set; }
            internal bool ReadyOnReceive { get; private set; }
            internal int Length { get; private set; }
            internal BeltItem Item { get; private set; }
            internal void SetAccept(bool value) => accept = value;

            public void AttachInput(IBeltSource source, BeltDirection inputDirection)
            {
                // 外部参照から再配線不可。
                // External references cannot rewire.
                // 列・buffer操作も不可。
                // They cannot mutate queues or buffers.
                Assert.That(source, Is.Not.InstanceOf<BeltConveyorSegment>());
                Assert.That(source, Is.Not.InstanceOf<BeltBuffer>());
                Assert.That(source, Is.Not.InstanceOf<IBeltReceiver>());
                Source = source;
                Direction = inputDirection;
            }

            public int GetOffer(BeltDirection inputDirection)
            {
                Assert.That(inputDirection, Is.EqualTo(Direction));
                return 256;
            }

            public bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item)
            {
                Assert.That(inputDirection, Is.EqualTo(Direction));
                ReadyOnReceive = Source.TryGetOutput(inputDirection);
                Length = length;
                Item = item;
                return accept;
            }
        }
    }
}
