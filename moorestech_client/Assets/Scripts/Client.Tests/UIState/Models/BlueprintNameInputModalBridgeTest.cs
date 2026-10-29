using System.Collections.Generic;
using Client.Game.InGame.UI.Blueprint;
using Client.Ui.State.Blueprint;
using Client.Ui.State.Modal;
using Client.Localization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.UIState.Models
{
    /// <summary>
    ///     BP名入力とモーダル要求の橋渡しを検証するテスト
    ///     Tests for the bridge between blueprint-name input and modal requests
    /// </summary>
    public class BlueprintNameInputModalBridgeTest
    {
        [SetUp]
        public void SetUp()
        {
            Localize.Initialize();
        }

        [Test]
        public void 保留中に要求が置き換わっても新しい要求は取り消されない()
        {
            var state = new BlueprintNameInputState();
            var modal = new ModalRequestService();
            new BlueprintNameInputModalBridge(state, modal).Initialize();
            var openLog = new List<bool>();
            using var subscription = state.OnOpenChanged.Subscribe(openLog.Add);

            // 入力中にもう一度開くと、旧要求は置き換えで cancel 解決される
            // Reopening while naming cancel-resolves the old request by replacement
            state.Open();
            var firstId = modal.Pending.Id;
            state.Open();

            Assert.IsNotNull(modal.Pending, "旧要求の cancel 継続が新しい入力モーダルまで取り消した");
            Assert.AreNotEqual(firstId, modal.Pending.Id);
            CollectionAssert.DoesNotContain(openLog, false, "置き換えで BP 名入力が閉じた");
        }

        [Test]
        public void 自分の要求の取消ではBP名入力を閉じる()
        {
            var state = new BlueprintNameInputState();
            var modal = new ModalRequestService();
            new BlueprintNameInputModalBridge(state, modal).Initialize();
            var openLog = new List<bool>();
            using var subscription = state.OnOpenChanged.Subscribe(openLog.Add);

            state.Open();
            modal.CancelPendingRequest();

            Assert.IsNull(modal.Pending);
            CollectionAssert.AreEqual(new[] { true, false }, openLog);
        }

        [Test]
        public void 同期購読者が要求を差し替えても差し替えた要求は取り消されない()
        {
            var state = new BlueprintNameInputState();
            var modal = new ModalRequestService();
            new BlueprintNameInputModalBridge(state, modal).Initialize();
            var openLog = new List<bool>();
            using var subscription = state.OnOpenChanged.Subscribe(openLog.Add);

            // 入力モーダルが出た瞬間に、同期購読者が別の要求へ差し替える
            // A synchronous subscriber replaces the input modal the moment it appears
            var replaced = false;
            using var replacer = modal.OnPendingChanged.Subscribe(_ =>
            {
                if (replaced || modal.Pending == null || !modal.Pending.RequiresInput) return;
                replaced = true;
                modal.RequestModal("replacing", "replacing", "OK", ModalVariant.Error).Forget();
            });
            state.Open();

            Assert.IsTrue(replaced);
            Assert.IsNotNull(modal.Pending, "橋渡しが差し替えた側の要求IDを自分のものと取り違え、差し替えた要求を取り消した");
            Assert.IsFalse(modal.Pending.RequiresInput);
            CollectionAssert.DoesNotContain(openLog, false, "置き換えで BP 名入力が閉じた");
        }

        [Test]
        public void 同期購読者が入力要求をその場で取り消すとBP名入力を閉じる()
        {
            var state = new BlueprintNameInputState();
            var modal = new ModalRequestService();
            new BlueprintNameInputModalBridge(state, modal).Initialize();
            var openLog = new List<bool>();
            using var subscription = state.OnOpenChanged.Subscribe(openLog.Add);

            // 入力モーダルが出た瞬間に、同期購読者がその要求を取り消す
            // A synchronous subscriber cancels the input modal the moment it appears
            using var canceller = modal.OnPendingChanged.Subscribe(_ =>
            {
                if (modal.Pending != null && modal.Pending.RequiresInput) modal.CancelPendingRequest();
            });
            state.Open();

            Assert.IsNull(modal.Pending);
            CollectionAssert.Contains(openLog, false, "自分の要求が取り消されたのに BP 名入力が開いたまま残った");
        }
    }
}
