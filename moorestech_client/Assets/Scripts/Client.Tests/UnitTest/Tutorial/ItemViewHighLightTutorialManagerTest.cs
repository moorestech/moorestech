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
    // 空判定→labelGuidへ反映を保証
    // Pins the empty check maps to labelGuid
    public class ItemViewHighLightTutorialManagerTest
    {
        private static readonly Guid ChallengeGuid = Guid.Parse("00000000-0000-0000-4567-000000000001");
        private const string ItemGuid = "00000000-0000-0000-1234-000000000001";

        private ChallengeMaster _originalChallengeMaster;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _originalChallengeMaster = MasterHolder.ChallengeMaster;
            _root = new GameObject("ItemViewHighLightTutorialManagerTest");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
            SetChallengeMaster(_originalChallengeMaster);
        }

        [Test]
        public void 文言ありならlabelTutorialGuidにtutorialGuidが入る()
        {
            SetChallengeMaster(CreateItemViewHighLightChallengeMaster("照準に合わせる"));
            var manager = _root.AddComponent<ItemViewHighLightTutorialManager>();
            var tutorial = MasterHolder.ChallengeMaster.GetChallenge(ChallengeGuid).Tutorials[0];

            manager.ApplyTutorial(tutorial);

            var outline = LatestOutline();
            Assert.AreEqual(tutorial.TutorialGuid.ToString(), outline.LabelTutorialGuid);
        }

        [Test]
        public void 文言が空ならlabelTutorialGuidはnull()
        {
            SetChallengeMaster(CreateItemViewHighLightChallengeMaster(""));
            var manager = _root.AddComponent<ItemViewHighLightTutorialManager>();
            var tutorial = MasterHolder.ChallengeMaster.GetChallenge(ChallengeGuid).Tutorials[0];

            manager.ApplyTutorial(tutorial);

            var outline = LatestOutline();
            Assert.IsNull(outline.LabelTutorialGuid);
        }

        private static TutorialOutlineElementData LatestOutline()
        {
            return TutorialPresentationStateStore.Instance.GetCurrent().Sessions
                .SelectMany(session => session.Elements)
                .OfType<TutorialOutlineElementData>().Last();
        }

        private static ChallengeMaster CreateItemViewHighLightChallengeMaster(string highLightText)
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "challenges.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var tutorials = (JArray)json["data"][0]["challenges"][0]["tutorials"];
            var tutorial = (JObject)tutorials[0].DeepClone();
            tutorials.Clear();
            tutorials.Add(tutorial);
            tutorial["tutorialType"] = "itemViewHighLight";
            tutorial["tutorialParam"] = new JObject
            {
                ["highLightItemGuid"] = ItemGuid,
                ["highLightText"] = highLightText,
            };
            var master = new ChallengeMaster(json);
            master.Initialize();
            return master;
        }

        private static void SetChallengeMaster(ChallengeMaster challengeMaster)
        {
            typeof(MasterHolder).GetProperty(nameof(MasterHolder.ChallengeMaster))
                .GetSetMethod(true).Invoke(null, new object[] { challengeMaster });
        }
    }
}
