using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.UIState;
using Client.Game.Localization;
using Client.Ui.State.ModeHud;
using Game.Blueprint;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.ModeHud
{
    /// <summary>
    /// 配置 HUD の対象名は今の辞書で解いた DisplayName（L1）
    /// Target name is DisplayName resolved with the current dictionary (L1)
    /// </summary>
    public class ModeHudPlacementNameTest
    {
        private static readonly Guid BlockGuid = new("30000000-0000-4000-8000-000000000001");
        private static readonly Guid ConnectToolGuid = new("40000000-0000-4000-8000-000000000001");
        private static readonly Guid TrainCarGuid = new("8f9c2a51-0000-4000-8000-000000000001");
        private PlacementNameTestDictionary _dictionary;

        [SetUp]
        public void SetUp()
        {
            // マスタを読み、日本語で導出キーの辞書を合成する（言語を変えると合成が消えるので順は 言語 → 合成）
            // Load the master and compose the derived-key dictionary in Japanese (changing language drops the merge, so language first)
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _dictionary = new PlacementNameTestDictionary();
            _dictionary.UseLanguage("japanese");
            _dictionary.Merge(
                $"{ContentLocalizationKeys.BlockName(BlockGuid).Key},Assembler,Localized Assembler,組立機\n" +
                $"{ContentLocalizationKeys.ConnectToolName(ConnectToolGuid).Key},Wire Connect Tool,Wire Connect Tool,電線接続ツール\n" +
                $"{ContentLocalizationKeys.TrainCarName(TrainCarGuid).Key},Freight Car,Freight Car,貨物車両\n");
        }

        [TearDown]
        public void TearDown()
        {
            _dictionary.Restore();
        }

        /// <web-test file="src/features/modeHud/PlacementModeHud.localization.test.ts">block Guidを現在辞書の表示名へ解決する</web-test>
        [Test]
        public void ブロックは今の辞書の表示名になる()
        {
            Assert.AreEqual("組立機", TargetName(new BlockPlacementTarget(BlockGuid, null)));
        }

        /// <web-test file="src/features/modeHud/PlacementModeHud.localization.test.ts">block以外のraw表示名をそのまま維持する</web-test>
        [Test]
        public void 保存したブループリントは付けた名前のまま()
        {
            var guid = Guid.NewGuid();
            var blueprint = new BlueprintJsonObject("My Blueprint", new(), new(), new(), guid);
            Assert.AreEqual("My Blueprint", TargetName(new BlueprintPlacementTarget(guid, "My Blueprint", blueprint)));
        }

        /// <web-test file="src/features/modeHud/PlacementModeHud.localization.test.ts">connectTool GuidをGuid導出キーの表示名へ解決する</web-test>
        /// <web-test file="e2e/tests/modeHud/operation-mode-hud.spec.ts">配置対象connectToolをGuidだけの配信から辞書表示名へ解決する</web-test>
        [Test]
        public void 接続ツールはGUIDから導いたキーの表示名になる()
        {
            Assert.AreEqual("電線接続ツール", TargetName(new ConnectToolPlacementTarget(ConnectToolGuid)));
        }

        /// <web-test file="src/features/modeHud/PlacementModeHud.localization.test.ts">trainCar GuidをGuid導出キーの表示名へ解決する</web-test>
        /// <web-test file="e2e/tests/modeHud/operation-mode-hud.spec.ts">配置対象trainCarをGuidだけの配信から辞書表示名へ解決する</web-test>
        [Test]
        public void 列車車両はGUIDから導いたキーの表示名になる()
        {
            Assert.AreEqual("貨物車両", TargetName(new TrainCarPlacementTarget(TrainCarGuid)));
        }

        /// <web-test file="src/features/modeHud/PlacementModeHud.localization.test.ts">Blueprint Copyをtyped UI keyで解決する</web-test>
        [TestCase("english", "Blueprint Copy")]
        [TestCase("japanese", "ブループリントコピー")]
        public void ブループリントコピーはUIの辞書キーで解く(string languageCode, string expected)
        {
            _dictionary.UseLanguage(languageCode);
            Assert.AreEqual(expected, TargetName(new BlueprintCopyPlacementTarget(Guid.NewGuid())));
        }

        private static string TargetName(IPlacementTarget target)
        {
            var placement = (ModeHudPlacement)ModeHudViewModelFactory.Create(UIStateEnum.PlaceBlock, target, 0);
            Assert.IsTrue(placement.Target.HasValue);
            return placement.Target.Value;
        }
    }
}
