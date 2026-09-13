using System;
using System.Collections.Generic;
using Game.Block.Interface.Component;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class BlockComponentStateReaderTest
    {
        private class SampleState
        {
            public int Count;
            public List<string> Names = new();
        }
        
        [Test]
        public void JTokenの値はToObjectで読める()
        {
            var token = JObject.Parse("{\"Count\":3,\"Names\":[\"a\",\"b\"]}");
            var states = new Dictionary<string, object> { { "k", token } };
            var read = BlockComponentStateReader.Read<SampleState>(states, "k");
            Assert.AreEqual(3, read.Count);
            CollectionAssert.AreEqual(new[] { "a", "b" }, read.Names);
        }
        
        [Test]
        public void 同一プロセス内のオブジェクトはそのまま読める()
        {
            var original = new SampleState { Count = 7 };
            var states = new Dictionary<string, object> { { "k", original } };
            Assert.AreSame(original, BlockComponentStateReader.Read<SampleState>(states, "k"));
        }
        
        [Test]
        public void 旧形式の文字列は移行を促す例外になる()
        {
            var states = new Dictionary<string, object> { { "k", "{\"Count\":3}" } };
            var e = Assert.Throws<InvalidOperationException>(() => BlockComponentStateReader.Read<SampleState>(states, "k"));
            StringAssert.Contains("migrate_block_state_objects.py", e.Message);
        }
        
        [Test]
        public void キーが無ければTryReadはfalse()
        {
            var states = new Dictionary<string, object>();
            Assert.IsFalse(BlockComponentStateReader.TryRead<SampleState>(states, "k", out _));
        }
    }
}
