using System.Collections.Generic;
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.Modal;
using Client.Localization;
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
    }
}
