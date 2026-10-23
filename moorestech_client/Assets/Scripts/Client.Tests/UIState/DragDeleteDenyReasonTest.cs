using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
using Client.Tests.BuildUndo;
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.UIState.Fakes;
using Client.Localization;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     削除拒否理由が下流の辞書引きへ安全に流れることを検証するテスト
    ///     Tests verifying that delete deny reasons flow safely into the downstream dictionary lookup
    /// </summary>
    public class DragDeleteDenyReasonTest
    {
        [SetUp]
        public void SetUp()
        {
            Localize.Initialize();
        }

        [Test]
        public void CablePoolFullResponseResolvesToItsLocalizedReason()
        {
            // サーバーの撤去拒否を本番の理由変換と辞書引きまで通す
            // Pass the server removal refusal through the production reason mapping and dictionary
            var method = typeof(Client.Game.InGame.Block.BlockGameObjectChild).GetMethod("GetReason",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var key = (LocalizationKey)method.Invoke(null, new object[] { Server.Protocol.PacketResponse.RemoveBlockProtocol.RemoveBlockFailureReason.CablePoolFull });
            Assert.AreEqual(LocalizationKeys.Ui.Delete.CablePoolFull, key);
            Assert.AreNotEqual(Localize.Get(LocalizationKeys.Ui.Delete.BlockDeleteFailed), Localize.Get(key));
            Assert.IsNotEmpty(Localize.Get(key));
        }

        [Test]
        public void ReasonlessDenialNeverReachesDictionaryLookup()
        {
            // 理由キー無しの拒否はnullで表現され、表示経路が辞書引きへ到達しない
            // A reasonless denial is expressed as null so the display path never reaches the lookup
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
            var target = new FakeDeleteTarget { Removable = false, DenyReason = null };

            selection.BeginStroke(DeleteSelectionIntent.Add);
            var outcome = selection.TryAddTarget(target);
            var added = outcome.IsAdded;
            var denyReason = outcome.DenyReason;

            // 先に表示経路をなぞり、空キーが流れた場合の辞書引き例外をそのまま失敗として捕まえる
            // Walk the display path first so a leaked empty key surfaces as the lookup exception itself
            Assert.AreEqual(string.Empty, ResolveLikeDeleteObjectService(denyReason));
            Assert.IsFalse(added);
            Assert.IsFalse(denyReason.HasValue);
        }

        [Test]
        public void CanceledSelectionDenialCarriesNoReasonKey()
        {
            // ESCキャンセル後の拒否も理由なし拒否として安全に扱える
            // A denial after an ESC cancel is also handled safely as a reasonless denial
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
            var target = new FakeDeleteTarget { Removable = true };

            selection.BeginStroke(DeleteSelectionIntent.Add);
            selection.CancelSelection();
            var outcome = selection.TryAddTarget(target);
            var added = outcome.IsAdded;
            var denyReason = outcome.DenyReason;

            Assert.IsFalse(added);
            Assert.IsFalse(denyReason.HasValue);
            Assert.AreEqual(string.Empty, ResolveLikeDeleteObjectService(denyReason));
        }

        [Test]
        public void DenialWithReasonKeyResolvesToLocalizedText()
        {
            // 理由キーがある拒否は従来どおり文言まで解決される
            // A denial that carries a reason key still resolves all the way to its wording
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
            var target = new FakeDeleteTarget
            {
                Removable = false,
                DenyReason = LocalizationKeys.Ui.Delete.RailHasVehicle,
            };

            selection.BeginStroke(DeleteSelectionIntent.Add);
            var outcome = selection.TryAddTarget(target);
            var added = outcome.IsAdded;
            var denyReason = outcome.DenyReason;

            Assert.IsFalse(added);
            Assert.IsTrue(denyReason.HasValue);
            Assert.AreEqual(LocalizationKeys.Ui.Delete.RailHasVehicle, denyReason.Value);
            Assert.AreEqual(
                Localize.Get(LocalizationKeys.Ui.Delete.RailHasVehicle),
                ResolveLikeDeleteObjectService(denyReason));
        }

        [Test]
        public void EmptyLocalizationKeyBreaksTheDictionaryLookup()
        {
            // 空キーは辞書引きで落ちるため、理由なし拒否はnullでしか安全に表せない
            // An empty key breaks the lookup, so a reasonless denial can only be expressed safely as null
            Assert.Throws<ArgumentNullException>(() => Localize.Get(default(LocalizationKey)));
        }

        // DeleteObjectService.ShowDenyReasonと同じ判定順で理由文言を解決する
        // Resolve the reason wording with the same ordering as DeleteObjectService.ShowDenyReason
        private static string ResolveLikeDeleteObjectService(LocalizationKey? denyReason)
        {
            if (!denyReason.HasValue) return string.Empty;
            return Localize.Get(denyReason.Value);
        }
    }
}
