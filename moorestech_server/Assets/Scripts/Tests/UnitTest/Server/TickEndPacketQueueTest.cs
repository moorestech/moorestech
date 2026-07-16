using System.Collections.Generic;
using NUnit.Framework;
using Server.Boot.Loop.PacketProcessing;
using Server.Protocol;

namespace Tests.UnitTest.Server
{
    public class TickEndPacketQueueTest
    {
        [Test]
        public void 固定時点までの全接続パケットを到着順に処理する()
        {
            var queue = new TickEndPacketQueue();
            var log = new List<string>();

            queue.Enqueue(new FakeEntry("A1", log, TickEndPacketProcessResult.Completed));
            queue.Enqueue(new FakeEntry("B1", log, TickEndPacketProcessResult.Completed));
            queue.Enqueue(new FakeEntry("A2", log, TickEndPacketProcessResult.Completed));
            queue.FreezeCurrentPackets();
            queue.ProcessFrozenPackets();

            CollectionAssert.AreEqual(new[] { "A1", "B1", "A2" }, log);
        }

        [Test]
        public void 固定後に到着したパケットは次の固定まで処理しない()
        {
            var queue = new TickEndPacketQueue();
            var log = new List<string>();
            queue.Enqueue(new FakeEntry("first", log, TickEndPacketProcessResult.Completed));

            queue.FreezeCurrentPackets();
            queue.Enqueue(new FakeEntry("next", log, TickEndPacketProcessResult.Completed));
            queue.ProcessFrozenPackets();
            CollectionAssert.AreEqual(new[] { "first" }, log);

            queue.FreezeCurrentPackets();
            queue.ProcessFrozenPackets();
            CollectionAssert.AreEqual(new[] { "first", "next" }, log);
        }

        [Test]
        public void 保留パケットと固定済み後続を新着パケットより先に戻す()
        {
            var queue = new TickEndPacketQueue();
            var log = new List<string>();
            var deferred = new DeferredOnceEntry("deferred", log);
            queue.Enqueue(deferred);
            queue.Enqueue(new FakeEntry("frozen-tail", log, TickEndPacketProcessResult.Completed));

            queue.FreezeCurrentPackets();
            queue.Enqueue(new FakeEntry("new-arrival", log, TickEndPacketProcessResult.Completed));
            queue.ProcessFrozenPackets();
            CollectionAssert.AreEqual(new[] { "deferred" }, log);

            queue.FreezeCurrentPackets();
            queue.ProcessFrozenPackets();
            CollectionAssert.AreEqual(new[] { "deferred", "deferred", "frozen-tail", "new-arrival" }, log);
        }

        [Test]
        public void 切断済み接続のパケットは実行しない()
        {
            var queue = new TickEndPacketQueue();
            var log = new List<string>();
            queue.Enqueue(new FakeEntry("inactive", log, TickEndPacketProcessResult.Completed, false));

            queue.FreezeCurrentPackets();
            queue.ProcessFrozenPackets();

            CollectionAssert.IsEmpty(log);
        }

        private sealed class FakeEntry : ITickEndPacketEntry
        {
            private readonly string _name;
            private readonly List<string> _log;
            private readonly TickEndPacketProcessResult _result;
            public bool IsActive { get; }

            public FakeEntry(string name, List<string> log, TickEndPacketProcessResult result) : this(name, log, result, true) { }

            public FakeEntry(string name, List<string> log, TickEndPacketProcessResult result, bool isActive)
            {
                _name = name;
                _log = log;
                _result = result;
                IsActive = isActive;
            }

            public TickEndPacketProcessResult Process()
            {
                _log.Add(_name);
                return _result;
            }
        }

        private sealed class DeferredOnceEntry : ITickEndPacketEntry
        {
            private readonly string _name;
            private readonly List<string> _log;
            private bool _isFirst = true;
            public bool IsActive => true;

            public DeferredOnceEntry(string name, List<string> log)
            {
                _name = name;
                _log = log;
            }

            public TickEndPacketProcessResult Process()
            {
                _log.Add(_name);
                if (!_isFirst) return TickEndPacketProcessResult.Completed;
                _isFirst = false;
                return TickEndPacketProcessResult.Deferred;
            }
        }
    }
}
