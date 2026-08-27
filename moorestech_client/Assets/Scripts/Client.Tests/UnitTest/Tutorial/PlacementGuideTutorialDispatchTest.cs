using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.PlacementGuide;
using Client.Game.InGame.Tutorial.UIHighlight;
using Core.Master;
using Mooresmaster.Model.ChallengesModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.UnitTest.Tutorial
{
    /// <summary>
    ///     設置システム案内の2チュートリアル型が専用managerへdispatchされ、共有状態へ書かれることを検証する
    ///     Verifies that the two placement-guide tutorial types dispatch to their managers and write the shared state
    /// </summary>
    public class PlacementGuideTutorialDispatchTest
    {
        private static readonly Guid ChallengeGuid = Guid.Parse("00000000-0000-0000-4567-000000000001");

        private ChallengeMaster _originalChallengeMaster;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _originalChallengeMaster = MasterHolder.ChallengeMaster;
            _root = new GameObject("PlacementGuideTutorialDispatchTest");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
            SetChallengeMaster(_originalChallengeMaster);
        }

        [Test]
        public void veinRestrictedPlacementは専用managerへdispatchされ状態へ書く()
        {
            SetTutorial("veinRestrictedPlacement", new JObject
            {
                ["veinGuid"] = "11111111-0000-0000-0000-000000000001",
                ["blockGuid"] = "00000000-0000-0000-0000-000000000006",
            });
            var state = new VeinRestrictedPlacementState();
            var veinRestricted = _root.AddComponent<VeinRestrictedPlacementTutorialManager>();
            veinRestricted.Construct(state);
            var manager = CreateTutorialManager(veinRestricted, _root.AddComponent<RelativeBlockPlacePreviewTutorialManager>());

            manager.ApplyTutorial(ChallengeGuid);

            Assert.IsTrue(state.IsRestrictedBlock(ForUnitTestModBlockId.ElectricMinerId));
            Assert.AreEqual(Guid.Parse("11111111-0000-0000-0000-000000000001"), state.VeinGuid);

            manager.CompleteChallenge(ChallengeGuid);

            Assert.IsNull(state.VeinGuid);
            Assert.IsFalse(state.IsRestrictedBlock(ForUnitTestModBlockId.ElectricMinerId));
        }

        [Test]
        public void relativeBlockPlacePreviewは専用managerへdispatchされ完了で解除される()
        {
            SetTutorial("relativeBlockPlacePreview", new JObject
            {
                ["anchorBlockGuid"] = "00000000-0000-0000-0000-000000000014",
                ["blockGuid"] = "00000000-0000-0000-0000-00000000000e",
                ["offset"] = new JArray(0, 0, 1),
                ["blockDirection"] = "North",
                ["message"] = "テスト",
            });
            var relative = _root.AddComponent<RelativeBlockPlacePreviewTutorialManager>();
            var manager = CreateTutorialManager(_root.AddComponent<VeinRestrictedPlacementTutorialManager>(), relative);

            manager.ApplyTutorial(ChallengeGuid);
            Assert.IsTrue(relative.IsApplied);

            manager.CompleteChallenge(ChallengeGuid);
            Assert.IsFalse(relative.IsApplied);
        }

        private TutorialManager CreateTutorialManager(VeinRestrictedPlacementTutorialManager veinRestricted, RelativeBlockPlacePreviewTutorialManager relative)
        {
            return new TutorialManager(
                new List<ITutorialWorldPin>(),
                _root.AddComponent<UIHighlightTutorialManager>(),
                _root.AddComponent<KeyControlTutorialManager>(),
                _root.AddComponent<ItemViewHighLightTutorialManager>(),
                _root.AddComponent<BlockPlacePreviewTutorialManager>(),
                _root.AddComponent<UiDragGuideTutorialManager>(),
                veinRestricted,
                relative);
        }

        // challenges.json の最初のチャレンジのチュートリアルを1件だけ差し替えて ChallengeMaster を作り直す
        // Rebuild the ChallengeMaster with the first challenge's tutorial list replaced by a single entry
        private void SetTutorial(string tutorialType, JObject tutorialParam)
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "challenges.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var tutorials = (JArray)json["data"][0]["challenges"][0]["tutorials"];
            var tutorial = (JObject)tutorials[0].DeepClone();
            tutorials.Clear();
            tutorials.Add(tutorial);
            tutorial["tutorialType"] = tutorialType;
            tutorial["tutorialParam"] = tutorialParam;
            var master = new ChallengeMaster(json);
            master.Initialize();
            SetChallengeMaster(master);
        }

        private static void SetChallengeMaster(ChallengeMaster challengeMaster)
        {
            typeof(MasterHolder).GetProperty(nameof(MasterHolder.ChallengeMaster))
                .GetSetMethod(true).Invoke(null, new object[] { challengeMaster });
        }
    }
}
