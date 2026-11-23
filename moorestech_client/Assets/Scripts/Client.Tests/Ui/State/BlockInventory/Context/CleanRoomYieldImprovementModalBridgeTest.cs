using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Client.Ui.State.Modal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 「失敗率の改善方法」のモーダルを押した時点の中身で開く（ADR 0105 §4）
    /// Opens the improvement modal with the contents at press time (ADR 0105 §4)
    /// </summary>
    public class CleanRoomYieldImprovementModalBridgeTest
    {
        private const string Minus = "−";
        private FakeBlockContextModel _context;
        private ModalRequestService _modal;
        private CleanRoomYieldImprovementModalBridge _bridge;

        [SetUp]
        public void SetUp()
        {
            Localize.Initialize();
            _context = new FakeBlockContextModel();
            _modal = new ModalRequestService();
            _bridge = new CleanRoomYieldImprovementModalBridge(_context, _modal);
        }

        [Test]
        public void 開くと今の値を本文に_改善ごとの項目名と差分を行にして出す()
        {
            _context.Set(Dicing(0.505));
            _bridge.Open();

            var pending = _modal.Pending;
            Assert.AreEqual("How to lower the failure rate", pending.Title);
            Assert.AreEqual("Now: Lv1 42.3% / Lv2 7.2% / Failed 50.5%", pending.Message);
            Assert.AreEqual("Close", pending.ButtonText);
            Assert.IsFalse(pending.RequiresInput);
            Assert.AreEqual(2, pending.Rows.Count);
            Assert.AreEqual("Raise room class to B", pending.Rows[0].Label);
            Assert.AreEqual($"Lv1 +9.2% / Lv2 +3.8% / Failed {Minus}13.1%", pending.Rows[0].Values);
            Assert.AreEqual("Add quality module Lv2", pending.Rows[1].Label);
            Assert.AreEqual($"Lv1 {Minus}36.9% / Lv2 +37.0% / Lv3 +8.2% / Failed {Minus}8.2%", pending.Rows[1].Values);
        }

        [Test]
        public void 開くまでは取り直しがあってもモーダルを出さない()
        {
            _context.Set(Dicing(0.505));
            _context.Set(Dicing(0.4));
            Assert.IsNull(_modal.Pending);
        }

        [Test]
        public void 開いている間に取り直しがあっても中身を変えない()
        {
            _context.Set(Dicing(0.505));
            _bridge.Open();
            var opened = _modal.Pending;

            _context.Set(Dicing(0.48));
            _context.Set(BlockContextViewModel.None);

            Assert.AreEqual(opened.Id, _modal.Pending.Id);
            Assert.AreEqual("Now: Lv1 42.3% / Lv2 7.2% / Failed 50.5%", _modal.Pending.Message);
        }

        [Test]
        public void 改善の行が無いときに開いても出さず警告だけを残す()
        {
            _context.Set(WithOdds(CleanRoomYieldOdds.LayerExposure(0.01, new List<CleanRoomOddsImprovement>())));
            LogAssert.Expect(LogType.Warning, "[CleanRoomYieldImprovementModalBridge] open ignored: the tracked machine has no improvement rows now");
            _bridge.Open();
            Assert.IsNull(_modal.Pending);
        }

        [Test]
        public void 機械の画面を離れると開いたモーダルを閉じる()
        {
            _context.Set(Dicing(0.505));
            _bridge.Open();

            _bridge.CloseUnlessMachineScreen(UIStateEnum.GameScreen);

            Assert.IsNull(_modal.Pending);
        }

        [Test]
        public void 機械の画面に居る間は閉じない()
        {
            _context.Set(Dicing(0.505));
            _bridge.Open();

            _bridge.CloseUnlessMachineScreen(UIStateEnum.SubInventory);

            Assert.IsNotNull(_modal.Pending);
        }

        [Test]
        public void 機械の画面を離れても別のモーダルは閉じない()
        {
            _context.Set(Dicing(0.505));
            _bridge.Open();
            _modal.RequestInputModal("別の要求", "本文", "保存");
            var other = _modal.Pending;

            _bridge.CloseUnlessMachineScreen(UIStateEnum.GameScreen);

            Assert.AreEqual(other.Id, _modal.Pending.Id);
        }

        // ADR 0105 §4 の形の例（ダイシング300個・Lv2は7.2%・Lv1は失敗との差し引き）
        // An ADR 0105 §4 shaped example (300 dies, Lv2 at 7.2%, and Lv1 balancing the supplied failure share)
        private static BlockContextViewModel Dicing(double failedRate)
        {
            var improvements = new List<CleanRoomOddsImprovement>
            {
                CleanRoomOddsImprovement.RoomClassUp("B", new[] { 0.092, 0.038, 0.0 }, -0.131),
                CleanRoomOddsImprovement.AddQualityModule(2, new[] { -0.369, 0.370, 0.082 }, -0.082),
            };
            return WithOdds(CleanRoomYieldOdds.Dicing(300, new[] { 0.928 - failedRate, 0.072, 0.0 }, failedRate, improvements));
        }

        private static BlockContextViewModel WithOdds(CleanRoomYieldOdds odds)
        {
            return new BlockContextViewModel(SectionContent<CleanRoomFetchedDetail>.Absent, SectionContent<CleanRoomYieldOdds>.Present(odds), SectionContent<MachineInputStall>.Absent);
        }
    }
}
