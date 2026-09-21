using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.Construction;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Localization;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.TrainRailConnect
{
    /// <summary>
    /// 橋脚1セルのコスト・接続可否を検証
    /// Verifies cost and connection gates for one pier cell.
    /// </summary>
    public class TrainRailPierPlaceabilityTest
    {
        // TestTrainRailの建設コストはTest3×2
        // TestTrainRail costs Test3 x2
        private static readonly Guid PierMaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");

        [SetUp]
        public void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Localize.Initialize();
        }

        [Test]
        public void コスト不足なら設置不可になり不足行が積まれる()
        {
            var placeInfo = BuildPierCell();
            var feedback = new PlacementFeedback();

            TrainRailPierPlaceability.ApplyCostShortage(placeInfo, BuildWalletQuery(), BuildInventory(1), feedback);

            Assert.IsFalse(placeInfo.Placeable);
            Assert.AreEqual(1, feedback.Lines.Count);
        }

        [Test]
        public void コストが足りていれば設置可のまま不足行も無い()
        {
            var placeInfo = BuildPierCell();
            var feedback = new PlacementFeedback();

            TrainRailPierPlaceability.ApplyCostShortage(placeInfo, BuildWalletQuery(), BuildInventory(2), feedback);

            Assert.IsTrue(placeInfo.Placeable);
            Assert.IsEmpty(feedback.Lines);
        }

        [Test]
        public void 地面干渉で既に不可のセルには不足行を積まない()
        {
            // 設置予定セルが0なので支払いも発生しない
            // No cell is about to be placed, so nothing is paid for
            var placeInfo = BuildPierCell();
            placeInfo.Placeable = false;
            var feedback = new PlacementFeedback();

            TrainRailPierPlaceability.ApplyCostShortage(placeInfo, BuildWalletQuery(), BuildInventory(0), feedback);

            Assert.IsFalse(placeInfo.Placeable);
            Assert.IsEmpty(feedback.Lines);
        }

        [Test]
        public void 接続判定が不可なら橋脚も不可になる()
        {
            var placeInfo = BuildPierCell();

            // Invalidは失敗理由付きで設置不可
            // Invalid has a failure reason and is unplaceable.
            TrainRailPierPlaceability.ApplyConnectJudgement(placeInfo, TrainRailConnectPreviewData.Invalid);

            Assert.IsFalse(placeInfo.Placeable);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void 接続判定が正常なら橋脚の既存可否を維持する(bool wasPlaceable)
        {
            var placeInfo = BuildPierCell();
            placeInfo.Placeable = wasPlaceable;
            var judgement = new RailPlacementJudgement(RailConnectionEditProtocol.RailConnectionEditFailureReason.None,
                Guid.Parse("c0000000-0000-0000-0000-000000000002"), Array.Empty<ConnectToolMaterialCost>());
            var preview = new TrainRailConnectPreviewData(Vector3.zero, Vector3.forward, Vector3.forward * 2, Vector3.forward * 3,
                judgement, true, Array.Empty<ConstructionMaterialShortage>(), Array.Empty<ConstructionMaterialShortage>());

            Assert.IsTrue(preview.IsPlaceable);
            TrainRailPierPlaceability.ApplyConnectJudgement(placeInfo, preview);
            Assert.AreEqual(wasPlaceable, placeInfo.Placeable);
        }

        private static PlaceInfo BuildPierCell()
        {
            return new PlaceInfo { Position = Vector3Int.zero, Placeable = true, BlockId = ForUnitTestModBlockId.TestTrainRail };
        }

        private static ConstructionWalletQuery BuildWalletQuery()
        {
            return new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
        }

        private static List<IItemStack> BuildInventory(int pierMaterialCount)
        {
            return new List<IItemStack> { ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(PierMaterialGuid), pierMaterialCount) };
        }
    }
}
