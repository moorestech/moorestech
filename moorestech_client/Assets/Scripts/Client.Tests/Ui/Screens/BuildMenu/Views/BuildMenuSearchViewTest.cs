using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.BuildMenu.Support.BuildMenuSamples;

namespace Client.Tests.Ui.Screens.BuildMenu.Views
{
    public class BuildMenuSearchViewTest : BuildMenuScreenTestHost
    {
        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">検索は同じリストを絞り込み、ヒットの無いカテゴリだけサイドバーで無効になる</web-test>
        [Test]
        public void 検索は同じリストを絞り込みヒットの無いカテゴリだけサイドバーで無効になる()
        {
            OpenMenu(Create());

            Search("鉄");

            Assert.IsNotNull(View.Q($"build-menu-category-heading-{Logistics:D}"));
            Assert.IsNotNull(View.Q($"build-menu-section-{Transport:D}-{Sub:D}"));
            Assert.IsNull(View.Q($"build-menu-category-heading-{Blueprint:D}"));

            // 小見出しはサブカテゴリ名だけ（Web の複合見出しの廃止）
            // The section heading is the sub-category name alone (the Web dropped composite headings)
            var sectionHeading = Find<VisualElement>(View, $"build-menu-section-{Logistics:D}-{Sub:D}").Q<TextElement>(className: "build-menu__section-heading");
            StringAssert.DoesNotContain("/", sectionHeading.text);
            Assert.IsTrue(CategoryButton(Transport).enabledSelf);
            Assert.IsFalse(CategoryButton(Blueprint).enabledSelf);

            Search("");
            Assert.IsTrue(CategoryButton(Blueprint).enabledSelf);
        }

        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">検索0件は該当なし表示</web-test>
        [Test]
        public void 検索0件は該当なしをリストと入れ替えて出す()
        {
            OpenMenu(Create());

            Search("存在しないブロック");

            var noResults = Find<TextElement>(View, "build-menu-no-results");
            Assert.IsTrue(IsShown(noResults));
            StringAssert.Contains(new UiText(LocalizationKeys.Ui.BuildMenu.NoResults).Resolve(), noResults.text);
            Assert.IsFalse(IsShown(Find<VisualElement>(View, "build-menu-sections")));
        }

        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">検索文字列も閉じて開き直すと復元される</web-test>
        [Test]
        public void 検索語は閉じて開き直しても戻る()
        {
            OpenMenu(Create());
            Search("鉄");
            CloseMenu();

            OpenMenu(Create());

            Assert.AreEqual("鉄", Find<TextField>(View, "build-menu-search").value);
            Assert.IsFalse(CategoryButton(Blueprint).enabledSelf);
        }

        [Test]
        public void 検索欄を空にしても検索語は空文字で保存される()
        {
            OpenMenu(Create());
            Search("鉄");

            Search("");

            // セッションへは非 null の文字列で渡す（L-5）
            // The session receives a non-null string (L-5)
            Assert.AreEqual("", Session.Current.Query);
        }
    }
}
