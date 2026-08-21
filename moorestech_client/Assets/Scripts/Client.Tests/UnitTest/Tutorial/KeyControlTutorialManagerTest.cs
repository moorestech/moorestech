using System;
using System.IO;
using System.Linq;
using Client.Game.InGame.Tutorial;
using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.UnitTest.Tutorial
{
    public class KeyControlTutorialManagerTest
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
            SetChallengeMaster(CreateKeyControlChallengeMaster());
            _root = new GameObject("KeyControlTutorialManagerTest");

            #region Internal

            ChallengeMaster CreateKeyControlChallengeMaster()
            {
                var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                    "mods", "forUnitTest", "master", "challenges.json");
                var json = JObject.Parse(File.ReadAllText(path));
                var tutorials = (JArray)json["data"][0]["challenges"][0]["tutorials"];
                var tutorial = (JObject)tutorials[0].DeepClone();
                tutorials.Clear();
                tutorials.Add(tutorial);
                tutorial["tutorialType"] = "keyControl";
                tutorial["tutorialParam"] = new JObject
                {
                    ["uiState"] = "PlayerInventory",
                    ["keyName"] = "R",
                    ["controlText"] = "研究画面を開く",
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

        // keyControlを公開・完了で撤去
        // Publishes keyControl; removed on completion
        [Test]
        public void ApplyTutorialはkeyControl要素を公開し完了で撤去する()
        {
            var manager = _root.AddComponent<KeyControlTutorialManager>();
            var tutorial = MasterHolder.ChallengeMaster.GetChallenge(ChallengeGuid).Tutorials[0];
            var countBefore = KeyControls().Length;

            var view = manager.ApplyTutorial(tutorial);

            var hints = KeyControls();
            Assert.AreEqual(countBefore + 1, hints.Length);
            var hint = hints[hints.Length - 1];
            Assert.AreEqual(tutorial.TutorialGuid.ToString(), hint.TutorialGuid);
            Assert.AreEqual("R", hint.KeyName);
            Assert.AreEqual("PlayerInventory", hint.UiState);

            view.CompleteTutorial();
            Assert.AreEqual(countBefore, KeyControls().Length);

            #region Internal

            TutorialKeyControlElementData[] KeyControls()
            {
                return TutorialPresentationStateStore.Instance.GetCurrent().Sessions
                    .SelectMany(session => session.Elements)
                    .OfType<TutorialKeyControlElementData>().ToArray();
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
