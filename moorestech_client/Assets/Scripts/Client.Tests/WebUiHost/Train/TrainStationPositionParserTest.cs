using Client.WebUiHost.Game.Actions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUiHost
{
    public class TrainStationPositionParserTest
    {
        // 不正座標で例外を投げず、置換全体を拒否できることを確認する
        // Ensure malformed coordinates allow rejection of the entire replacement without throwing
        [TestCase("{\"x\":1,\"y\":2}")]
        [TestCase("{\"x\":1.5,\"y\":2,\"z\":3}")]
        [TestCase("{\"x\":\"1\",\"y\":2,\"z\":3}")]
        [TestCase("{\"x\":2147483648,\"y\":2,\"z\":3}")]
        [TestCase("null")]
        public void RejectsInvalidCoordinates(string json)
        {
            Assert.That(TrainStationPositionParser.TryParse(JToken.Parse(json), out _), Is.False);
        }

        [Test]
        public void PreservesNegativeAndBoundaryCoordinates()
        {
            var token = JObject.FromObject(new { x = int.MinValue, y = -5, z = int.MaxValue });
            Assert.That(TrainStationPositionParser.TryParse(token, out var position), Is.True);
            Assert.That(position, Is.EqualTo(new Vector3Int(int.MinValue, -5, int.MaxValue)));
        }
    }
}
