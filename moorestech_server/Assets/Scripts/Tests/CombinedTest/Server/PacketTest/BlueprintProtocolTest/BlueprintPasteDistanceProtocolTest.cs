using System.Text.RegularExpressions;
using Game.Context;
using Game.Entity.Interface;
using Game.PlacementTarget;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BlueprintPasteDistanceProtocolTest
    {
        [TestCase(0)]
        [TestCase(1)]
        public void 列のどの原点でも範囲外なら要求全体を拒否するTest(int farIndex)
        {
            using var context = new BlueprintPasteProtocolTestContext(true, true);
            var blueprint = context.Create(ForUnitTestModBlockId.BlockId);
            context.Register(blueprint);
            var origins = new[] { Vector3Int.zero, Vector3Int.right };
            origins[farIndex] = new Vector3Int(101, 0, 0);
            LogAssert.Expect(LogType.Warning, new Regex(@"invalid placement distance"));

            context.Paste(blueprint, 0, origins);

            Assert.IsEmpty(ServerContext.WorldBlockDatastore.BlockMasterDictionary);
            context.AssertDenied(BlueprintFailureReason.InvalidRequest, 0);
        }

        [Test]
        public void プレイヤーが移動したら同期位置から距離を再判定するTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, true);
            var blueprint = context.Create(ForUnitTestModBlockId.BlockId);
            context.Register(blueprint);
            context.Services.GetRequiredService<IEntitiesDatastore>().SetPosition(
                new EntityInstanceId(BlueprintPasteProtocolTestContext.PlayerId), new Vector3(200, 0, 0));
            LogAssert.Expect(LogType.Warning, new Regex(@"invalid placement distance"));

            context.Paste(blueprint, 0, Vector3Int.zero);

            Assert.IsEmpty(ServerContext.WorldBlockDatastore.BlockMasterDictionary);
            context.AssertDenied(BlueprintFailureReason.InvalidRequest, 0);
        }

        [Test]
        public void 通常設置と同じ距離境界なら受理するTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, true);
            var blueprint = context.Create(ForUnitTestModBlockId.BlockId);
            context.Register(blueprint);
            context.Paste(blueprint, 0, new Vector3Int(100, 0, 0));
            Assert.AreEqual(1, ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
        }

        [Test]
        public void 非有限位置と整数境界の距離は拒否するTest()
        {
            Assert.IsFalse(PlacementDistanceRule.IsWithinReach(new Vector3(float.NaN, 0, 0), Vector3Int.zero));
            Assert.IsFalse(PlacementDistanceRule.IsWithinReach(new Vector3(float.PositiveInfinity, 0, 0), Vector3Int.zero));
            Assert.IsFalse(PlacementDistanceRule.IsWithinReach(Vector3.zero, new Vector3Int(int.MaxValue, 0, 0)));
        }
    }
}
