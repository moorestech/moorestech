using System.Collections.Generic;
using Client.Localization;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Client.Ui.State.Modal;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 「失敗率の改善方法」のモーダルの開閉と、取り直しでの出し直し（ADR 0105 §4）
    /// Opening and closing the improvement modal and re-issuing it on refetch (ADR 0105 §4)
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
            _bridge.Initialize();
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
        public void 開いている間は中身が変われば出し直し_同じなら出し直さない()
        {
            _context.Set(Dicing(0.505));
            _bridge.Open();
            var firstId = _modal.Pending.Id;

            // 同じ値の取り直し（別の実体）では要求を替えない
            // A refetch with equal values (a new instance) keeps the request
            _context.Set(Dicing(0.505));
            Assert.AreEqual(firstId, _modal.Pending.Id);

            // 失敗の割合が変わると新しい要求で出し直す
            // A changed failure share re-issues a new request
            _context.Set(Dicing(0.48));
            Assert.AreNotEqual(firstId, _modal.Pending.Id);
            Assert.AreEqual("Now: Lv1 44.8% / Lv2 7.2% / Failed 48.0%", _modal.Pending.Message);
        }

        [Test]
        public void 閉じるボタンで閉じた後は取り直しでも出さない()
        {
            _context.Set(Dicing(0.505));
            _bridge.Open();
            Assert.IsTrue(_modal.Respond(_modal.Pending.Id, ModalResult.Confirm, null));

            _context.Set(Dicing(0.4));
            Assert.IsNull(_modal.Pending);
        }

        [Test]
        public void 機械の画面が閉じる_または改善の行が無くなるとモーダルを閉じる()
        {
            _context.Set(Dicing(0.505));
            _bridge.Open();
            _context.Set(BlockContextViewModel.None);
            Assert.IsNull(_modal.Pending, "機械の画面を閉じると文脈が無しになる");

            _context.Set(Dicing(0.505));
            _bridge.Open();
            _context.Set(WithOdds(CleanRoomYieldOdds.LayerExposure(0.01, new List<CleanRoomOddsImprovement>())));
            Assert.IsNull(_modal.Pending, "改善の行が無い");
        }

        [Test]
        public void 別のモーダルに置き換わったら閉じたままで_それを取り消さない()
        {
            _context.Set(Dicing(0.505));
            _bridge.Open();
            var ownId = _modal.Pending.Id;
            LogAssert.Expect(LogType.Warning, $"[CleanRoomYieldImprovementModalBridge] modal request {ownId} was replaced by another modal; the improvement modal stays closed");
            _modal.RequestModal("other", "other", "OK", ModalVariant.Confirm).Forget();
            var otherId = _modal.Pending.Id;

            _context.Set(Dicing(0.4));
            _context.Set(BlockContextViewModel.None);
            Assert.AreEqual(otherId, _modal.Pending.Id);
        }

        [Test]
        public void 改善の行が無いときに開いても出さず警告だけを残す()
        {
            _context.Set(WithOdds(CleanRoomYieldOdds.LayerExposure(0.01, new List<CleanRoomOddsImprovement>())));
            LogAssert.Expect(LogType.Warning, "[CleanRoomYieldImprovementModalBridge] open ignored: the tracked machine has no improvement rows now");
            _bridge.Open();
            Assert.IsNull(_modal.Pending);
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
