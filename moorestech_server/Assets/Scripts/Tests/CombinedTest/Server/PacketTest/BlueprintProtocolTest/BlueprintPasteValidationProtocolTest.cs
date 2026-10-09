using System;
using System.Linq;
using System.Text.RegularExpressions;
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
        public void 存在しないBPは通知して拒否するTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);

            context.Send(BlueprintRequest.CreatePasteRequest(Guid.NewGuid(), 0, new() { Vector3Int.zero }));

            context.AssertDenied(BlueprintFailureReason.NotFound, 0);
        }
    }
}
