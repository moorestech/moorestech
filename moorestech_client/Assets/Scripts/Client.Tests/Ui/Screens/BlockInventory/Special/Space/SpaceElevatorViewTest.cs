using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Tests.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Special.Space;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Text;
using Game.Block.Interface.State;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.Space
{
    /// <summary>
    /// 宇宙エレベーターの本文と状態区画（Web SpaceElevatorInventory / SpaceElevatorSection の vitest）
    /// Space elevator body and state section (Web SpaceElevatorInventory / SpaceElevatorSection vitests)
    /// </summary>
    public class SpaceElevatorViewTest : SpaceElevatorTestHost
    {
        /// <web-test file="src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorInventory.test.tsx">空スロット=%sでも入口1枠と全出口を識別し元データを保持する</web-test>
        [TestCase(false)]
        [TestCase(true)]
        public void 空きでも入口1枠と全出口を元の添字で出す(bool empty)
        {
            var view = Render(Slots(empty), Unconnected(SpaceElevatorBaseStatus.ReadyToConnect));
            Assert.AreEqual(new[] { "space-elevator-input-grid-slot-0" }, FrameIds(view, "space-elevator-input-grid"));
            Assert.AreEqual(new[] { "space-elevator-output-grid-slot-1", "space-elevator-output-grid-slot-2" }, FrameIds(view, "space-elevator-output-grid"));
        }

        /// <web-test file="src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorSection.test.tsx">ケーブル不足なら接続できず不足の理由を示す</web-test>
        [Test]
        public void ケーブル不足は接続できず不足の色で理由を出す()
        {
            var view = Render(Slots(true), SpecialDetailSamples.SpaceElevator(SpaceElevatorBaseStatus.InsufficientCable, false, 2000, 0f));
            Assert.IsFalse(Find<Button>(view, "space-elevator-connect").enabledSelf);
            Assert.IsTrue(Find<Label>(view, "space-elevator-status").ClassListContains(LackHighlightText.InsufficientClassName));
        }

        /// <web-test file="src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorSection.test.tsx">接続可能なら空payloadのactionを送る</web-test>
        [Test]
        public void 接続可能なら接続を送る()
        {
            var view = Render(Slots(true), Unconnected(SpaceElevatorBaseStatus.ReadyToConnect));
            simulate.Click(Find<Button>(view, "space-elevator-connect"));
            CollectionAssert.AreEqual(new[] { "space_elevator.connect" }, Commands.Issued);
        }

        /// <web-test file="src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorSection.test.tsx">接続済みなら進捗を描き接続ボタンを隠す</web-test>
        [Test]
        public void 接続済みは進捗を描き接続ボタンを隠す()
        {
            var view = Render(Slots(true), SpecialDetailSamples.SpaceElevator(SpaceElevatorBaseStatus.Climbing, true, 1000, 0.5f));
            Assert.AreEqual(0.5f, GaugeValue(view, "space-elevator-progress"), 0.001f);
            Assert.IsFalse(IsShown(Find<Button>(view, "space-elevator-connect")));
        }

        /// <web-test file="src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorSection.test.tsx">%s の文言と不足色を表示する</web-test>
        [TestCase(SpaceElevatorBaseStatus.ReadyToConnect, false)]
        [TestCase(SpaceElevatorBaseStatus.CargoNotSelected, false)]
        [TestCase(SpaceElevatorBaseStatus.InsufficientCable, true)]
        [TestCase(SpaceElevatorBaseStatus.WaitingSatellite, false)]
        [TestCase(SpaceElevatorBaseStatus.Climbing, false)]
        [TestCase(SpaceElevatorBaseStatus.LowPower, true)]
        [TestCase(SpaceElevatorBaseStatus.InletBlocked, true)]
        [TestCase(SpaceElevatorBaseStatus.OutputBlocked, true)]
        public void 状態ごとの文言と不足色を出す(SpaceElevatorBaseStatus status, bool insufficient)
        {
            Assert.AreEqual(insufficient, SpaceElevatorLogic.StatusDisplay(status).Insufficient);
            var view = Render(Slots(true), Unconnected(status));
            Assert.AreEqual(new UiText(SpaceElevatorLogic.StatusDisplay(status).LabelKey).Resolve(), TextOf(view, "space-elevator-status"));
            Assert.AreEqual(insufficient, Find<Label>(view, "space-elevator-status").ClassListContains(LackHighlightText.InsufficientClassName));
        }

        /// <web-test file="src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorSection.test.tsx">詳細未着は取得待ちを表示し接続操作を作らない</web-test>
        [Test]
        public void 詳細の未着は取得待ちを出し接続ボタンを出さない()
        {
            var view = Mount(new SpaceElevatorBodyView(Kit(null), false));
            view.Render(Slots(true), SpecialBlockDetailViewModel.Empty, false);
            Assert.AreEqual(new UiText(LocalizationKeys.Ui.BlockInventory.DetailLoading).Resolve(), TextOf(view, "space-elevator-loading"));
            Assert.IsFalse(IsShown(Find<VisualElement>(view, "space-elevator-section")));
            Assert.IsFalse(IsDisplayedInHierarchy(Find<Button>(view, "space-elevator-connect")), "接続ボタンは隠れた区画の中にあり見えない");

            #region Internal

            // 自分から根まで display:none が無いか（隠れた区画の中の子は見えない）
            // Whether nothing from itself to the root is display:none (children of a hidden section are not visible)
            bool IsDisplayedInHierarchy(VisualElement element)
            {
                if (element.panel == null) return false;
                for (var current = element; current != null; current = current.parent)
                {
                    if (current.resolvedStyle.display == DisplayStyle.None) return false;
                }
                return true;
            }

            #endregion
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 未選択の拠点は接続の有無にかかわらず一覧を出し接続ボタンを出さない(bool connected)
        {
            var unselected = new OrbitalCargoDetail(null, null, new[] { OrbitalCargoSamples.ChipLv1Choice, OrbitalCargoSamples.ChipLv2Choice }, new[] { OrbitalCargoSamples.ChipLv1 }, 240);
            var view = Attach(Slots(true), SpecialDetailSamples.SpaceElevatorWith(SpaceElevatorBaseStatus.CargoNotSelected, connected, 1000, 0f, unselected));
            Assert.IsNotNull(view.Q<VisualElement>("space-elevator-cargo-row-1"));
            Assert.IsNull(view.Q<VisualElement>("space-elevator-cargo-row-2"), "未解放の Lv2 は出さない");
            Assert.IsNull(view.Q<VisualElement>("space-elevator-connect"));
            Assert.IsNull(view.Q<VisualElement>("space-elevator-output-grid"), "出口の枠も選ぶまで出さない（ユーザー裁定 2026-11-21・判断 23）");
            Assert.IsNull(view.Q<VisualElement>("space-elevator-pool"), "接続済みでもケーブルプールの行は選ぶまで出さない（同上）");
            Assert.AreEqual(new UiText(SpaceElevatorLogic.StatusDisplay(SpaceElevatorBaseStatus.CargoNotSelected).LabelKey).Resolve(), TextOf(view, "space-elevator-cargo-status"));
        }

        [Test]
        public void 昇降中に選び直すと見出しは昇っている便で下に次の便からを出し入口は新しい種類の透かしになる()
        {
            var cargo = new OrbitalCargoDetail(OrbitalCargoSamples.ChipLv2, OrbitalCargoSamples.ChipLv1, new[] { OrbitalCargoSamples.ChipLv1Choice, OrbitalCargoSamples.ChipLv2Choice }, new[] { OrbitalCargoSamples.ChipLv1, OrbitalCargoSamples.ChipLv2 }, 240);
            var view = Render(Slots(true), SpecialDetailSamples.SpaceElevatorWith(SpaceElevatorBaseStatus.Climbing, true, 0, 0.5f, cargo));
            Assert.IsTrue(IsShown(Find<VisualElement>(view, "space-elevator-cargo-next-flight")));
            Assert.AreEqual(new[] { "space-elevator-input-grid-slot-0" }, FrameIds(view, "space-elevator-input-grid"));
            Assert.IsTrue(IsShown(Find<VisualElement>(view, "space-elevator-pool")), "接続済みでもケーブルのプールの行は今のまま");
        }
    }
}
