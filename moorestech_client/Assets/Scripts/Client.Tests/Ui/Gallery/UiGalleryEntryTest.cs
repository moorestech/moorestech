using System.Linq;
using Client.Localization;
using Client.Tests.Ui.Conventions;
using Client.Tests.Ui.TestHost;
using Client.Ui.Editor.Gallery;
using NUnit.Framework;

namespace Client.Tests.Ui.Gallery
{
    /// <summary>
    /// 見本の集約・一意・寸法を確かめる（L1）
    /// Verifies entry collection, uniqueness and size (L1)
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

            // 見本を載せて寸法を見る
            // Mount each sample and check its size
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
            // 族と区画が同じ順
            // Families and sections share order
            var familySections = System.Enum.GetNames(typeof(UiGalleryFamily)).Select(name => $"Components/{name}").ToList();
            var themeSections = UiThemeImportBundleRules.SectionOrder.Where(section => section.StartsWith("Components/")).ToList();
            CollectionAssert.AreEqual(themeSections, familySections);
        }
    }
}
