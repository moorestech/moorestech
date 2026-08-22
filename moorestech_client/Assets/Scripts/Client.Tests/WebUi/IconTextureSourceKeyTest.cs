using Client.WebUiHost.Game.Icons;
using NUnit.Framework;

namespace Client.Tests.WebUi
{
    public class IconTextureSourceKeyTest
    {
        // 経路プレフィクスとキー書式の対応表。webui側の *_ICON_PREFIX と同じ文字列を固定する
        // Table of path prefix to key format; pins the same strings the webui uses for *_ICON_PREFIX
        private static readonly object[] _keyCases =
        {
            new object[] { new ItemIconSource(), "/api/icons/", "12", "abc" },
            new object[] { new BlockIconSource(), "/api/block-icons/", "12", "abc" },
            new object[] { new FluidIconSource(), "/api/fluid-icons/", "9eae6979-d56a-4991-9107-b8161acec430", "12" },
            new object[] { new TrainCarIconSource(), "/api/train-car-icons/", "9eae6979-d56a-4991-9107-b8161acec430", "12" },
            new object[] { new ConnectToolIconSource(), "/api/connect-tool-icons/", "9eae6979-d56a-4991-9107-b8161acec430", "12" },
        };

        [TestCaseSource(nameof(_keyCases))]
        public void PathPrefixAndKeyFormatAreFixedPerSource(IIconTextureSource source, string expectedPrefix, string validKey, string invalidKey)
        {
            Assert.AreEqual(expectedPrefix, source.PathPrefix);

            // 不正キーを解決前に弾けることが、負キャッシュの無制限増加を防ぐ唯一の関門
            // Rejecting a bad key before resolving is the only gate that keeps the negative cache from growing without bound
            Assert.IsTrue(source.IsValidKey(validKey));
            Assert.IsFalse(source.IsValidKey(invalidKey));
            Assert.IsFalse(source.IsValidKey(""));
        }
    }
}
