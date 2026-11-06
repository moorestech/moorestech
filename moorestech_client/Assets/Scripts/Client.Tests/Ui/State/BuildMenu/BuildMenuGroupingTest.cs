using System;
using System.Linq;
using Client.Ui.State.BuildMenu.Logic;
using NUnit.Framework;
using static Client.Tests.Ui.State.BuildMenu.BuildMenuTestEntries;

namespace Client.Tests.Ui.State.BuildMenu
{
    public class BuildMenuGroupingTest
    {
        /// <web-test file="src/features/buildMenu/logic/buildMenuGrouping.test.ts">エントリの無いカテゴリを除外し定義順のカテゴリ群を返す</web-test>
        [Test]
        public void エントリの無いカテゴリを除き定義順のカテゴリ群を返す()
        {
            var groups = BuildMenuGrouping.Group(Categories, Entries);
            CollectionAssert.AreEqual(new[] { Mining, Logistics }, groups.Select(g => g.CategoryGuid));
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuGrouping.test.ts">各カテゴリ内はサブカテゴリ定義順で空サブカテゴリを除外する</web-test>
        [Test]
        public void カテゴリの中はサブカテゴリ定義順で空のサブカテゴリを除く()
        {
            var logistics = BuildMenuGrouping.Group(Categories, Entries)[1];
            CollectionAssert.AreEqual(new[] { ChestSub, ConveyorSub }, logistics.Sections.Select(s => s.SubCategoryGuid));
            CollectionAssert.AreEqual(new[] { "木のチェスト" }, logistics.Sections[0].Entries.Select(e => e.DisplayLabel));
            Assert.AreEqual(Logistics, logistics.Sections[0].CategoryGuid);
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuGrouping.test.ts">エントリが空なら空配列</web-test>
        [Test]
        public void エントリが空なら群も空()
        {
            Assert.AreEqual(0, BuildMenuGrouping.Group(Categories, Array.Empty<BuildMenuCatalogEntry>()).Count);
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuGrouping.test.ts">カテゴリ定義に無いサブカテゴリのエントリしか無いカテゴリは群にならない</web-test>
        [Test]
        public void 定義に無いサブカテゴリのエントリしか無いカテゴリは群にならない()
        {
            // サイドバーがカテゴリ一致だけで判定すると、この群に出ないカテゴリが恒久的に無効で並ぶ
            // Judging the sidebar on a category match alone would leave this ungrouped category permanently disabled
            var stray = new[] { Entry("70000000-0000-4000-8000-000000000004", Building, ChestSub, "迷子ブロック") };
            Assert.AreEqual(0, BuildMenuGrouping.Group(Categories, stray).Count);
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuGrouping.test.ts">同一サブカテゴリ内はソートせずentries引数の並び順をそのまま維持する</web-test>
        [Test]
        public void 同じサブカテゴリの中は並べ替えず配信順を保つ()
        {
            var ordered = new[]
            {
                Entry("70000000-0000-4000-8000-000000000002", Logistics, ChestSub, "Zチェスト"),
                Entry("70000000-0000-4000-8000-000000000001", Logistics, ChestSub, "Aチェスト"),
            };
            var chest = BuildMenuGrouping.Group(Categories, ordered).Single(g => g.CategoryGuid == Logistics).Sections.Single(s => s.SubCategoryGuid == ChestSub);

            // 辞書順なら A→Z だが、配信順（Z→A）がそのまま出る
            // Dictionary order would be A then Z; the delivery order (Z then A) survives untouched
            CollectionAssert.AreEqual(new[] { "Zチェスト", "Aチェスト" }, chest.Entries.Select(e => e.DisplayLabel));
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuGrouping.test.ts">表示名の部分一致で大文字小文字を無視して絞り込む</web-test>
        [Test]
        public void 表示名の部分一致で絞り込む()
        {
            CollectionAssert.AreEqual(new[] { "鉄の採掘機" }, BuildMenuGrouping.Search("鉄", Entries).Select(e => e.DisplayLabel));
            CollectionAssert.AreEqual(new[] { "ベルトコンベア" }, BuildMenuGrouping.Search("ベルト", Entries).Select(e => e.DisplayLabel));
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuGrouping.test.ts">英字ラベルでも大文字小文字を無視して絞り込む</web-test>
        [Test]
        public void 英字の表示名は大文字小文字を無視して絞り込む()
        {
            // 小文字化を落とすと iron が Iron Chest に一致せず検出漏れになる
            // Dropping the lower-casing would make "iron" miss "Iron Chest"
            var english = new[] { Entry("70000000-0000-4000-8000-000000000003", Mining, MinerSub, "Iron Chest") };
            CollectionAssert.AreEqual(new[] { "Iron Chest" }, BuildMenuGrouping.Search("iron", english).Select(e => e.DisplayLabel));
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuGrouping.test.ts">空文字は全件を返す</web-test>
        [Test]
        public void 空の検索語は全件を返す()
        {
            Assert.AreEqual(3, BuildMenuGrouping.Search("", Entries).Count);
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuGrouping.test.ts">不一致は空配列</web-test>
        [Test]
        public void 一致しなければ空()
        {
            Assert.AreEqual(0, BuildMenuGrouping.Search("存在しない", Entries).Count);
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuGrouping.test.ts">絞り込み結果をグルーピングするとヒットの無いカテゴリが消える</web-test>
        [Test]
        public void 絞り込んでから群にするとヒットの無いカテゴリが消える()
        {
            var groups = BuildMenuGrouping.Group(Categories, BuildMenuGrouping.Search("鉄", Entries));
            CollectionAssert.AreEqual(new[] { Mining }, groups.Select(g => g.CategoryGuid));
        }
    }
}
