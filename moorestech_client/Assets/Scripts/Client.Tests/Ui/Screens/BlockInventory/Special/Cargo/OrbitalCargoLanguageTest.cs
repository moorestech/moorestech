using System;
using System.IO;
using Client.Game.Localization;
using Client.Localization;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Special.Cargo;
using Client.Ui.State.Text;
using Core.Master;
using Mod.Loader;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.BlockInventory.Special.Cargo.OrbitalCargoSamples;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.Cargo
{
    public class OrbitalCargoLanguageTest : SpecialScreenTestHost
    {
        [Test]
        public void 同じ積荷行と見出しと次便が言語切替に追従する()
        {
            var content = LoadContent();
            LoadCargoNames();
            Assert.IsTrue(Localize.TrySetChosenLanguage("japanese"));

            // 一覧と見出しの同じラベルを切替前後で読む
            // Read the same mounted list and headline labels before and after the switch
            var cargo = Pad(Cable, ChipLv2, Cable, ChipLv2);
            var list = Mount(new OrbitalCargoPane("list", Kit(content), new VisualElement(), false));
            var detail = Mount(new OrbitalCargoPane("detail", Kit(content), new VisualElement(), false));
            list.Render(cargo, UiText.Empty, true);
            detail.Render(cargo, UiText.Empty, false);
            var chip = Find<LocalizedLabel>(list, "list-cargo-row-2-name");
            var cable = Find<LocalizedLabel>(list, "list-cargo-row-3-name");
            var header = Find<LocalizedLabel>(detail, "detail-cargo-header-name");
            var next = Find<LocalizedLabel>(detail, "detail-cargo-next-flight");

            var japanese = new[] { "チップ ×20 → 成果 ×40", "ケーブル ×1000 → ケーブルプール +1000",
                "チップ ×20 → 成果 ×40", "次の便から ケーブル ×1000 → ケーブルプール +1000" };
            CollectionAssert.AreEqual(japanese, Displayed());
            Assert.IsTrue(Localize.TrySetChosenLanguage("english"));
            CollectionAssert.AreEqual(new[] { "Chip ×20 → Output ×40", "Cable ×1000 → Cable pool +1000",
                "Chip ×20 → Output ×40", "From the next launch: Cable ×1000 → Cable pool +1000" }, Displayed());

            // 外れている間に言語を戻し、同じ行と見出しを再び載せる
            // Switch back while detached, then remount the same rows and headline
            list.RemoveFromHierarchy();
            detail.RemoveFromHierarchy();
            Assert.IsTrue(Localize.TrySetChosenLanguage("japanese"));
            Mount(list);
            Mount(detail);
            CollectionAssert.AreEqual(japanese, Displayed());

            #region Internal

            string[] Displayed()
            {
                var names = new[] { chip.SourceText, cable.SourceText, header.SourceText, next.SourceText };
                TestContext.WriteLine(string.Join(" | ", names));
                return names;
            }

            #endregion
        }

        private static void LoadCargoNames()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"cargo-names-{Guid.NewGuid():N}");
            var mod = Path.Combine(directory, "names");
            Directory.CreateDirectory(Path.Combine(mod, "master"));
            Directory.CreateDirectory(Path.Combine(mod, "localization"));
            try
            {
                // 実際のマスタIDに二言語の品名を与える
                // Provide bilingual names for the actual master IDs
                File.WriteAllText(Path.Combine(mod, "master", "modMeta.json"),
                    "{\"id\":\"names\",\"name\":\"names\",\"version\":\"1.0\",\"author\":\"test\",\"description\":\"test\"}");
                File.WriteAllText(Path.Combine(mod, "localization", "localization.csv"),
                    $"key,Source,english,japanese\n{Key(ChipLv2)},Chip,Chip,チップ\n{Key(Cable)},Cable,Cable,ケーブル\n{Key(ComputeData)},Output,Output,成果\n");
                Localize.MergeGameDictionaries(new ModsResource(directory), new[] { new ModId("test:names") }, MasterSourceTextCollector.Collect());
            }
            finally
            {
                Directory.Delete(directory, true);
            }

            #region Internal

            string Key(ItemId id) => ContentLocalizationKeys.ItemName(MasterHolder.ItemMaster.GetItemGuid(id)).Key;

            #endregion
        }
    }
}
