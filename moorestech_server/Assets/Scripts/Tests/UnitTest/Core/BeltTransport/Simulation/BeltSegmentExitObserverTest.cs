using System.Collections.Generic;
using Core.BeltTransport;
using NUnit.Framework;
using Tests.UnitTest.Core.BeltTransport.Connection;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;
using static Tests.UnitTest.Core.BeltTransport.Simulation.BeltSimulationTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Simulation
{
    // 搬出の観測者が「相手へ渡せた瞬間」にだけ、設定したhandleで呼ばれるか
    // Whether the exit observer is called only at the moment an item is handed to the target, with the configured handle
    public class BeltSegmentExitObserverTest
    {
        [Test]
        public void 通常segmentは機械へ渡せたtickだけ通知する()
        {
            var segment = CreateNormal(2, W / 2);
            var machine = new FakeBeltReceiver(W, false);
            segment.ConnectTo(machine, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            var observer = new RecordingExitObserver();
            segment.SetExitObserver(observer, 7);
            Restore(segment, (1, W / 2));
            var simulation = new BeltSimulation(new[] { segment });

            // 出口に届くが拒否される間は通知しない
            // No notification while the head reaches the exit but is rejected
            TickTimes(simulation, 2);
            Assert.AreEqual(0, observer.Handles.Count);

            machine.SetAccepts(true);
            simulation.Tick();
            CollectionAssert.AreEqual(new[] { 7 }, observer.Handles);

            // 空になった後は何も起きない
            // Nothing happens once the segment is empty
            TickTimes(simulation, 3);
            Assert.AreEqual(1, observer.Handles.Count);
        }

        [Test]
        public void 通常segment間の搬送は送り元だけが通知する()
        {
            var source = CreateNormal(1, W / 2);
            var target = CreateNormal(2, W / 2);
            source.ConnectTo(target, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            var sourceObserver = new RecordingExitObserver();
            var targetObserver = new RecordingExitObserver();
            source.SetExitObserver(sourceObserver, 1);
            target.SetExitObserver(targetObserver, 2);
            Restore(source, (1, 0));
            var simulation = new BeltSimulation(new[] { source, target });

            simulation.Tick();
            CollectionAssert.AreEqual(new[] { 1 }, sourceObserver.Handles);
            Assert.AreEqual(0, targetObserver.Handles.Count);
            Assert.AreEqual(1, target.Count);
        }

        [Test]
        public void 分岐はbufferへの回収では通知せず相手へ渡せた時に通知する()
        {
            var branch = CreateBranch(1, W / 2, BeltPriority.InitializeFromDirection, BeltDirection.Front);
            var front = new FakeBeltReceiver(W, false);
            branch.Buffer.ConnectTo(front, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            var observer = new RecordingExitObserver();
            branch.SetExitObserver(observer, 3);
            Restore(branch, (1, 0));
            var simulation = new BeltSimulation(new[] { branch });

            // 段階1で回収され、段階3で拒否される
            // Collected in stage 1, rejected in stage 3
            simulation.Tick();
            Assert.AreEqual(1, BufferSerial(branch.Buffer));
            Assert.AreEqual(0, observer.Handles.Count);

            front.SetAccepts(true);
            simulation.Tick();
            CollectionAssert.AreEqual(new[] { 3 }, observer.Handles);
            Assert.AreEqual(-1, BufferSerial(branch.Buffer));
        }

        [Test]
        public void 既定の観測者では何も起きない()
        {
            var segment = CreateNormal(1, W / 2);
            var machine = new FakeBeltReceiver(W, true);
            segment.ConnectTo(machine, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            Restore(segment, (1, 0));
            var simulation = new BeltSimulation(new[] { segment });

            simulation.Tick();
            Assert.AreEqual(1, machine.ReceivedItems.Count);
        }

        private sealed class RecordingExitObserver : IBeltSegmentExitObserver
        {
            public readonly List<int> Handles = new();

            public void OnExited(int handle)
            {
                Handles.Add(handle);
            }
        }
    }
}
