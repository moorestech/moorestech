using System.Linq;
using Client.Localization;
using Client.Tests.Ui.Conventions;
using Client.Tests.Ui.TestHost;
using Client.Ui.Editor.Gallery;
using NUnit.Framework;

namespace Client.Tests.Ui.Gallery
{
    /// <summary>
    /// ギャラリーの見本が全部集まり、名前が族の中で一意で、本番のテーマのパネルで寸法を持って描けることを確かめる（L1・設計書 A-9）
    /// Verifies all gallery entries are collected, names are unique per family, and each sample lays out with size on a production-themed panel (L1, design A-9)
    /// </summary>
    public class UiGalleryEntryTest : UiTestHost
    {
        [Test]
        public void EntriesAreCollectedUniqueAndRenderWithSize()
        {
            // 見本の LocalizedLabel が載った時点で辞書を引くので先に読み込む
            // Sample LocalizedLabels look up the dictionary on attach, so load it first
            Localize.Initialize();

            var entries = UiGalleryEntryCatalog.CreateAll();
            CollectionAssert.IsSubsetOf(new[] { "PanelActionButton", "ModeSwitch" }, entries.Select(entry => entry.ComponentName).ToList());
            Assert.AreEqual(entries.Count, entries.Select(entry => (entry.Family, entry.ComponentName)).Distinct().Count(), "duplicate gallery entry");

            // 見本を載せ、テーマのトークンが効いて寸法が出ることを見る
            // Mount each sample and check theme tokens give it a size
            foreach (var entry in entries)
            {
                var sample = entry.CreateSample();
                rootVisualElement.Add(sample);
                simulate.FrameUpdate();
                Assert.Greater(sample.layout.width, 0f, entry.ComponentName);
                Assert.Greater(sample.layout.height, 0f, entry.ComponentName);
                rootVisualElement.Remove(sample);
            }
        }

        [Test]
        public void GalleryFamiliesMatchThemeComponentSections()
        {
            // ギャラリーの族と tss の Components 区画が同じ 8 族・同じ順（片方だけ足すずれを止める）
            // Gallery families match the tss Components sections in name and order, so neither can grow alone
            var familySections = System.Enum.GetNames(typeof(UiGalleryFamily)).Select(name => $"Components/{name}").ToList();
            var themeSections = UiThemeImportBundleRules.SectionOrder.Where(section => section.StartsWith("Components/")).ToList();
            CollectionAssert.AreEqual(themeSections, familySections);
        }
    }
}
