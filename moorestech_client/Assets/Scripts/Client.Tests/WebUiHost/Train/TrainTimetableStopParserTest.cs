using Client.WebUiHost.Game.Actions;
using Game.Train.RailGraph;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUiHost
{
    public class TrainTimetableStopParserTest
    {
        // 不正座標で例外を投げず、置換全体を拒否できることを確認する
        // Ensure malformed coordinates allow rejection of the entire replacement without throwing
        [TestCase("{\"x\":1,\"y\":2,\"side\":\"back\"}")]
        [TestCase("{\"x\":1.5,\"y\":2,\"z\":3,\"side\":\"back\"}")]
        [TestCase("{\"x\":\"1\",\"y\":2,\"z\":3,\"side\":\"back\"}")]
        [TestCase("{\"x\":2147483648,\"y\":2,\"z\":3,\"side\":\"back\"}")]
        [TestCase("null")]
        public void RejectsInvalidCoordinates(string json)
        {
            Assert.That(TrainTimetableStopParser.TryParse(JToken.Parse(json), out _), Is.False);
        }

        // 端は "front"/"back" の文字列だけを受け取る
        // Accept the side only as the string "front" or "back"
        [TestCase("{\"x\":1,\"y\":2,\"z\":3}")]
        [TestCase("{\"x\":1,\"y\":2,\"z\":3,\"side\":\"up\"}")]
        [TestCase("{\"x\":1,\"y\":2,\"z\":3,\"side\":0}")]
        [TestCase("{\"x\":1,\"y\":2,\"z\":3,\"side\":null}")]
        [TestCase("{\"x\":1,\"y\":2,\"z\":3,\"side\":\"Front\"}")]
        public void RejectsInvalidSide(string json)
        {
            Assert.That(TrainTimetableStopParser.TryParse(JToken.Parse(json), out _), Is.False);
        }

        [TestCase("front", StationNodeSide.Front)]
        [TestCase("back", StationNodeSide.Back)]
        public void ParsesSide(string side, StationNodeSide expected)
        {
            var token = JObject.FromObject(new { x = 1, y = 2, z = 3, side });
            Assert.That(TrainTimetableStopParser.TryParse(token, out var stop), Is.True);
            Assert.That(stop.Side, Is.EqualTo(expected));
            Assert.That(stop.StationPosition, Is.EqualTo(new Vector3Int(1, 2, 3)));
        }

        [Test]
        public void PreservesNegativeAndBoundaryCoordinates()
        {
            var token = JObject.FromObject(new { x = int.MinValue, y = -5, z = int.MaxValue, side = "front" });
            Assert.That(TrainTimetableStopParser.TryParse(token, out var stop), Is.True);
            Assert.That(stop.StationPosition, Is.EqualTo(new Vector3Int(int.MinValue, -5, int.MaxValue)));
        }
    }
}
