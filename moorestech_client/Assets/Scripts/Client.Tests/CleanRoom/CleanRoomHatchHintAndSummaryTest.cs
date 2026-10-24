using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.CleanRoom;
using Client.Game.InGame.Interact.Tap;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Localization;
using Client.Tests.Common;
using Client.Tests.Interact;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.CleanRoom
{
    /// <summary>
    ///     ハッチのように操作案内が常に出る境界ブロックで、案内の下に部屋の要約（密閉状態・漏れ箇所）が併記されることを固定する
    ///     Pins that a boundary block whose interact hint is always shown, such as a hatch, lists the room summary (sealed state, leaks) under the hint
    /// </summary>
    public class CleanRoomHatchHintAndSummaryTest : InputTestFixture
    {
        private MouseCursorTooltipState _tooltip;
        private GameObject _hatchObject;

        public override void Setup()
        {
            base.Setup();
            TestReflection.ResetInputManagerCache();
            Localize.Initialize();
            _tooltip = new MouseCursorTooltipState();
            _hatchObject = new GameObject("ItemHatch");
        }

        public override void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_hatchObject);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void HatchShowsOpenHintAndThenSealedRoomSummaryTest()
        {
            var (driver, hatch, selector) = CreateAimedHatch();
            var presenter = new CleanRoomTooltipPresenter(_tooltip);

            // 案内を出したあとに密閉済みの要約が届くと、案内の下に部屋の3行が並ぶ
            // A sealed summary arriving after the hint lists the room's three lines under it
            driver.Step(hatch, selector.Scan(), Array.Empty<InputKey>());
            var filter = new CleanRoomAirFilterMessagePack(new Vector3IntMessagePack(1, 1, 1), 1d, 10d, 5, 1d);
            var room = CleanRoomSummaryMessagePack.CreateClassified("A", 0, 0.5d, 2d, 26, new List<CleanRoomAirFilterMessagePack> { filter });
            presenter.Present(CleanRoomTooltipLines.Build(new CleanRoomEnclosedView(new List<CleanRoomSummaryMessagePack> { room })));

            var tooltip = LinesOf(_tooltip);
            Assert.AreEqual(4, tooltip.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.InteractOpenBlock.Key, tooltip[0].Key.Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.CleanRoomClass.Key, tooltip[1].Key.Key);
            CollectionAssert.AreEqual(new[] { "A" }, tooltip[1].TextParams);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.CleanRoomAir.Key, tooltip[2].Key.Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.CleanRoomFilters.Key, tooltip[3].Key.Key);
        }

        [Test]
        public void SummaryFirstThenHatchHintKeepsLeakLineAndClearingHintKeepsSummaryTest()
        {
            var (driver, hatch, selector) = CreateAimedHatch();
            var presenter = new CleanRoomTooltipPresenter(_tooltip);

            // 要約が先に出ていても、後から出た案内に消されず、漏れ箇所の件数が案内の下に残る
            // Even with the summary first, the later hint does not erase it and the leak count stays under the hint
            var cells = new List<Vector3IntMessagePack> { new(0, 0, 0), new(1, 0, 0) };
            presenter.Present(CleanRoomTooltipLines.Build(new CleanRoomNotEnclosedView(new CleanRoomOpeningsLeakView(cells, 3))));
            driver.Step(hatch, selector.Scan(), Array.Empty<InputKey>());

            var tooltip = LinesOf(_tooltip);
            CollectionAssert.AreEqual(new[] { LocalizationKeys.Ui.Tooltip.InteractOpenBlock.Key, LocalizationKeys.Ui.Tooltip.CleanRoomNotSealed.Key }, tooltip.Select(line => line.Key.Key).ToArray());
            CollectionAssert.AreEqual(new[] { "3" }, tooltip[1].TextParams);

            // 案内が畳まれても要約は残り、要約を消すと何も出ない
            // Folding the hint keeps the summary, and hiding the summary leaves nothing
            driver.Clear();
            CollectionAssert.AreEqual(new[] { LocalizationKeys.Ui.Tooltip.CleanRoomNotSealed.Key }, LinesOf(_tooltip).Select(line => line.Key.Key).ToArray());
            presenter.Hide();
            Assert.IsFalse(_tooltip.GetPresentation().Visible);
        }

        private (TapInteractionDriver driver, StubTapInteractable hatch, ScriptedInteractTargetSelector selector) CreateAimedHatch()
        {
            // ハッチは「開く」の案内を持つ単押し対象として照準される
            // The hatch is aimed as a tap target offering the open hint
            InputSystem.AddDevice<Keyboard>();
            var hatch = new StubTapInteractable(_hatchObject, new StubTapInteractAction(InputManager.Playable.Interact, LocalizationKeys.Ui.Tooltip.InteractOpenBlock, UIStateEnum.SubInventory));
            var selector = new ScriptedInteractTargetSelector();
            selector.SetNext(hatch);
            InputSystem.Update();
            return (new TapInteractionDriver(_tooltip), hatch, selector);
        }

        private static IReadOnlyList<TooltipLine> LinesOf(MouseCursorTooltipState tooltip)
        {
            return tooltip.GetPresentation().Lines;
        }
    }
}
