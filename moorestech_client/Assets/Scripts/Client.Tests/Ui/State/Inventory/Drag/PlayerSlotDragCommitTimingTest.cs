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
    /// 分割ドラッグの確定の時機。解放は確定待ちの印だけ立て LateTick で確定し、解放を取りこぼした session は次の左操作で捨てる
    /// Split-drag commit timing; a release only marks the commit pending for LateTick, and a session that missed its release is dropped on the next left action
    /// </summary>
    public class PlayerSlotDragCommitTimingTest
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
        public void 開始の時点でボタンが離れていてもLateTickで一度だけ確定する()
        {
            // 同じフレームで押下と解放が届くと、解放の流れが UI の押下より先に走る
            // When press and release arrive in one frame, the release stream runs before the UI press
            _operations.SetGrab(new SlotItem(Wood, 6));
            _button.Release();

            _commands.BeginSplitDrag(PlayerSlotRef.Main(3));
            Assert.IsEmpty(_operations.Moves, "LateTick までは配らない");
            _commands.LateTick();
            _commands.LateTick();

            Assert.AreEqual(6, _operations.ReadMain(3).Count);
            Assert.AreEqual(1, _operations.Moves.Count, "確定済みなので次の LateTick は何もしない");
        }

        [Test]
        public void 同じフレームで解放の後に届いた進入の枠にも配る()
        {
            // InputSystem の解放は同じフレームの UITK の進入より先に流れるので、最後の枠は解放の後に届く
            // The InputSystem release runs before this frame's UITK enter, so the last slot arrives after the release
            _operations.SetGrab(new SlotItem(Wood, 9));

            _commands.BeginSplitDrag(PlayerSlotRef.Main(3));
            _commands.EnterWithLeftButton(PlayerSlotRef.Main(4));
            _button.Release();
            _commands.EnterWithLeftButton(PlayerSlotRef.Main(5));
            Assert.IsEmpty(_operations.Moves, "解放の購読では配らない");
            _commands.LateTick();

            Assert.AreEqual(3, _operations.ReadMain(3).Count);
            Assert.AreEqual(3, _operations.ReadMain(4).Count);
            Assert.AreEqual(3, _operations.ReadMain(5).Count);
        }

        [Test]
        public void 同じフレームで前の解放の後に次の開始が届くと前のsessionを先に確定する()
        {
            // 低 fps では前のドラッグの解放と次の押下が同じフレームに届き、解放が UI の押下より先に流れる
            // At low fps the previous drag's release and the next press share a frame, and the release runs before the UI press
            _operations.SetGrab(new SlotItem(Wood, 10));
            _commands.BeginSplitDrag(PlayerSlotRef.Main(3));
            _commands.EnterWithLeftButton(PlayerSlotRef.Main(4));
            _button.Release();
            _button.SetPressed(true);

            _commands.BeginSplitDrag(PlayerSlotRef.Main(6));
            Assert.AreEqual(new[] { "Grab->Main:3 x5", "Grab->Main:4 x5" }, _operations.Moves.ToArray(), "前の session の枠へ配る");
            _commands.LateTick();

            Assert.AreEqual(2, _operations.Moves.Count, "押したままの新しい session は LateTick で確定しない");
        }

        [Test]
        public void 解放を取りこぼした分割ドラッグは次の左操作で捨てて配らない()
        {
            // フォーカス喪失のデバイスリセット等で解放が流れず、本番の押下（マウスの実状態）だけが下りて session が残る
            // A device reset on focus loss etc. emits no release; only the production pressed state (actual mouse) goes up, leaving the session
            _operations.SetGrab(new SlotItem(Wood, 10));
            _commands.BeginSplitDrag(PlayerSlotRef.Main(3));
            _commands.EnterWithLeftButton(PlayerSlotRef.Main(4));
            _button.SetPressed(false);
            _commands.LateTick();
            LogAssert.Expect(LogType.Warning, new Regex(@"stale split-drag session discarded"));

            _commands.EnterWithLeftButton(PlayerSlotRef.Main(5));
            ReleaseAndLateTick();

            Assert.IsEmpty(_operations.Moves);
            Assert.IsEmpty(_shownToasts);
        }

        [Test]
        public void 解放を取りこぼした分割ドラッグは次の開始で捨てて新しい枠だけに配る()
        {
            _operations.SetGrab(new SlotItem(Wood, 10));
            _commands.BeginSplitDrag(PlayerSlotRef.Main(3));
            _commands.EnterWithLeftButton(PlayerSlotRef.Main(4));
            _button.SetPressed(false);
            LogAssert.Expect(LogType.Warning, new Regex(@"stale split-drag session discarded"));

            _commands.BeginSplitDrag(PlayerSlotRef.Main(6));
            _commands.LateTick();

            Assert.AreEqual(new[] { "Grab->Main:6 x10" }, _operations.Moves.ToArray());
        }

        [Test]
        public void マウスがリセットされた分割ドラッグは後の解放で配らない()
        {
            // 解放を取りこぼした後に枠以外をクリックして離しても、古い session の枠へ配らない
            // Even if the player clicks and releases outside slots after the missed release, the old session's slots get nothing
            _operations.SetGrab(new SlotItem(Wood, 10));
            _commands.BeginSplitDrag(PlayerSlotRef.Main(3));
            _commands.EnterWithLeftButton(PlayerSlotRef.Main(4));
            LogAssert.Expect(LogType.Warning, new Regex(@"split-drag session discarded on mouse reset"));
            _button.Reset();
            _commands.LateTick();

            _button.SetPressed(true);
            ReleaseAndLateTick();

            Assert.IsEmpty(_operations.Moves);
            Assert.IsEmpty(_shownToasts);
        }

        private void ReleaseAndLateTick()
        {
            _button.Release();
            _commands.LateTick();
        }
    }
}
