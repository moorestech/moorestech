using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.PlacementGuide;
using Core.Master;
using Game.Block.Interface;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using static Client.Tests.UnitTest.Tutorial.PlacementGuide.PlacementGuideTutorialTestFixture;

namespace Client.Tests.UnitTest.Tutorial.PlacementGuide
{
    /// <summary>
    ///     設置システム案内のチュートリアル型が専用managerへdispatchされ、共有状態へ書かれることを検証する
    ///     ゴースト生成はClientContextを要するためここでは踏まない。複数ゴーストの独立性はEditModeInPlayingTest側で見る
    ///     Verifies that the placement-guide tutorial types dispatch to their managers and write the shared state
    ///     Ghost creation needs ClientContext, so it is not exercised here; multi-ghost independence lives in the EditModeInPlayingTest side
    /// </summary>
    public class PlacementGuideTutorialDispatchTest
    {
        private PlacementGuideTutorialTestFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _fixture = new PlacementGuideTutorialTestFixture("PlacementGuideTutorialDispatchTest");
        }

        [TearDown]
        public void TearDown()
        {
            _fixture.TearDown();
        }

        [Test]
        public void veinRestrictedPlacementは専用managerへdispatchされ状態へ書く()
        {
            _fixture.SetTutorial("veinRestrictedPlacement", new JObject
            {
                ["veinGuid"] = "11111111-0000-0000-0000-000000000001",
                ["blockGuid"] = "00000000-0000-0000-0000-000000000006",
            });
            var state = new VeinRestrictedPlacementState();
            var veinRestricted = _fixture.CreateVeinRestrictedManager(state);
            var manager = _fixture.CreateTutorialManager(veinRestricted, _fixture.CreateRelativeManager(), new List<ITutorialViewManager>());

            manager.ApplyTutorial(ChallengeGuid);

            Assert.IsTrue(state.TryGetRestrictedVeinType(ForUnitTestModBlockId.ElectricMinerId, out var veinGuid));
            Assert.AreEqual(Guid.Parse("11111111-0000-0000-0000-000000000001"), veinGuid);

            manager.CompleteChallenge(ChallengeGuid);

            Assert.IsFalse(state.TryGetRestrictedVeinType(ForUnitTestModBlockId.ElectricMinerId, out _));
        }

        [Test]
        public void relativeBlockPlacePreviewは専用managerへdispatchされ完了で解除される()
        {
            _fixture.SetTutorial("relativeBlockPlacePreview", CreateRelativeParam("00000000-0000-0000-0000-000000000014", "00000000-0000-0000-0000-00000000000e", 0, 0, 1));
            var relative = _fixture.CreateRelativeManager();
            var veinRestricted = _fixture.CreateVeinRestrictedManager(new VeinRestrictedPlacementState());
            var manager = _fixture.CreateTutorialManager(veinRestricted, relative, new List<ITutorialViewManager>());

            // 専用managerへ振り分けられた時だけViewが返り、完了で解除される。dispatchが外れれば戻り値がnullになって落ちる
            // A view comes back only when the dedicated manager received the dispatch, and completion releases it; a broken dispatch returns null and fails here
            manager.ApplyTutorial(ChallengeGuid);
            Assert.IsInstanceOf<RelativeBlockPlacePreviewEntry>(GetAppliedView(manager));

            manager.CompleteChallenge(ChallengeGuid);
            Assert.IsNull(GetAppliedView(manager));
        }

        [Test]
        public void chainBlockPlacePreviewは専用managerへdispatchされ状態へ書き完了で解除される()
        {
            _fixture.SetTutorial("chainBlockPlacePreview", new JObject
            {
                ["placingBlockGuid"] = "00000000-0000-0000-0000-000000000014",
                ["chainBlocks"] = new JArray
                {
                    new JObject
                    {
                        ["blockGuid"] = "00000000-0000-0000-0000-00000000000e",
                        ["offset"] = new JArray(0, 0, 1),
                        ["blockDirection"] = "North",
                    },
                },
            });
            var state = new ChainPlacePreviewState();
            var chain = _fixture.Root.AddComponent<ChainBlockPlacePreviewTutorialManager>();
            chain.Construct(state);
            var veinRestricted = _fixture.CreateVeinRestrictedManager(new VeinRestrictedPlacementState());
            var manager = _fixture.CreateTutorialManager(veinRestricted, _fixture.CreateRelativeManager(), new List<ITutorialViewManager> { chain });

            manager.ApplyTutorial(ChallengeGuid);

            // 共有状態のJSON一致と完了解除を確認
            // Verifies the shared state was written per the JSON chain layout, and clears on completion
            var anchorBlockId = MasterHolder.BlockMaster.GetBlockId(Guid.Parse("00000000-0000-0000-0000-000000000014"));
            var expectedGhostBlockId = MasterHolder.BlockMaster.GetBlockId(Guid.Parse("00000000-0000-0000-0000-00000000000e"));
            Assert.IsTrue(state.TryGetChain(anchorBlockId, out var resultChain, out _));
            Assert.AreEqual(1, resultChain.Count);
            Assert.AreEqual(expectedGhostBlockId, resultChain[0].BlockId);
            Assert.AreEqual(new Vector3Int(0, 0, 1), resultChain[0].Offset);
            Assert.AreEqual(BlockDirection.North, resultChain[0].LocalDirection);

            manager.CompleteChallenge(ChallengeGuid);

            Assert.IsFalse(state.TryGetChain(anchorBlockId, out _, out _));
        }
    }
}
