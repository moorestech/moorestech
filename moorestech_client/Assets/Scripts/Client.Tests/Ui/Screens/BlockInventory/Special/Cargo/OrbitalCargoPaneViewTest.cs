using System.Collections.Generic;
using System.Linq;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Tests.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.Components.Slots;
using Client.Ui.Screens.BlockInventory.Special.Cargo;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Core.Master;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.BlockInventory.Special.Cargo.OrbitalCargoSamples;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.Cargo
{
    /// <summary>
    /// 積荷の一覧と選んだ後の見出しの View（ADR 0106 の 1・3・4）。一覧を出すかは引数で受け、押下はコマンドと Subject へ出す。モードの進み方は OrbitalCargoBodyPresenterTest が見る
    /// The cargo list and selected-headline view (ADR 0106 §1, §3, §4); the mode comes as an argument and presses go to the command and Subjects; OrbitalCargoBodyPresenterTest covers mode transitions
    /// </summary>
    public class OrbitalCargoPaneViewTest : SpecialScreenTestHost
    {
        private const string Prefix = "rocket-launch-pad";
        private static readonly UiText Status = UiText.FromVerbatim("status");

        [Test]
        public void 一覧を出すときは一覧だけを置き解放済みの行を運ぶ順で並べる()
        {
            var (pane, content) = Render(Pad(null, null, Cable, ChipLv1), true);
            CollectionAssert.AreEqual(new[] { $"{Prefix}-cargo-row-1", $"{Prefix}-cargo-row-3" }, RowIds(pane));
            Assert.IsNull(pane.Q<VisualElement>($"{Prefix}-cargo-header"), "見出しと選んだ後の中身は木に置かない");
            Assert.IsNull(pane.Q<VisualElement>(content.name));
            Assert.AreEqual("status", TextOf(pane, $"{Prefix}-cargo-status"));
        }

        [Test]
        public void 行の文は積むものと返るものの対でケーブルは成果の枠を持たずプールの増分を書く()
        {
            var (pane, _) = Render(Pad(null, null, ChipLv2, Cable), true);
            Assert.AreEqual(Resolved(OrbitalCargoNames.Row(ChipLv2Choice, LoadContent())), TextOf(pane, $"{Prefix}-cargo-row-2-name"));
            Assert.IsNotNull(pane.Q<VisualElement>($"{Prefix}-cargo-row-2-output-0"));
            StringAssert.Contains("+1000", TextOf(pane, $"{Prefix}-cargo-row-3-name"));
            Assert.IsNull(pane.Q<VisualElement>($"{Prefix}-cargo-row-3-output-0"));
        }

        [Test]
        public void プールへ足し出力も出す積荷の行は文に両方を書き成果の枠も並べる()
        {
            // サーバーはプールへの加算と出力を別々に処理するので、両方を持つ積荷もマスタに書ける（判断 21）
            // The server applies the pool and the outputs independently, so the master may give a kind both (decision 21)
            var both = new OrbitalCargoChoice(Cable, 1000, new[] { new ItemAmount(ComputeData, 7) }, true);
            var (pane, _) = Render(new OrbitalCargoDetail(null, null, new[] { both }, new[] { Cable }, 120), true);
            var text = TextOf(pane, $"{Prefix}-cargo-row-3-name");
            StringAssert.Contains("+1000", text);
            StringAssert.Contains(LoadContent().ItemName(ComputeData).Resolve(), text);
            StringAssert.Contains("×7", text);
            Assert.IsNotNull(pane.Q<VisualElement>($"{Prefix}-cargo-row-3-output-0"));
        }

        [Test]
        public void 行を押すと選ぶコマンドを送り押した種類を出すが自分ではモードを変えない()
        {
            var (pane, _) = Render(Pad(null, null, ChipLv1, ChipLv2), true);
            var pressed = new List<ItemId>();
            pane.OnRowPressed.Subscribe(pressed.Add);
            simulate.Click(Find<VisualElement>(pane, $"{Prefix}-cargo-row-2"));
            CollectionAssert.AreEqual(new[] { "orbital_cargo.select 2" }, Commands.Issued);
            CollectionAssert.AreEqual(new[] { ChipLv2 }, pressed);
            Assert.IsNotNull(pane.Q<VisualElement>($"{Prefix}-cargo-list"), "一覧を閉じるのは Presenter の描き直し");
        }

        [Test]
        public void 一覧を出さないときは見出しと中身を置き見出しを押すと一覧を求める()
        {
            var (pane, content) = Render(Pad(ChipLv2, null, ChipLv1, ChipLv2), false);
            Assert.IsNull(pane.Q<VisualElement>($"{Prefix}-cargo-list"));
            Assert.AreEqual(Resolved(OrbitalCargoNames.Row(ChipLv2Choice, LoadContent())), TextOf(pane, $"{Prefix}-cargo-header-name"));
            Assert.IsNotNull(pane.Q<VisualElement>(content.name));
            Assert.IsFalse(IsShown(Find<VisualElement>(pane, $"{Prefix}-cargo-next-flight")));

            var requested = 0;
            pane.OnChangeCargoRequested.Subscribe(_ => requested++);
            simulate.Click(Find<VisualElement>(pane, $"{Prefix}-cargo-header"));
            Assert.AreEqual(1, requested);
            CollectionAssert.IsEmpty(Commands.Issued, "見出しの押下はコマンドを送らない");
        }

        [Test]
        public void 選んだ後に一覧を出すと今の種類の行に印が付く()
        {
            var (pane, _) = Render(Pad(ChipLv1, null, ChipLv1, ChipLv2), true);
            Assert.IsTrue(Find<VisualElement>(pane, $"{Prefix}-cargo-row-1").ClassListContains(OrbitalCargoSelectionRowView.SelectedClassName));
            Assert.IsFalse(Find<VisualElement>(pane, $"{Prefix}-cargo-row-2").ClassListContains(OrbitalCargoSelectionRowView.SelectedClassName));
        }

        [Test]
        public void 便の途中で選び直すと見出しは今の便で下に次の便から一行を出す()
        {
            var (pane, _) = Render(Pad(ChipLv2, ChipLv1, ChipLv1, ChipLv2), false);
            Assert.AreEqual(Resolved(OrbitalCargoNames.Row(ChipLv1Choice, LoadContent())), TextOf(pane, $"{Prefix}-cargo-header-name"));
            Assert.IsTrue(IsShown(Find<VisualElement>(pane, $"{Prefix}-cargo-next-flight")));
            Assert.AreEqual(Resolved(OrbitalCargoNames.NextFlight(ChipLv2Choice, LoadContent())), TextOf(pane, $"{Prefix}-cargo-next-flight"));
        }

        [Test]
        public void 解放済みが無くても一覧を出すときは空の一覧を置く()
        {
            var (pane, _) = Render(Pad(null, null), true);
            Assert.IsNotNull(pane.Q<VisualElement>($"{Prefix}-cargo-list"));
            CollectionAssert.IsEmpty(RowIds(pane));
        }

        [Test]
        public void 詳細待ちから一覧と選択済みへ切り替えるとそのモードの中身だけを出す()
        {
            var content = new VisualElement { name = "selected-content" };
            var pane = Mount(new OrbitalCargoPane(Prefix, Kit(LoadContent()), content, true));
            pane.RenderWithoutCargo();
            Assert.IsFalse(IsShown(Find<VisualElement>(pane, $"{Prefix}-cargo-header")));
            Assert.IsNotNull(pane.Q<VisualElement>(content.name));

            pane.Render(Pad(null, null, ChipLv1), Status, true);
            Assert.IsNotNull(pane.Q<VisualElement>($"{Prefix}-cargo-list"));
            Assert.IsNull(pane.Q<VisualElement>($"{Prefix}-cargo-scroll"));
            pane.Render(Pad(ChipLv1, null, ChipLv1), Status, false);
            Assert.IsNull(pane.Q<VisualElement>($"{Prefix}-cargo-list"));
            Assert.IsNotNull(pane.Q<VisualElement>($"{Prefix}-cargo-scroll"));
            Assert.IsTrue(IsShown(Find<VisualElement>(pane, $"{Prefix}-cargo-header")));
        }

        [Test]
        public void 同じ枠が空の透かしから所持と必要の数に切り替わる()
        {
            const string gridId = "cargo-input";
            var content = LoadContent();
            var grid = Mount(Kit(content).Grids.Create(gridId));
            var bounds = new[] { BoundItemSlot.WithGhost(0, new MachineItemGhost(ChipLv1, 20, new[] { ChipLv1 })) };
            OrbitalCargoSlotContents.Render(grid, gridId, new SlotItem[1], bounds, content);
            simulate.FrameUpdate();

            var frame = Find<SlotFrame>(grid, $"{gridId}-slot-0");
            Assert.IsTrue(frame.ClassListContains("slot-frame--ghost"));
            OrbitalCargoSlotContents.Render(grid, gridId, new[] { new SlotItem(ChipLv1, 7) }, bounds, content);
            simulate.FrameUpdate();

            Assert.AreSame(frame, Find<SlotFrame>(grid, $"{gridId}-slot-0"));
            Assert.IsFalse(frame.ClassListContains("slot-frame--ghost"));
            Assert.IsTrue(frame.parent.ClassListContains("item-slot--shortage"));
            Assert.AreEqual("7/20", frame.parent.Q<Label>(null, "item-slot__shortage", "outlined-count-label--glyph").text);
        }

        private (OrbitalCargoPane pane, VisualElement content) Render(OrbitalCargoDetail cargo, bool showsSelection)
        {
            var content = new VisualElement { name = "selected-content" };
            var pane = Mount(new OrbitalCargoPane(Prefix, Kit(LoadContent()), content, false));
            pane.Render(cargo, Status, showsSelection);
            simulate.FrameUpdate();
            return (pane, content);
        }

        private static string[] RowIds(VisualElement root)
        {
            return root.Query<OrbitalCargoSelectionRowView>().ToList().Select(row => row.name).ToArray();
        }
    }
}
