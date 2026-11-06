using System;
using System.IO;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.Localization;
using Client.Localization;
using Game.Blueprint;
using Mod.Loader;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.State.PlacementTarget
{
    /// <summary>
    /// Web の localizeSelectableTargetName を、C# の唯一の解決点 IPlacementTarget.DisplayName で固定する
    /// Pins the Web localizeSelectableTargetName onto C#'s single resolution point, IPlacementTarget.DisplayName
    /// </summary>
    public class PlacementTargetDisplayNameTest
    {
        private static readonly Guid BlockGuid = new("30000000-0000-4000-8000-000000000001");
        private static readonly Guid ConnectToolGuid = new("40000000-0000-4000-8000-000000000001");
        private static readonly Guid TrainCarGuid = new("8f9c2a51-0000-4000-8000-000000000001");
        private bool _hadSavedLanguage;
        private string _savedLanguage;
        private string _temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            // 端末の保存言語を退避し、日本語で辞書を組む
            // Stash the device's saved language and compose the dictionary in Japanese
            _hadSavedLanguage = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguage = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"placement-names-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_temporaryDirectory);
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            UseLanguage("japanese");
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadSavedLanguage) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguage);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            if (Directory.Exists(_temporaryDirectory)) Directory.Delete(_temporaryDirectory, true);
            Localize.Initialize();
        }

        /// <web-test file="src/shared/placementTarget/selectableTarget.test.ts">blockはblockNameKeyで解決する</web-test>
        /// <web-test file="src/features/buildMenu/logic/buildMenuGrouping.test.ts">blockはGuid導出キーで表示名を解決しraw labelを要求しない</web-test>
        [Test]
        public void ブロックはGUIDから導いたキーで表示名を解決する()
        {
            MergeContentNames();
            Assert.AreEqual("木のチェスト", new BlockPlacementTarget(BlockGuid, null).DisplayName);
        }

        /// <web-test file="src/shared/placementTarget/selectableTarget.test.ts">connectToolはconnectToolNameKeyで解決する</web-test>
        /// <web-test file="src/features/buildMenu/logic/buildMenuGrouping.test.ts">connectToolはraw labelなしでGuid導出キーから表示名を解決する</web-test>
        [Test]
        public void 接続ツールはGUIDから導いたキーで表示名を解決する()
        {
            MergeContentNames();
            Assert.AreEqual("電線ツール", new ConnectToolPlacementTarget(ConnectToolGuid).DisplayName);
        }

        /// <web-test file="src/shared/placementTarget/selectableTarget.test.ts">trainCarはtrainCarNameKeyで解決する</web-test>
        /// <web-test file="src/features/buildMenu/logic/buildMenuGrouping.test.ts">trainCarはraw labelなしでGuid導出キーから表示名を解決する</web-test>
        [Test]
        public void 列車車両はGUIDから導いたキーで表示名を解決する()
        {
            MergeContentNames();
            Assert.AreEqual("蒸気機関車", new TrainCarPlacementTarget(TrainCarGuid).DisplayName);
        }

        /// <web-test file="src/shared/placementTarget/selectableTarget.test.ts">blueprintCopyはtyped UI keyで解決する</web-test>
        /// <web-test file="src/features/buildMenu/logic/buildMenuGrouping.test.ts">blueprintCopyはraw labelなしで%s辞書から表示名を解決する</web-test>
        [TestCase("english", "Blueprint Copy")]
        [TestCase("japanese", "ブループリントコピー")]
        public void ブループリントコピーはUIの辞書キーで表示名を解決する(string languageCode, string expected)
        {
            UseLanguage(languageCode);
            Assert.AreEqual(expected, new BlueprintCopyPlacementTarget(Guid.NewGuid()).DisplayName);
        }

        /// <web-test file="src/shared/placementTarget/selectableTarget.test.ts">rawはユーザー命名文字列をそのまま返す</web-test>
        /// <web-test file="src/features/buildMenu/logic/buildMenuGrouping.test.ts">ユーザー命名blueprintはlabelをそのまま維持する</web-test>
        [Test]
        public void 保存したブループリントは利用者が付けた名前をそのまま返す()
        {
            var guid = Guid.NewGuid();
            var blueprint = new BlueprintJsonObject("starter-base", new(), new(), new(), guid);
            Assert.AreEqual("starter-base", new BlueprintPlacementTarget(guid, "starter-base", blueprint).DisplayName);
        }

        private static void UseLanguage(string languageCode)
        {
            PlayerPrefs.SetString(Localize.LanguagePreferenceKey, languageCode);
            Localize.Initialize();
        }

        // 一時 mod の辞書へ 3 種の GUID 導出キーを入れて合成する（前例 LocalizeContentTest）
        // Compose a temporary mod dictionary carrying the three GUID-derived keys (precedent: LocalizeContentTest)
        private void MergeContentNames()
        {
            var modDirectory = Path.Combine(_temporaryDirectory, "placement-names-mod");
            Directory.CreateDirectory(Path.Combine(modDirectory, "master"));
            Directory.CreateDirectory(Path.Combine(modDirectory, "localization"));
            File.WriteAllText(
                Path.Combine(modDirectory, "master", "modMeta.json"),
                "{\"id\":\"placement-names\",\"name\":\"placement-names\",\"version\":\"1.0\",\"author\":\"author\",\"description\":\"test\"}");
            File.WriteAllText(
                Path.Combine(modDirectory, "localization", "localization.csv"),
                "key,Source,english,japanese\n" +
                $"{ContentLocalizationKeys.BlockName(BlockGuid).Key},Wooden Chest,Wooden Chest,木のチェスト\n" +
                $"{ContentLocalizationKeys.ConnectToolName(ConnectToolGuid).Key},Wire Tool,Wire Tool,電線ツール\n" +
                $"{ContentLocalizationKeys.TrainCarName(TrainCarGuid).Key},Steam Locomotive,Steam Locomotive,蒸気機関車\n");
            Localize.MergeGameDictionaries(new ModsResource(_temporaryDirectory), new[] { new ModId("author:placement-names") }, MasterSourceTextCollector.Collect());
        }
    }
}
