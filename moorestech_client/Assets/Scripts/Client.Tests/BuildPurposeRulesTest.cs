using System;
using Client.Build.Policy;
using NUnit.Framework;

namespace Client.Tests
{
    public class BuildPurposeRulesTest
    {
        // 用途ごとの6方針を固定
        // Lock the 6 policies per purpose
        [TestCase(BuildPurpose.Ci, false, false, false, false, false, false, true)]
        [TestCase(BuildPurpose.Ci, true, false, false, false, false, false, true)]
        [TestCase(BuildPurpose.LocalDevelopment, false, false, true, false, false, false, false)]
        [TestCase(BuildPurpose.LocalDevelopment, true, false, true, false, false, false, true)]
        [TestCase(BuildPurpose.Exhibition, false, true, true, true, true, true, false)]
        [TestCase(BuildPurpose.Exhibition, true, true, true, true, true, true, false)]
        [TestCase(BuildPurpose.SteamPlaytest, false, true, true, false, true, true, false)]
        [TestCase(BuildPurpose.SteamPlaytest, true, true, true, false, true, true, false)]
        public void 用途から6つのビルド方針を導く(
            BuildPurpose purpose, bool localDevelopmentChoice, bool strict, bool gameData, bool exhibitionScript,
            bool pinsAppleSilicon, bool reSignsMacApp, bool development)
        {
            var policy = BuildPurposeRules.Resolve(purpose, localDevelopmentChoice);

            Assert.AreEqual(strict, policy.IsStrictBundling);
            Assert.AreEqual(gameData, policy.BundlesLocalGameData);
            Assert.AreEqual(exhibitionScript, policy.BundlesExhibitionLaunchScript);
            Assert.AreEqual(pinsAppleSilicon, policy.PinsAppleSilicon);
            Assert.AreEqual(reSignsMacApp, policy.ReSignsMacApp);
            Assert.AreEqual(development, policy.IsDevelopmentBuild);
        }

        // 用途未指定はビルド方針を持たない（0値の書き忘れを検出する）
        // An unspecified purpose has no policy, which surfaces a forgotten assignment
        [Test]
        public void 用途未指定は方針を導けない()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildPurposeRules.Resolve(BuildPurpose.Unspecified, false));
        }
    }
}
