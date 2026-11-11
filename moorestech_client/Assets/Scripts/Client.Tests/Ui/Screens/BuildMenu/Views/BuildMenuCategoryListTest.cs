using System;
using System.Linq;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Ui.Screens.BuildMenu.Parts;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BuildMenu.Views
{
    /// <summary>
    /// カテゴリ群の見出し・群・末尾スペーサ（Web BuildMenuCategoryList.test.ts の写し）
    /// Category headings, groups and the trailing spacer (port of the Web BuildMenuCategoryList.test.ts)
    /// </summary>
    public class BuildMenuCategoryListTest : ScreenTestHost
    {
        /// <web-test file="src/features/buildMenu/views/BuildMenuCategoryList.test.ts">カテゴリ群ごとに大見出しを置き、全群のsectionと各見出しのh2をrefへ登録し、末尾スペーサ高を反映する</web-test>
        [Test]
        public void カテゴリ群ごとに大見出しを置き見出しと群を引けて末尾スペーサ高を反映する()
        {
            var viewModel = BuildMenuSamples.Create();
            var shown = BuildMenuShownListComposer.Compose(viewModel, "");
            var list = Mount(new BuildMenuCategoryList(null, BuildMenuSamples.Icons(null), new RecordingBuildMenuEntryHandler()));

            list.Render(shown.Groups, viewModel);
            list.SetSpacerHeight(123f);
            simulate.FrameUpdate();

            // 見出しは群の順に並び、testid は Web と同じ
            // Headings follow the group order with the Web testids
            var headings = list.Query<VisualElement>(className: "build-menu__category-heading").ToList();
            CollectionAssert.AreEqual(shown.ShownCategoryGuids.Select(g => $"build-menu-category-heading-{g:D}"), headings.Select(h => h.name));
            Assert.IsTrue(list.TryGetHeading(BuildMenuSamples.Logistics, out var heading));
            Assert.AreSame(headings[0], heading);
            Assert.IsTrue(list.TryGetGroup(BuildMenuSamples.Transport, out var group));
            Assert.AreEqual($"build-menu-category-{BuildMenuSamples.Transport:D}-group", group.name);
            Assert.AreEqual(123f, list.Spacer.resolvedStyle.minHeight.value, 0.01f);
            Assert.AreEqual("build-menu-trailing-spacer", list.Spacer.name);
        }

        /// <web-test file="src/features/buildMenu/views/BuildMenuCategoryList.test.ts">groupsが空でもクラッシュせずスペーサのみ出す</web-test>
        [Test]
        public void 群が空でもスペーサだけを出す()
        {
            var list = Mount(new BuildMenuCategoryList(null, BuildMenuSamples.Icons(null), new RecordingBuildMenuEntryHandler()));

            list.Render(Array.Empty<BuildMenuCategoryGroup>(), BuildMenuViewModel.Empty);
            list.SetSpacerHeight(42f);
            simulate.FrameUpdate();

            Assert.AreEqual(0, list.Query<VisualElement>(className: "build-menu__category-heading").ToList().Count);
            Assert.IsFalse(list.TryGetHeading(BuildMenuSamples.Logistics, out _));
            Assert.AreEqual(42f, list.Spacer.resolvedStyle.minHeight.value, 0.01f);
        }
    }
}
