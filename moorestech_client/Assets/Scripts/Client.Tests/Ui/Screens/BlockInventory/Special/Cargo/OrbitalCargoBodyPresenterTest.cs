using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Screens.BlockInventory.Special.Cargo;
using Client.Ui.Screens.BlockInventory.Special.Space;
using Client.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using Core.Master;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.BlockInventory.Special.Cargo.OrbitalCargoSamples;
using S = Mooresmaster.Model.BlocksModule.BlockMasterElement.BlockScreenConst;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.Cargo
{
    /// <summary>
    /// 積荷の本文の Presenter が一覧の局所状態を持ち、本文の押下で進めて描かせる（前例 MachineSectionPresenter。ADR 0106 の 1・3・4）。発射台の本文で組む
    /// The cargo body presenter owns the list's local state, advances it on the body's presses and has it draw (precedent MachineSectionPresenter; ADR 0106 §1, §3, §4), built with the launch pad body
    /// </summary>
    public class OrbitalCargoBodyPresenterTest : SpecialScreenTestHost
    {
        [Test]
        public void 未選択の発射台を開くと一覧だけを出し資材と積荷の枠を出さない()
        {
            var (view, presenter) = Attach(Pad(null, null, ChipLv1));
            Assert.IsTrue(presenter.IsApplicable.Value);
            Assert.IsNotNull(view.Q<VisualElement>("rocket-launch-pad-cargo-row-1"));
            Assert.IsNull(view.Q<VisualElement>("rocket-launch-pad-material-slots"));
            Assert.IsNull(view.Q<VisualElement>("rocket-launch-pad-payload-slots"));
            Assert.IsNull(view.Q<VisualElement>("rocket-launch-pad-output-slots"), "出力の枠も選ぶまで出さない（ユーザー裁定 2026-11-21・判断 23）");
        }

        [Test]
        public void 行を押すと送り届くまで一覧のまま届けば見出しと中身を出す()
        {
            var (view, _) = Attach(Pad(null, null, ChipLv1, ChipLv2));
            simulate.Click(Find<VisualElement>(view, "rocket-launch-pad-cargo-row-2"));
            CollectionAssert.AreEqual(new[] { "orbital_cargo.select 2" }, Commands.Issued);
            Assert.IsNotNull(view.Q<VisualElement>("rocket-launch-pad-cargo-list"), "届くまでは一覧のまま（拒否されても一覧に残る）");

            Model.Set(Detail(Pad(ChipLv2, null, ChipLv1, ChipLv2)));
            Assert.IsNull(view.Q<VisualElement>("rocket-launch-pad-cargo-list"));
            Assert.IsNotNull(view.Q<VisualElement>("rocket-launch-pad-cargo-header"));
            Assert.IsNotNull(view.Q<VisualElement>("rocket-launch-pad-material-slots"));
        }

        [Test]
        public void 見出しを押すと一覧へ戻り今の種類の行を押せばその場で閉じる()
        {
            var (view, _) = Attach(Pad(ChipLv1, null, ChipLv1, ChipLv2));
            Assert.IsNull(view.Q<VisualElement>("rocket-launch-pad-cargo-list"), "選んだ後は見出しと中身");
            simulate.Click(Find<VisualElement>(view, "rocket-launch-pad-cargo-header"));
            simulate.FrameUpdate();
            Assert.IsTrue(Find<VisualElement>(view, "rocket-launch-pad-cargo-row-1").ClassListContains(OrbitalCargoSelectionRowView.SelectedClassName));
            CollectionAssert.IsEmpty(Commands.Issued, "見出しの押下はコマンドを送らない");

            // 同じ種類はサーバーの状態が変わらず更新が来ないので、押した時の描き直しで閉じる（判断 15）
            // The same kind leaves the server state unchanged and no update comes, so the redraw on press closes it (decision 15)
            simulate.Click(Find<VisualElement>(view, "rocket-launch-pad-cargo-row-1"));
            CollectionAssert.AreEqual(new[] { "orbital_cargo.select 1" }, Commands.Issued);
            Assert.IsNull(view.Q<VisualElement>("rocket-launch-pad-cargo-list"));
        }

        [Test]
        public void Detachで開いた一覧を戻し次のAttachでは選んだ後の画面から始める()
        {
            var (view, presenter) = Attach(Pad(ChipLv1, null, ChipLv1, ChipLv2));
            simulate.Click(Find<VisualElement>(view, "rocket-launch-pad-cargo-header"));
            Assert.IsNotNull(view.Q<VisualElement>("rocket-launch-pad-cargo-list"));
            presenter.Detach();
            Assert.IsFalse(presenter.IsApplicable.Value);
            presenter.Attach(OpenPanel(BlockMasterElement.BlockTypeConst.RocketLaunchPad, S.RocketLaunchPad, Slots()));
            Assert.IsNull(view.Q<VisualElement>("rocket-launch-pad-cargo-list"), "前のパネルの開いた一覧を持ち越さない");
        }

        [Test]
        public void 詳細の未着は本文を出したまま取得待ちを語り一覧を出さない()
        {
            var view = Mount(new RocketLaunchPadBodyView(Kit(LoadContent()), false));
            var presenter = Presenters.OrbitalCargoBody(view);
            presenter.Attach(OpenPanel(BlockMasterElement.BlockTypeConst.RocketLaunchPad, S.RocketLaunchPad, Slots()));
            Assert.IsTrue(presenter.IsApplicable.Value, "本文は詳細の未着でも出す（前例 WholeSpecialDetailSelector）");
            Assert.IsTrue(IsShown(Find<Label>(view, "rocket-launch-pad-loading")));
            Assert.IsNull(view.Q<VisualElement>("rocket-launch-pad-cargo-list"));
        }

        private (RocketLaunchPadBodyView view, OrbitalCargoBodyPresenter presenter) Attach(OrbitalCargoDetail cargo)
        {
            var view = Mount(new RocketLaunchPadBodyView(Kit(LoadContent()), false));
            var presenter = Presenters.OrbitalCargoBody(view);
            Model.Set(Detail(cargo));
            presenter.Attach(OpenPanel(BlockMasterElement.BlockTypeConst.RocketLaunchPad, S.RocketLaunchPad, Slots()));
            simulate.FrameUpdate();
            return (view, presenter);
        }

        private static SpecialBlockDetailViewModel Detail(OrbitalCargoDetail cargo)
        {
            return SpecialDetailSamples.Only(SpecialDetailSamples.RocketWith(RocketLaunchPadPhase.Idle, cargo));
        }

        // 見本の発射台（RocketWith）は資材 0・積荷 1・出力 2 から
        // The sample pad (RocketWith) has material 0, payload 1 and outputs from 2
        private static SlotItem[] Slots()
        {
            return new[] { new SlotItem(ItemMaster.EmptyItemId, 0), new SlotItem(ItemMaster.EmptyItemId, 0), new SlotItem(ItemMaster.EmptyItemId, 0) };
        }
    }
}
