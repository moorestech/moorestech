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
            Assert.IsInstanceOf<RelativeBlockPlacePreviewEntry>(GetAppliedView(manager));

            manager.CompleteChallenge(ChallengeGuid);
            Assert.IsNull(GetAppliedView(manager));
        }

        [Test]
        public void 同一チャレンジ内の相対ゴースト2件は上書きされず両方生きる()
        {
            SetTutorials(
                CreateRelativeParam("00000000-0000-0000-0000-000000000014", "00000000-0000-0000-0000-00000000000e", 0, 0, 1),
                CreateRelativeParam("00000000-0000-0000-0000-000000000014", "00000000-0000-0000-0000-000000000006", 0, 0, 2));
            var relative = CreateRelativeManager();
            var veinRestricted = _root.AddComponent<VeinRestrictedPlacementTutorialManager>();
            veinRestricted.Construct(new VeinRestrictedPlacementState());
            var manager = CreateTutorialManager(veinRestricted, relative);

            manager.ApplyTutorial(ChallengeGuid);

            // 2エントリが独立したViewとして生き、片方の完了で他方が消えない
            // Both entries stay alive as independent views; completing one never folds the other
            var views = GetAppliedViews(manager);
            Assert.AreEqual(2, views.Count);
            var first = (RelativeBlockPlacePreviewEntry)views[0];
            var second = (RelativeBlockPlacePreviewEntry)views[1];
            Assert.AreNotSame(first, second);
            Assert.IsTrue(relative.HasActiveEntry(first.TutorialGuid));
            Assert.IsTrue(relative.HasActiveEntry(second.TutorialGuid));

            first.CompleteTutorial();
            Assert.IsFalse(relative.HasActiveEntry(first.TutorialGuid));
            Assert.IsTrue(relative.HasActiveEntry(second.TutorialGuid));
        }

        private static JObject CreateRelativeParam(string anchorGuid, string blockGuid, int x, int y, int z)
        {
            return new JObject
            {
                ["anchorBlockGuid"] = anchorGuid,
                ["blockGuid"] = blockGuid,
                ["offset"] = new JArray(x, y, z),
                ["blockDirection"] = "North",
                ["message"] = "テスト",
            };
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
            var applied = GetAppliedViews(manager);
            return applied.Count > 0 ? applied[0] : null;
        }

        private static List<ITutorialView> GetAppliedViews(TutorialManager manager)
        {
            var views = (Dictionary<Guid, List<ITutorialView>>)typeof(TutorialManager)
                .GetField("_tutorialViews", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            return views.TryGetValue(ChallengeGuid, out var applied) ? applied : new List<ITutorialView>();
        }

        // challenges.json の最初のチャレンジのチュートリアルを1件だけ差し替えて ChallengeMaster を作り直す
        // Rebuild the ChallengeMaster with the first challenge's tutorial list replaced by a single entry
        private void SetTutorial(string tutorialType, JObject tutorialParam)
        {
            SetTutorialsCore((tutorialType, tutorialParam));
        }

        // 相対ゴースト複数件をtutorialGuidを変えて差し込む
        // Injects multiple relative previews with distinct tutorial guids
        private void SetTutorials(params JObject[] relativeParams)
        {
            var entries = new (string, JObject)[relativeParams.Length];
            for (var i = 0; i < relativeParams.Length; i++) entries[i] = ("relativeBlockPlacePreview", relativeParams[i]);
            SetTutorialsCore(entries);
        }

        private void SetTutorialsCore(params (string tutorialType, JObject tutorialParam)[] entries)
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "challenges.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var tutorials = (JArray)json["data"][0]["challenges"][0]["tutorials"];
            var template = (JObject)tutorials[0].DeepClone();
            tutorials.Clear();
            for (var i = 0; i < entries.Length; i++)
            {
                var tutorial = (JObject)template.DeepClone();
                tutorial["tutorialGuid"] = new Guid($"aaaaaaaa-0000-0000-0000-00000000000{i + 1}").ToString("D");
                tutorial["tutorialType"] = entries[i].tutorialType;
                tutorial["tutorialParam"] = entries[i].tutorialParam;
                tutorials.Add(tutorial);
            }
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
