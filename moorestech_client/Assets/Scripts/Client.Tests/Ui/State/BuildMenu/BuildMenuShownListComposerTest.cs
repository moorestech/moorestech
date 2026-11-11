using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.PlacementTarget;
using Client.Ui.State.Text;
using Core.Master;
using Game.PlacementTarget;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BuildMenu
{
    public class BuildMenuShownListComposerTest
    {
        private static readonly Guid ComposerLogistics = Guid.Parse("10000000-0000-4000-8000-000000000001");
        private static readonly Guid ComposerTransport = Guid.Parse("10000000-0000-4000-8000-000000000002");
        private static readonly Guid ComposerEmpty = Guid.Parse("10000000-0000-4000-8000-000000000003");
        private static readonly Guid ComposerSub = Guid.Parse("20000000-0000-4000-8000-000000000001");

        [Test]
        public void 空の検索語では全群を出しサイドバーは全部有効()
        {
            var shown = BuildMenuShownListComposer.Compose(CreateViewModel(), "");

            CollectionAssert.AreEqual(new[] { ComposerLogistics, ComposerTransport }, shown.ShownCategoryGuids);
            CollectionAssert.AreEqual(new[] { ComposerLogistics, ComposerTransport }, shown.SidebarCategories);
            Assert.IsTrue(shown.IsCategoryEnabled(ComposerTransport));
            Assert.IsFalse(shown.ShowsNoResults);
        }

        [Test]
        public void 検索は同じリストを絞りヒットの無いカテゴリだけ無効にする()
        {
            var shown = BuildMenuShownListComposer.Compose(CreateViewModel(), "chest");

            CollectionAssert.AreEqual(new[] { ComposerLogistics }, shown.ShownCategoryGuids);
            CollectionAssert.AreEqual(new[] { ComposerLogistics, ComposerTransport }, shown.SidebarCategories);
            Assert.IsTrue(shown.IsCategoryEnabled(ComposerLogistics));
            Assert.IsFalse(shown.IsCategoryEnabled(ComposerTransport));
        }

        [Test]
        public void 検索0件は該当なしを出す()
        {
            var shown = BuildMenuShownListComposer.Compose(CreateViewModel(), "存在しないブロック");

            Assert.AreEqual(0, shown.Groups.Count);
            Assert.IsTrue(shown.ShowsNoResults);
        }

        [Test]
        public void エントリの無いカテゴリはサイドバーに出ない()
        {
            var shown = BuildMenuShownListComposer.Compose(CreateViewModel(), "");

            CollectionAssert.DoesNotContain(shown.SidebarCategories, ComposerEmpty);
        }

        private static BuildMenuViewModel CreateViewModel()
        {
            var categories = new[] { ComposerLogistics, ComposerTransport, ComposerEmpty }.Select(guid => new BuildMenuCategory(guid, new[] { ComposerSub })).ToList();
            var entries = new List<BuildMenuEntryViewModel>
            {
                Entry(ComposerLogistics, "Wood Chest"),
                Entry(ComposerTransport, "Rail"),
            };
            var names = new[] { ComposerLogistics, ComposerTransport, ComposerEmpty, ComposerSub }.ToDictionary(guid => guid, guid => UiText.FromVerbatim(guid.ToString()));
            return new BuildMenuViewModel(categories, names, entries);
        }

        private static BuildMenuEntryViewModel Entry(Guid category, string label)
        {
            return new BuildMenuEntryViewModel(Guid.NewGuid(), PlacementTargetKind.Block, category, ComposerSub, label, Array.Empty<BuildMenuMaterialRow>(), false, new PlacementTargetBlockIcon(new BlockId(1)), BuildMenuSetPlacementNone.Instance);
        }
    }
}
