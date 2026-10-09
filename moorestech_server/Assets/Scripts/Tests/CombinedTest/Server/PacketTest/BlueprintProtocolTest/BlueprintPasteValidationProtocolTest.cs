using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Block.Interface;
using Game.Blueprint;
using Game.Context;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.Blueprint;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BlueprintPasteValidationProtocolTest
    {
        [TestCase(-1)]
        [TestCase(4)]
        public void 回転が範囲外ならログと通知で拒否するTest(int rotation)
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var blueprint = context.Create(ForUnitTestModBlockId.ChestId);
            context.Register(blueprint);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[BlueprintPaste\] invalid request"));

            context.Paste(blueprint, rotation, BlueprintPasteProtocolTestContext.Origin);

            Assert.AreEqual(0, ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
            context.AssertDenied(BlueprintFailureReason.InvalidRequest, 0);
        }

        [TestCase(0)]
        [TestCase(BlueprintRequest.MaxPasteOrigins + 1)]
        public void 原点数が範囲外ならログと通知で拒否するTest(int count)
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var blueprint = context.Create(ForUnitTestModBlockId.ChestId);
            context.Register(blueprint);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[BlueprintPaste\] invalid request"));

            context.Paste(blueprint, 0, Enumerable.Repeat(BlueprintPasteProtocolTestContext.Origin, count).ToArray());

            Assert.AreEqual(0, ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
            context.AssertDenied(BlueprintFailureReason.InvalidRequest, 0);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void 原点nullまたは要素nullを拒否するTest(bool nullList)
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var request = BlueprintRequest.CreatePasteRequest(Guid.NewGuid(), 0, new() { Vector3Int.zero });
            if (nullList) request.Origins = null;
            else request.Origins[0] = null;
            LogAssert.Expect(LogType.Warning, new Regex(@"\[BlueprintPaste\] invalid request"));

            context.Send(request);

            context.AssertDenied(BlueprintFailureReason.InvalidRequest, 0);
        }

        [Test]
        public void BP機能が未解放なら無料設置でも拒否するTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(false, true);
            var blueprint = context.Create(ForUnitTestModBlockId.ChestId);
            context.Register(blueprint);

            context.Paste(blueprint, 0, BlueprintPasteProtocolTestContext.Origin);

            Assert.AreEqual(0, ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
            context.AssertDenied(BlueprintFailureReason.NotUnlocked, -1);
        }

        [Test]
        public void 未解放ブロックを含むBPは全体を拒否するTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var blueprint = context.Create(ForUnitTestModBlockId.BlockId, ForUnitTestModBlockId.ChestId);
            PlaceBlockProtocolTestSupport.LockBlock(context.Services, ForUnitTestModBlockId.BlockId);
            context.Services.GetRequiredService<IGameUnlockStateDataController>().UnlockBlueprint();
            context.Register(blueprint);
            context.Supply(blueprint, 1);

            context.Paste(blueprint, 0, BlueprintPasteProtocolTestContext.Origin);

            Assert.AreEqual(0, ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
            context.AssertDenied(BlueprintFailureReason.PasteNotUnlocked, 1);
        }

        [Test]
        public void 欠損マスタと未知線種を操作単位でログに残し既知ブロックを置くTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, true);
            var blueprint = context.Create(ForUnitTestModBlockId.ChestId, ForUnitTestModBlockId.ChestId);
            blueprint.Blocks.Add(new BlueprintBlockJsonObject(Vector3Int.zero, Guid.NewGuid().ToString(),
                (int)BlockDirection.North, new Dictionary<string, string>()));
            blueprint.Wires.Add(new BlueprintLineJsonObject(0, 1, Guid.NewGuid()));
            blueprint.Wires.Add(new BlueprintLineJsonObject(1, 2, BlueprintPasteProtocolTestContext.WireGuid));
            context.UnlockLines();
            context.Register(blueprint);

            // 同じ欠損を含む2コピーでも、理由ごとのログは集計値を1回だけ出す
            // Two copies of the same damaged blueprint report one aggregate per reason
            LogAssert.Expect(LogType.Warning, new Regex(@"skipped missing block master count=1 "));
            LogAssert.Expect(LogType.Warning, new Regex(@"skipped line endpoint missing count=2 "));
            LogAssert.Expect(LogType.Warning, new Regex(@"skipped unknown connect tool count=2 "));
            var origin = BlueprintPasteProtocolTestContext.Origin;
            context.Paste(blueprint, 0, origin, origin + new Vector3Int(10, 0, 0));

            Assert.AreEqual(4, ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
        }

        [Test]
        public void 全重なりの複数コピーは拒否理由を集計してログに残すTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, true);
            var block = ForUnitTestModBlockId.ChestId;
            var blueprint = context.Create(block);
            context.Register(blueprint);
            var origin = BlueprintPasteProtocolTestContext.Origin;
            BlueprintPasteProtocolTestContext.Place(block, origin);

            LogAssert.Expect(LogType.Warning, new Regex(@"skipped block overlaps count=2 "));
            LogAssert.Expect(LogType.Warning, new Regex(@"skipped AllOverlapped count=2 "));
            context.Paste(blueprint, 0, origin, origin);

            Assert.AreEqual(1, ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
        }

        [Test]
        public void 存在しないBPは通知して拒否するTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);

            context.Send(BlueprintRequest.CreatePasteRequest(Guid.NewGuid(), 0, new() { Vector3Int.zero }));

            context.AssertDenied(BlueprintFailureReason.NotFound, 0);
        }
    }
}
