using System;
using System.IO;
using System.Linq;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.UIHighlight;
using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.UnitTest.Tutorial
{
    public class UiDragGuideTutorialManagerTest
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
            SetChallengeMaster(CreateUiDragGuideChallengeMaster());
            _root = new GameObject("UiDragGuideTutorialManagerTest");

            #region Internal

            ChallengeMaster CreateUiDragGuideChallengeMaster()
            {
                var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                    "mods", "forUnitTest", "master", "challenges.json");
                var json = JObject.Parse(File.ReadAllText(path));
                var tutorials = (JArray)json["data"][0]["challenges"][0]["tutorials"];
                var tutorial = (JObject)tutorials[0].DeepClone();
                tutorials.Clear();
                tutorials.Add(tutorial);
                tutorial["tutorialType"] = "uiDragGuide";
                tutorial["tutorialParam"] = new JObject
                {
                    ["fromAnchorId"] = "build-menu.entry-block-00000000-0000-0000-0000-000000000001",
                    ["toAnchorId"] = "hotbar.hud",
                };
                var master = new ChallengeMaster(json);
                master.Initialize();
                return master;
            }

            #endregion
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
            SetChallengeMaster(_originalChallengeMaster);
        }

        // from/toが入れ替わっても検出できない退行を防ぐ
        // Guards against from/to being silently swapped
        [Test]
        public void ApplyTutorialはfromとtoを正しい向きでDragGuideとして公開する()
        {
            var manager = _root.AddComponent<UiDragGuideTutorialManager>();
            var tutorial = MasterHolder.ChallengeMaster.GetChallenge(ChallengeGuid).Tutorials[0];
            var countBefore = DragGuides().Length;

            manager.ApplyTutorial(tutorial);

            var guides = DragGuides();
            Assert.AreEqual(countBefore + 1, guides.Length);
            var guide = guides[guides.Length - 1];
            Assert.AreEqual("build-menu.entry-block-00000000-0000-0000-0000-000000000001", guide.FromAnchorId);
            Assert.AreEqual("hotbar.hud", guide.ToAnchorId);

            #region Internal

            TutorialDragGuideElementData[] DragGuides()
            {
                return TutorialPresentationStateStore.Instance.GetCurrent().Sessions
                    .SelectMany(session => session.Elements)
                    .OfType<TutorialDragGuideElementData>().ToArray();
            }

            #endregion
        }

        private static void SetChallengeMaster(ChallengeMaster challengeMaster)
        {
            typeof(MasterHolder).GetProperty(nameof(MasterHolder.ChallengeMaster))
                .GetSetMethod(true).Invoke(null, new object[] { challengeMaster });
        }
    }
}
