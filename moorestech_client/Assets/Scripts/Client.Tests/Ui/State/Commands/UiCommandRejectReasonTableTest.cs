using System;
using Client.Localization;
using Client.Ui.State.Commands;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Commands
{
    public class UiCommandRejectReasonTableTest
    {
        // 全ての拒否理由が表に 1 行ずつあり、失敗コードと文言のキーが空でない
        // Every reject reason has one row whose failure code and message key are not empty
        [Test]
        public void 全ての拒否理由が表に1行ずつある()
        {
            foreach (UiCommandRejectReason reason in Enum.GetValues(typeof(UiCommandRejectReason)))
            {
                var row = UiCommandRejectReasonTable.Get(reason);
                Assert.IsFalse(string.IsNullOrEmpty(row.WireCode), $"{reason} の失敗コードが空");
                Assert.IsFalse(string.IsNullOrEmpty(row.LocalizationKey.Key), $"{reason} の文言のキーが空");
            }
        }

        // 以下の 2 本は PauseMenuCommandsTest から本文を変えずに移す（良性の一覧・トーストの翻訳文の全走査）
        // The two tests below move from PauseMenuCommandsTest unchanged (benign list and toast-text full scans)
        [Test]
        public void 良性の拒否理由は宣言した一覧と一致する()
        {
            // 選択済みの再送・持ち物の取りこぼし・特殊族の古い操作・空の掴みのフィルタ設定・閉じた後の選択・消えた BP の削除・受理外のホットバー選択・古いモーダル応答は利用者に知らせない（ログだけ）
            // Resends, inventory misses, special-family stale operations, filter sets with an empty grab, selections after close, deletes of vanished blueprints, non-accepting hotbar taps and stale modal replies are not told to players (log only)
            var benign = new[] { UiCommandRejectReason.LanguageAlreadySelected, UiCommandRejectReason.ItemSourceSlotEmpty, UiCommandRejectReason.ItemSourceCountShort, UiCommandRejectReason.SplitGrabNotEmpty, UiCommandRejectReason.SpecialBlockCommandStale, UiCommandRejectReason.FilterSplitterGrabEmpty, UiCommandRejectReason.BuildMenuNotOpen, UiCommandRejectReason.UnknownBuildMenuEntry, UiCommandRejectReason.BlueprintDeleteNotFound, UiCommandRejectReason.HotbarSelectNotAccepted, UiCommandRejectReason.ModalResponseStale };
            foreach (UiCommandRejectReason reason in System.Enum.GetValues(typeof(UiCommandRejectReason)))
            {
                Assert.AreEqual(0 <= System.Array.IndexOf(benign, reason), UiCommandRejectReasonTraits.IsBenign(reason), reason.ToString());
            }
        }

        [Test]
        public void どの拒否理由もトースト用の翻訳文を持つ()
        {
            // 単独実行でも辞書が引けるように初期化する
            // Initialize the dictionary so the test also passes when run alone
            Localize.Initialize();
            foreach (UiCommandRejectReason reason in System.Enum.GetValues(typeof(UiCommandRejectReason)))
            {
                var text = Localize.Get(UiCommandRejectReasonTraits.ToLocalizationKey(reason));
                Assert.IsNotEmpty(text, reason.ToString());
                StringAssert.DoesNotContain("_", text, $"{reason} の文言が失敗コードのまま");
                StringAssert.DoesNotStartWith("[!", text, $"{reason} の翻訳キーが辞書に無い");
            }
        }
    }
}
