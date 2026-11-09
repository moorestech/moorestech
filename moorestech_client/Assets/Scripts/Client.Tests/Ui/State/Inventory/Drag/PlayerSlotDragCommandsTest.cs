using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Localization;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Drag;
using Client.Ui.State.Items;
using Client.Ui.State.Toast;
using Core.Master;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Inventory.Drag
{
    /// <summary>
    /// 進入（左・右ボタン）と分割ドラッグの開始、左ボタンの解放での確定を Web の slotActions・splitDrag と同じ意味で実行する
    /// Runs enters (left/right button), split-drag begin and the commit on left release with the meaning of Web slotActions and splitDrag
    /// </summary>
    public class PlayerSlotDragCommandsTest
    {
        private static readonly ItemId Wood = new(1);
        private RecordingPlayerInventoryOperations _operations;
        private FakePrimaryPointerButton _button;
        private PlayerSlotDragCommands _commands;
        private List<GameUiToast> _shownToasts;

        [SetUp]
        public void SetUp()
        {
            // 拒否のトーストは文言を引くので言語表を読む
            // Rejection toasts look up text, so the language table is loaded
            Localize.Initialize();
            _operations = new RecordingPlayerInventoryOperations(9, 2);
            _button = new FakePrimaryPointerButton(true);
            var toasts = new GameUiToastFeed();
            _shownToasts = new List<GameUiToast>();
            toasts.OnToast.Subscribe(_shownToasts.Add);
            _commands = new PlayerSlotDragCommands(_operations, new PlayerItemActionExecutor(_operations), _button, new UiCommandResultToaster(toasts));
            _commands.Initialize();
        }

        [Test]
        public void 左ドラッグで通った行先へ解放の時に等分する()
        {
            _operations.SetGrab(new SlotItem(Wood, 10));

            _commands.BeginSplitDrag(PlayerSlotRef.Main(3));
            _commands.EnterWithLeftButton(PlayerSlotRef.Main(4));
            _commands.EnterWithLeftButton(PlayerSlotRef.Main(5));
            Assert.IsEmpty(_operations.Moves, "離すまでは配らない");
            ReleaseAndLateTick();

            Assert.AreEqual(3, _operations.ReadMain(3).Count);
            Assert.AreEqual(3, _operations.ReadMain(4).Count);
            Assert.AreEqual(3, _operations.ReadMain(5).Count);
            Assert.AreEqual(1, _operations.ReadGrab().Count);
            Assert.IsEmpty(_shownToasts);
        }

        [Test]
        public void 分割ドラッグ中でない左ボタンの進入と解放は何もしない()
        {
            _operations.SetGrab(new SlotItem(Wood, 10));

            _commands.EnterWithLeftButton(PlayerSlotRef.Main(4));
            ReleaseAndLateTick();

            Assert.IsEmpty(_operations.Moves);
            Assert.IsEmpty(_shownToasts);
        }

        [Test]
        public void 受けられる行先が無いと解放の時にトーストを出す()
        {
            _operations.SetGrab(new SlotItem(Wood, 10));
            _operations.SetMain(3, new SlotItem(new ItemId(2), 1));
            LogAssert.Expect(LogType.Warning, new Regex(@"inventory\.split_drag failed: no_valid_slots"));

            _commands.BeginSplitDrag(PlayerSlotRef.Main(3));
            ReleaseAndLateTick();

            Assert.AreEqual(1, _shownToasts.Count);
            Assert.IsEmpty(_operations.Moves);
        }

        [Test]
        public void 確定の時に範囲外の古い番地は警告して除き残りへ配る()
        {
            // 枠数が縮んだ後の番地は拒否でなく警告と除外（D-16）。実行器は番地の範囲を見ないのでここで止める
            // Refs left stale by a shrunk slot count are warned and dropped, not rejected (D-16); the executor does not range-check
            _operations.SetGrab(new SlotItem(Wood, 10));
            LogAssert.Expect(LogType.Warning, new Regex(@"split-drag dropped 1 stale slot"));

            _commands.BeginSplitDrag(PlayerSlotRef.Main(20));
            _commands.EnterWithLeftButton(PlayerSlotRef.Main(4));
            ReleaseAndLateTick();

            Assert.AreEqual(new[] { "Grab->Main:4 x10" }, _operations.Moves.ToArray());
            Assert.IsEmpty(_shownToasts);
        }

        private void ReleaseAndLateTick()
        {
            _button.Release();
            _commands.LateTick();
        }

        [Test]
        public void 右ボタンの進入はgrabから1個ずつ置く()
        {
            _operations.SetGrab(new SlotItem(Wood, 10));

            var first = _commands.EnterWithRightButton(PlayerSlotRef.Main(3));
            var second = _commands.EnterWithRightButton(PlayerSlotRef.Main(4));

            Assert.IsInstanceOf<UiCommandAccepted>(first);
            Assert.IsInstanceOf<UiCommandAccepted>(second);
            Assert.AreEqual(new[] { "Grab->Main:3 x1", "Grab->Main:4 x1" }, _operations.Moves.ToArray());
        }

        [Test]
        public void 何も持っていない右ボタンの進入は何もしない()
        {
            var result = _commands.EnterWithRightButton(PlayerSlotRef.Main(3));

            Assert.IsInstanceOf<UiCommandAccepted>(result);
            Assert.IsEmpty(_operations.Moves);
        }
    }
}
