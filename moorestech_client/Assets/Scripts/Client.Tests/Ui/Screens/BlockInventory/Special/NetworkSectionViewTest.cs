using System;
using System.Collections.Generic;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Screens.BlockInventory.Special.Network;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Text;
using Client.Ui.State.Items;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 電力網・歯車網の区画（Web NetworkSections）と、Presenter が Attach から Detach まで詳細とパネルの枠を購読し出し入れを知らせること
    /// Electric and gear network sections (Web NetworkSections) and the presenter subscribing to details and panel slots from Attach to Detach while reporting visibility
    /// </summary>
    public class NetworkSectionViewTest : SpecialScreenTestHost
    {
        [Test]
        public void 需要0は需要なしを出し他は集約の1行を出す()
        {
            var view = Mount(new ElectricNetworkSectionView());
            view.Render(NoSlots, SpecialDetailSamples.Only(new ElectricNetworkDetail(240, 0, 0, 1)));
            Assert.IsTrue(IsShown(Find<Label>(view, "electric-network-no-demand")));
            Assert.IsFalse(IsShown(Find<VisualElement>(view, "electric-network-summary")));

            view.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.PoleNetwork));
            Assert.IsFalse(IsShown(Find<Label>(view, "electric-network-no-demand")));
            Assert.AreEqual(Resolved(SpecialDetailTexts.ElectricNetworkSummary(SpecialDetailSamples.PoleNetwork)), TextOf(view, "electric-network-summary"));
        }

        [Test]
        public void 歯車網は集約と止まっていれば停止理由を出す()
        {
            var view = Mount(new GearNetworkSectionView());
            view.Render(NoSlots, SpecialDetailSamples.Only(new GearNetworkDetail(20, 30, GearNetworkStopReasonKind.OverRequirePower)));
            Assert.IsTrue(IsShown(Find<Label>(view, "gear-stop-reason")));
            Assert.IsTrue(Find<Label>(view, "gear-stop-reason").ClassListContains("text--insufficient"));

            view.Render(NoSlots, SpecialDetailSamples.Only(new GearNetworkDetail(20, 10, GearNetworkStopReasonKind.None)));
            Assert.IsFalse(IsShown(Find<Label>(view, "gear-stop-reason")));
        }

        [Test]
        public void Presenterは組み直しの外で届いた詳細で出し入れを知らせ自分のdisplayを触らずDetachの後は描かない()
        {
            var view = new ElectricNetworkSectionView();
            var presenter = Presenters.Of(view);
            Mount(presenter.Root);
            presenter.Attach(OpenPanel("ElectricPole"));
            Assert.IsFalse(presenter.IsApplicable.Value);

            Model.Set(SpecialDetailSamples.Only(SpecialDetailSamples.PoleNetwork));
            Assert.IsTrue(presenter.IsApplicable.Value, "網の集約が後から届いても出す側へ変わる");
            Assert.AreEqual(Resolved(SpecialDetailTexts.ElectricNetworkSummary(SpecialDetailSamples.PoleNetwork)), TextOf(view, "electric-network-summary"));

            Model.Set(SpecialBlockDetailViewModel.Empty);
            Assert.IsFalse(presenter.IsApplicable.Value, "集約が消えたら隠す側へ変わる");
            Assert.AreEqual(StyleKeyword.Null, view.style.display.keyword, "区画は自分の display を触らない（出し入れは親の SetItemShown）");

            presenter.Detach();
            Model.Set(SpecialDetailSamples.Only(SpecialDetailSamples.PoleNetwork));
            Assert.IsFalse(presenter.IsApplicable.Value, "Detach の後は購読しない");
        }

        [Test]
        public void Presenterは同じパネルの枠の変化を流し別のパネルの値は描かない()
        {
            var view = new RecordingRenderer();
            var presenter = Presenters.Of(view);
            Model.Set(SpecialDetailSamples.Only(SpecialDetailSamples.PoleNetwork));
            presenter.Attach(OpenPanel("ElectricPole"));
            Panels.State.Value = BlockInventoryTestPanels.OfType("ElectricPole", new SlotItem(new ItemId(1), 3));
            Assert.AreEqual(3, view.LastSlots[0].Count, "同じパネルの枠の更新が届く");

            Panels.State.Value = OtherPanel(new SlotItem(new ItemId(1), 7));
            Assert.AreEqual(3, view.LastSlots[0].Count, "別のパネルへ替わる途中の値は描かない");
        }

        // 別の位置・個体の電柱（マスタを読まずに組むため internal ctor を直に呼ぶ。前例 BlockInventoryTestPanels.OfType）
        // A pole at another position and instance, built via the internal ctor without the master (precedent BlockInventoryTestPanels.OfType)
        private static OpenBlockInventoryPanel OtherPanel(params SlotItem[] slots)
        {
            return new OpenBlockInventoryPanel("ElectricPole", Guid.Empty, default, new BlockInstanceId(2), new Vector3Int(2, 0, 0), slots, BlockInventoryPanelError.None);
        }

        // 渡された枠を覚えるだけの View
        // A view that only remembers the slots it was given
        private sealed class RecordingRenderer : VisualElement, ISpecialDetailRenderer
        {
            public IReadOnlyList<SlotItem> LastSlots { get; private set; }
            public bool HasDetail(SpecialBlockDetailViewModel detail) => true;
            public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail) => LastSlots = itemSlots;
        }
    }
}
