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
                (LocalizationKeys.Ui.KeyHint.Key.MiddleClick, LocalizationKeys.Ui.KeyHint.Text.PickPlacedObject),
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

        // 自明除外の裁定（ESC・移動・左クリック・デバッグキー・T）を許可リストで固定する。
        // Pin the "obvious operations are excluded" ruling with an allow-list of declarable key names.
        // 除外キー名を直接列挙する形だと別名（esc/keyEsc等）で足された瞬間すり抜けるため、
        // A deny-list of key names would be bypassed the moment someone adds it under another name,
        // 宣言してよいキー名の側を閉じ、新キー追加時にこの一覧とADR-0032の裁定を必ず読ませる。
        // so the declarable side is closed instead, forcing any new key past this list and the ADR-0032 ruling.
        [Test]
        public void EveryDeclaredKeyNameIsOnTheAdrAllowList()
        {
            var allowed = new[]
            {
                LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Key.B,
                LocalizationKeys.Ui.KeyHint.Key.G, LocalizationKeys.Ui.KeyHint.Key.R,
                LocalizationKeys.Ui.KeyHint.Key.V, LocalizationKeys.Ui.KeyHint.Key.Q,
                LocalizationKeys.Ui.KeyHint.Key.E, LocalizationKeys.Ui.KeyHint.Key.Digits,
                LocalizationKeys.Ui.KeyHint.Key.CtrlZ, LocalizationKeys.Ui.KeyHint.Key.DriveKeys,
                LocalizationKeys.Ui.KeyHint.Key.BranchKeys, LocalizationKeys.Ui.KeyHint.Key.LeftAltHold,
                LocalizationKeys.Ui.KeyHint.Key.MiddleClick,
                LocalizationKeys.Ui.KeyHint.Key.LeftDrag, LocalizationKeys.Ui.KeyHint.Key.RightClick,
                LocalizationKeys.Ui.KeyHint.Key.DoubleClick, LocalizationKeys.Ui.KeyHint.Key.ShiftLeftClick,
            }.Select(key => key.Key).ToArray();

            var declared = AllHints.Select(hint => hint.KeyNameKey.Key).Distinct().ToArray();

            CollectionAssert.IsEmpty(declared.Except(allowed).ToArray(), "ADR-0032の許可リストに無いキー名が宣言されている");
        }

        private static readonly IReadOnlyList<KeyHint> AllHints = new[]
        {
            GameScreenStateHints.Hints, PlaceBlockStateHints.Hints, DeleteObjectStateHints.Hints,
            PlayerInventoryStateHints.Hints, SubInventoryStateHints.Hints, ResearchTreeStateHints.Hints,
            BuildMenuStateHints.Hints, ChallengeListStateHints.Hints, TrainHudGameScreenSubStateHints.Hints,
        }.SelectMany(hints => hints).ToArray();

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
