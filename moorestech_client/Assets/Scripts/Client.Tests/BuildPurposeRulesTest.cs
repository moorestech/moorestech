using Client.Editor.Build;
using NUnit.Framework;

namespace Client.Tests
{
    public class BuildPurposeRulesTest
    {
        // 全用途のstrict・同梱・展示会・Development方針を固定する
        // Lock strict, bundling, exhibition, and Development policy for every purpose
        [TestCase(BuildPurpose.Ci, false, false, false, false, true)]
        [TestCase(BuildPurpose.Ci, true, false, false, false, true)]
        [TestCase(BuildPurpose.LocalDevelopment, false, false, true, false, false)]
        [TestCase(BuildPurpose.LocalDevelopment, true, false, true, false, true)]
        [TestCase(BuildPurpose.Exhibition, false, true, true, true, false)]
        [TestCase(BuildPurpose.Exhibition, true, true, true, true, false)]
        [TestCase(BuildPurpose.SteamPlaytest, false, true, true, false, false)]
        [TestCase(BuildPurpose.SteamPlaytest, true, true, true, false, false)]
        public void 用途から4つのビルド方針を導く(
            BuildPurpose purpose, bool localDevelopmentChoice, bool strict, bool gameData, bool exhibitionScript, bool development)
        {
            Assert.AreEqual(strict, BuildPurposeRules.IsStrictBundling(purpose));
            Assert.AreEqual(gameData, BuildPurposeRules.BundlesLocalGameData(purpose));
            Assert.AreEqual(exhibitionScript, BuildPurposeRules.BundlesExhibitionLaunchScript(purpose));
            Assert.AreEqual(development, BuildPurposeRules.IsDevelopmentBuild(purpose, localDevelopmentChoice));
        }
    }
}
