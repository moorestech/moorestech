using System;
using System.Collections.Generic;
using Game.Blueprint;
using MessagePack;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Tests.CombinedTest.Game.Blueprint
{
    public class BlueprintLineSerializationTest
    {
        [Test]
        public void JSONとMessagePackで配線とブロック順が往復するTest()
        {
            var blocks = new List<BlueprintBlockJsonObject>
            {
                new(Vector3Int.zero, Guid.NewGuid().ToString(), 0, new Dictionary<string, string>()),
                new(new Vector3Int(3, 2, 1), Guid.NewGuid().ToString(), 1, new Dictionary<string, string>()),
            };
            var original = new BlueprintJsonObject("lines", blocks,
                new List<BlueprintLineJsonObject> { new(0, 1, Guid.NewGuid()) },
                new List<BlueprintLineJsonObject> { new(1, 0, Guid.NewGuid()) }, Guid.NewGuid());

            // 実際のシリアライザを通して端点と線種を比較する
            // Compare endpoints and tool identities through the actual serializers
            var json = JsonConvert.DeserializeObject<BlueprintJsonObject>(JsonConvert.SerializeObject(original));
            AssertBlueprint(original, json);
            var packet = MessagePackSerializer.Deserialize<BlueprintMessagePack>(MessagePackSerializer.Serialize(new BlueprintMessagePack(original)));
            AssertBlueprint(original, packet.ToJsonObject());
        }

        [Test]
        public void 現行JSONの必須配線リスト欠損は補完しないTest()
        {
            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<BlueprintJsonObject>("{\"name\":\"broken\",\"blocks\":[]}"));
        }

        private static void AssertBlueprint(BlueprintJsonObject expected, BlueprintJsonObject actual)
        {
            Assert.AreEqual(expected.BlueprintGuid, actual.BlueprintGuid);
            Assert.AreEqual(expected.Name, actual.Name);
            Assert.AreEqual(expected.Blocks.Count, actual.Blocks.Count);
            for (var i = 0; i < expected.Blocks.Count; i++)
            {
                Assert.AreEqual(expected.Blocks[i].BlockGuid, actual.Blocks[i].BlockGuid);
                Assert.AreEqual(expected.Blocks[i].Offset, actual.Blocks[i].Offset);
                Assert.AreEqual(expected.Blocks[i].Direction, actual.Blocks[i].Direction);
            }

            Assert.AreEqual(1, actual.Wires.Count);
            Assert.AreEqual(1, actual.Chains.Count);
            AssertLine(expected.Wires[0], actual.Wires[0]);
            AssertLine(expected.Chains[0], actual.Chains[0]);
        }

        private static void AssertLine(BlueprintLineJsonObject expected, BlueprintLineJsonObject actual)
        {
            Assert.AreEqual(expected.BlockIndexA, actual.BlockIndexA);
            Assert.AreEqual(expected.BlockIndexB, actual.BlockIndexB);
            Assert.AreEqual(expected.ConnectToolGuid, actual.ConnectToolGuid);
        }
    }
}
