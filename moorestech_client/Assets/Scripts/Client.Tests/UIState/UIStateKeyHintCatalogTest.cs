using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.TrainHUDScreen;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     ADR-0032の画面別内容表どおりにヒントが宣言されているかを固定する
    ///     Pins each screen's hint declaration to the content table in ADR-0032
    /// </summary>
    public class UIStateKeyHintCatalogTest
    {
        [Test]
        public void GameScreenHintsMatchAdr()
        {
            var expected = new[]
            {
                (LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Inventory),
                (LocalizationKeys.Ui.KeyHint.Key.Digits, LocalizationKeys.Ui.KeyHint.Text.BuildShortcut),
                (LocalizationKeys.Ui.KeyHint.Key.B, LocalizationKeys.Ui.KeyHint.Text.BuildMenu),
                (LocalizationKeys.Ui.KeyHint.Key.G, LocalizationKeys.Ui.KeyHint.Text.DeleteMode),
                (LocalizationKeys.Ui.KeyHint.Key.R, LocalizationKeys.Ui.KeyHint.Text.ResearchTree),
                (LocalizationKeys.Ui.KeyHint.Key.V, LocalizationKeys.Ui.KeyHint.Text.ToggleView),
                (LocalizationKeys.Ui.KeyHint.Key.LeftAltHold, LocalizationKeys.Ui.KeyHint.Text.FreeCursor),
                (LocalizationKeys.Ui.KeyHint.Key.LeftAltMiddleClick, LocalizationKeys.Ui.KeyHint.Text.PickPlacedObject),
            };
            AssertHints(expected, GameScreenStateHints.Hints);
        }

        [Test]
        public void PlaceBlockHintsUseImplementationDirectionForHeight()
        {
            var hints = PlaceBlockStateHints.Hints;
            var lower = hints.Single(h => h.TextKey.Key == LocalizationKeys.Ui.KeyHint.Text.LowerHeight.Key);
            var raise = hints.Single(h => h.TextKey.Key == LocalizationKeys.Ui.KeyHint.Text.RaiseHeight.Key);
            Assert.AreEqual(LocalizationKeys.Ui.KeyHint.Key.Q.Key, lower.KeyNameKey.Key);
            Assert.AreEqual(LocalizationKeys.Ui.KeyHint.Key.E.Key, raise.KeyNameKey.Key);
        }

        [Test]
        public void ChallengeListHasNoHints()
        {
            Assert.IsEmpty(ChallengeListStateHints.Hints);
        }

        // ADR-0032の表どおりの件数を全画面ぶん固定する
        // Pin the per-screen hint counts to the table in ADR-0032
        [Test]
        public void EveryScreenHintCountMatchesAdr()
        {
            Assert.AreEqual(9, PlaceBlockStateHints.Hints.Count);
            Assert.AreEqual(7, DeleteObjectStateHints.Hints.Count);
            Assert.AreEqual(6, PlayerInventoryStateHints.Hints.Count);
            Assert.AreEqual(5, SubInventoryStateHints.Hints.Count);
            Assert.AreEqual(2, ResearchTreeStateHints.Hints.Count);
            Assert.AreEqual(2, BuildMenuStateHints.Hints.Count);
            Assert.AreEqual(3, TrainHudGameScreenSubStateHints.Hints.Count);
        }

        // 自明除外の裁定（ESC・移動・デバッグキー）をキー名側で機械的に固定する
        // Mechanically pin the "obvious operations are excluded" ruling on the key-name side
        [Test]
        public void NoScreenDeclaresExcludedKeys()
        {
            var excluded = new[]
            {
                "escape", "f1", "f2", "f3", "ctrlU", "wasd", "space", "shift", "t",
            }.Select(name => $"ui.keyHint.key.{name}").ToArray();

            var all = new[]
            {
                GameScreenStateHints.Hints, PlaceBlockStateHints.Hints, DeleteObjectStateHints.Hints,
                PlayerInventoryStateHints.Hints, SubInventoryStateHints.Hints, ResearchTreeStateHints.Hints,
                BuildMenuStateHints.Hints, ChallengeListStateHints.Hints, TrainHudGameScreenSubStateHints.Hints,
            }.SelectMany(hints => hints).Select(hint => hint.KeyNameKey.Key);

            CollectionAssert.IsEmpty(all.Intersect(excluded).ToArray());
        }

        private static void AssertHints((LocalizationKey keyNameKey, LocalizationKey textKey)[] expected, IReadOnlyList<KeyHint> actual)
        {
            Assert.AreEqual(expected.Length, actual.Count, "hint count");
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].keyNameKey.Key, actual[i].KeyNameKey.Key, $"keyNameKey[{i}]");
                Assert.AreEqual(expected[i].textKey.Key, actual[i].TextKey.Key, $"textKey[{i}]");
            }
        }
    }
}
