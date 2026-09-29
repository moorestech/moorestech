using System.Collections.Generic;
using NUnit.Framework;
using Server.Boot.Loop.PacketProcessing;

namespace Tests.UnitTest.Server
{
    public class TickEndPacketQueueTest
    {
        [Test]
        public void 固定時点までの全接続パケットを到着順に処理する()
        {
            var queue = new TickEndPacketQueue();
            var log = new List<string>();

            queue.Enqueue(new FakeEntry("A1", log));
            queue.Enqueue(new FakeEntry("B1", log));
            queue.Enqueue(new FakeEntry("A2", log));
            queue.FreezeCurrentPackets();
            queue.ProcessFrozenPackets();

            CollectionAssert.AreEqual(new[] { "A1", "B1", "A2" }, log);
        }

        [Test]
        public void 固定後に到着したパケットは次の固定まで処理しない()
        {
            var queue = new TickEndPacketQueue();
            var log = new List<string>();
            queue.Enqueue(new FakeEntry("first", log));

            queue.FreezeCurrentPackets();
            queue.Enqueue(new FakeEntry("next", log));
            queue.ProcessFrozenPackets();
            CollectionAssert.AreEqual(new[] { "first" }, log);

            queue.FreezeCurrentPackets();
            queue.ProcessFrozenPackets();
            CollectionAssert.AreEqual(new[] { "first", "next" }, log);
        }

        private sealed class FakeEntry : ITickEndPacketEntry
        {
            private readonly string _name;
            private readonly List<string> _log;

            public FakeEntry(string name, List<string> log)
            {
                _name = name;
                _log = log;
            }

            public void Process()
            {
                _log.Add(_name);
            }
        }
    }
}
