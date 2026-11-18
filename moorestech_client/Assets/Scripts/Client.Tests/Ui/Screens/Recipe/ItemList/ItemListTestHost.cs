using Client.Tests.Ui.Content;
using Client.Ui.Content;
using Client.Ui.Screens.Recipe;
using Client.Ui.State.Recipe.Screen;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Recipe.ItemList
{
    /// <summary>
    /// アイテム一覧 L1 の土台（テスト用 mod のマスタ・選択・一覧の VM・押下の操作）
    /// Base for item-list L1 tests (test-mod master, selection, list VMs, press operations)
    /// </summary>
    public abstract class ItemListTestHost : ScreenTestHost
    {
        // テスト用 mod のアイテムは 107 件なので、ID はそれ以下で組む
        // The test mod has 107 items, so IDs stay within that
        protected const int MaxItemId = 107;

        protected GameContentPresentation ContentPresentation { get; private set; }
        protected RecipeItemSelection Selection { get; private set; }

        [SetUp]
        public void SetUpItemList()
        {
            // 名前は ID からマスタで引くのでテスト用 mod を読む。絵は無くても ID の字で代わりに出る
            // Names resolve through the master, so load the test mod; without a texture the ID text stands in
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ContentPresentation = new GameContentPresentation(new FakeContentTextureSource(null, null, null));
            Selection = new RecipeItemSelection();
        }

        // ID 1..count の、作れる数 0 のセル列
        // Cells for IDs 1..count, all with zero craftable
        protected static ItemListViewModel ListOf(int count)
        {
            var cells = new ItemListCell[count];
            for (var i = 0; i < count; i++) cells[i] = new ItemListCell(new ItemId(i + 1), 0);
            return new ItemListViewModel(cells);
        }

        protected ItemListView MountList(ItemListViewModel vm)
        {
            // 描いてからレイアウトし、バーの出し入れの 2 回目も流す
            // Render, lay out, and flush the second layout pass from showing or hiding the bar
            var view = Mount(new ItemListView(ContentPresentation, Selection));
            view.Render(vm);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            return view;
        }

        // 1 回の移動で離す（閾値未満ならタップ、超えればドラッグ）
        // Release after a single move (a tap under the threshold, a drag past it)
        protected void PressMoveRelease(Vector2 from, Vector2 to)
        {
            simulate.MouseDown(from, MouseButton.LeftMouse);
            simulate.MouseMove(to);
            simulate.MouseUp(to, MouseButton.LeftMouse);
            simulate.FrameUpdate();
        }
    }
}
