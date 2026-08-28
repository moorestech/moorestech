using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.Block;
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
            var manager = CreateTutorialManager(veinRestricted, CreateRelativeManager());

            manager.ApplyTutorial(ChallengeGuid);

            Assert.IsTrue(state.TryGetRestrictedVeinType(ForUnitTestModBlockId.ElectricMinerId, out var veinGuid));
            Assert.AreEqual(Guid.Parse("11111111-0000-0000-0000-000000000001"), veinGuid);

            manager.CompleteChallenge(ChallengeGuid);

            Assert.IsFalse(state.TryGetRestrictedVeinType(ForUnitTestModBlockId.ElectricMinerId, out _));
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
            var relative = CreateRelativeManager();
            var veinRestricted = _root.AddComponent<VeinRestrictedPlacementTutorialManager>();
            veinRestricted.Construct(new VeinRestrictedPlacementState());
            var manager = CreateTutorialManager(veinRestricted, relative);

            // 専用managerへ振り分けられた時だけViewが返り、完了で解除される。dispatchが外れれば戻り値がnullになって落ちる
            // A view comes back only when the dedicated manager received the dispatch, and completion releases it; a broken dispatch returns null and fails here
            manager.ApplyTutorial(ChallengeGuid);
            Assert.AreSame(relative, GetAppliedView(manager));

            manager.CompleteChallenge(ChallengeGuid);
            Assert.IsNull(GetAppliedView(manager));
        }

        private TutorialManager CreateTutorialManager(VeinRestrictedPlacementTutorialManager veinRestricted, RelativeBlockPlacePreviewTutorialManager relative)
        {
            return new TutorialManager(new List<ITutorialViewManager>
            {
                _root.AddComponent<UIHighlightTutorialManager>(),
                _root.AddComponent<KeyControlTutorialManager>(),
                _root.AddComponent<ItemViewHighLightTutorialManager>(),
                _root.AddComponent<BlockPlacePreviewTutorialManager>(),
                _root.AddComponent<UiDragGuideTutorialManager>(),
                veinRestricted,
                relative,
            });
        }

        private RelativeBlockPlacePreviewTutorialManager CreateRelativeManager()
        {
            var blockGameObjectDataStore = _root.AddComponent<BlockGameObjectDataStore>();
            var relative = _root.AddComponent<RelativeBlockPlacePreviewTutorialManager>();
            relative.Construct(blockGameObjectDataStore, _root.AddComponent<BlockPlacePreviewTutorialManager>());
            return relative;
        }

        // 適用中のViewは外向きAPIに現れないため、TutorialManagerが保持している実体を読み出して突き合わせる
        // The applied view is not exposed by the public API, so the instance TutorialManager holds is read back for comparison
        private static ITutorialView GetAppliedView(TutorialManager manager)
        {
            var views = (Dictionary<Guid, List<ITutorialView>>)typeof(TutorialManager)
                .GetField("_tutorialViews", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            return views.TryGetValue(ChallengeGuid, out var applied) ? applied[0] : null;
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
