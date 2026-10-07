using System;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RailPierPlacementRejectionTest : RailConnectWithPlacePierProtocolTestBase
    {

        [Test]
        public void 補強棒材が不足なら設置されず失敗応答を返す()
        {
            UnlockRailConnectTool();
            // 補強棒材を1単位分未満だけ所持する（鉄板は潤沢）
            // Hold less than one unit of reinforce (plate is plentiful)
            SetInventory(reinforce: ReinforcePerUnit - 1, plate: PlatePlenty);

            var response = Send(ForUnitTestModBlockId.TestTrainRail);

            AssertFailedWithoutStateChange(response, expectedReinforce: ReinforcePerUnit - 1, expectedPlate: PlatePlenty);
        }

        [Test]
        public void 鉄板が橋脚コストと敷設分の合算で不足なら失敗しロールバックされる()
        {
            UnlockRailConnectTool();
            // 鉄板は橋脚コスト(2)分のみ所持する。敷設分(5×units≧5)には足りず合算で失敗する（補強棒材は潤沢）
            // Hold plate only for the pier cost (2); insufficient for laying (5×units≥5), so the combined check fails (reinforce is plentiful)
            SetInventory(reinforce: ReinforcePlenty, plate: PierPlateCost);

            var response = Send(ForUnitTestModBlockId.TestTrainRail);

            AssertFailedWithoutStateChange(response, expectedReinforce: ReinforcePlenty, expectedPlate: PierPlateCost);
        }

        [Test]
        public void 未解放橋脚は設置されず失敗応答を返す()
        {
            UnlockRailConnectTool();
            SetInventory(reinforce: ReinforcePlenty, plate: PlatePlenty);

            var response = Send(ForUnitTestModBlockId.LockedTrainRail);

            AssertFailedWithoutStateChange(response, expectedReinforce: ReinforcePlenty, expectedPlate: PlatePlenty);
        }

        [Test]
        public void 未解放connectToolでは設置されず失敗応答を返す()
        {
            // connectToolを解放しないまま接続を試みる
            // Attempt the connection without unlocking the connectTool
            SetInventory(reinforce: ReinforcePlenty, plate: PlatePlenty);

            var response = Send(ForUnitTestModBlockId.TestTrainRail);

            AssertFailedWithoutStateChange(response, expectedReinforce: ReinforcePlenty, expectedPlate: PlatePlenty);
        }

        [Test]
        public void connectToolGuidがEmptyの接続要求は無料設置扱いされず失敗応答を返す()
        {
            // connectToolを解放し素材も潤沢でも、Empty指定は無料設置扱いされず拒否される
            // Even with an unlocked connectTool and ample materials, an Empty specification is rejected instead of treated as free placement
            UnlockRailConnectTool();
            SetInventory(reinforce: ReinforcePlenty, plate: PlatePlenty);

            var response = Send(ForUnitTestModBlockId.TestTrainRail, Guid.Empty);

            AssertFailedWithoutStateChange(response, expectedReinforce: ReinforcePlenty, expectedPlate: PlatePlenty);
        }

        [Test]
        // 両端の最大接続長(TestTrainRailは100)を超える距離は、素材が潤沢でも共有判定で拒否されロールバックされる
        // A distance beyond both endpoints' max connectable length (100 for TestTrainRail) is rejected by the shared judgement and rolled back even with ample materials
        public void 最大接続長を超える距離は失敗しロールバックされる()
        {
            UnlockRailConnectTool();
            SetInventory(reinforce: ReinforcePlenty, plate: PlatePlenty);
            _pierPosition = new Vector3Int(200, 0, 0);

            var response = Send(ForUnitTestModBlockId.TestTrainRail);

            AssertFailedWithoutStateChange(response, expectedReinforce: ReinforcePlenty, expectedPlate: PlatePlenty);
        }
    }
}
