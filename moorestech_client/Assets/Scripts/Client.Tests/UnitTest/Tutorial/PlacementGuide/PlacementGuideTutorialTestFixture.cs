using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.PlacementGuide;
using Client.Game.InGame.Tutorial.UIHighlight;
using Core.Master;
using Mooresmaster.Model.ChallengesModule;
using Newtonsoft.Json.Linq;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.UnitTest.Tutorial.PlacementGuide
{
    /// <summary>
    ///     設置案内チュートリアルのEditModeテストが共有するマスタ差し替えとmanager組み立て
    ///     Shared master swapping and manager assembly for the placement-guide tutorial EditMode tests
    /// </summary>
    public class PlacementGuideTutorialTestFixture
    {
        public static readonly Guid ChallengeGuid = Guid.Parse("00000000-0000-0000-4567-000000000001");

        public GameObject Root { get; }

        private readonly ChallengeMaster _originalChallengeMaster;

        public PlacementGuideTutorialTestFixture(string rootName)
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _originalChallengeMaster = MasterHolder.ChallengeMaster;
            Root = new GameObject(rootName);
        }

        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(Root);
            SetChallengeMaster(_originalChallengeMaster);
        }

        public VeinRestrictedPlacementTutorialManager CreateVeinRestrictedManager(VeinRestrictedPlacementState state)
        {
            var veinRestricted = Root.AddComponent<VeinRestrictedPlacementTutorialManager>();
            veinRestricted.Construct(state);
            return veinRestricted;
        }

        public RelativeBlockPlacePreviewTutorialManager CreateRelativeManager()
        {
            var blockGameObjectDataStore = Root.AddComponent<BlockGameObjectDataStore>();
            var relative = Root.AddComponent<RelativeBlockPlacePreviewTutorialManager>();
            relative.Construct(blockGameObjectDataStore, Root.AddComponent<BlockPlacePreviewTutorialManager>());
            return relative;
        }

        public TutorialManager CreateTutorialManager(VeinRestrictedPlacementTutorialManager veinRestricted, RelativeBlockPlacePreviewTutorialManager relative, List<ITutorialViewManager> extraManagers)
        {
            var managers = new List<ITutorialViewManager>
            {
                Root.AddComponent<UIHighlightTutorialManager>(),
                Root.AddComponent<KeyControlTutorialManager>(),
                Root.AddComponent<ItemViewHighLightTutorialManager>(),
                Root.AddComponent<BlockPlacePreviewTutorialManager>(),
                Root.AddComponent<UiDragGuideTutorialManager>(),
                veinRestricted,
                relative,
            };
            managers.AddRange(extraManagers);
            return new TutorialManager(managers);
        }

        // 適用中のViewは外向きAPIに現れないため、TutorialManagerが保持している実体を読み出して突き合わせる
        // The applied view is not exposed by the public API, so the instance TutorialManager holds is read back for comparison
        public static ITutorialView GetAppliedView(TutorialManager manager)
        {
            var applied = GetAppliedViews(manager);
            return 0 < applied.Count ? applied[0] : null;
        }

        public static List<ITutorialView> GetAppliedViews(TutorialManager manager)
        {
            var views = (Dictionary<Guid, List<ITutorialView>>)typeof(TutorialManager)
                .GetField("_tutorialViews", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            return views.TryGetValue(ChallengeGuid, out var applied) ? applied : new List<ITutorialView>();
        }

        public static JObject CreateRelativeParam(string anchorGuid, string blockGuid, int x, int y, int z)
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

        // challenges.json の最初のチャレンジのチュートリアルを1件だけ差し替えて ChallengeMaster を作り直す
        // Rebuild the ChallengeMaster with the first challenge's tutorial list replaced by a single entry
        public void SetTutorial(string tutorialType, JObject tutorialParam)
        {
            SetTutorialsCore((tutorialType, tutorialParam));
        }

        // 相対ゴースト複数件をtutorialGuidを変えて差し込む
        // Injects multiple relative previews with distinct tutorial guids
        public void SetRelativeTutorials(params JObject[] relativeParams)
        {
            var entries = new (string, JObject)[relativeParams.Length];
            for (var i = 0; i < relativeParams.Length; i++) entries[i] = ("relativeBlockPlacePreview", relativeParams[i]);
            SetTutorialsCore(entries);
        }

        private static void SetTutorialsCore(params (string tutorialType, JObject tutorialParam)[] entries)
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
