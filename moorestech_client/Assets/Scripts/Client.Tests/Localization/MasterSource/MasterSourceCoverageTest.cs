using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Game.Localization;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Localization.MasterSource
{
    /// <summary>
    /// 宣言表から生成された導出キー種が1つ残らず原文収集に届いているかを検証する再発防止テスト
    /// Regression guard asserting that every generated derived-key kind reaches source collection
    /// </summary>
    public class MasterSourceCoverageTest
    {
        private static readonly Guid ProbeGuid = Guid.Parse("0123abcd-0000-4000-8000-0000000000ff");

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void CollectCoversEveryDeclaredContentKeyKind()
        {
            var collectedKeys = MasterSourceTextCollector.Collect().Keys;
            var declaredKinds = BuildDeclaredKeyKinds();

            // 宣言表に行を足してCollectorへのループ追加を忘れたらここで落ちる
            // Adding a declaration row without adding the Collector loop fails here
            Assert.IsNotEmpty(declaredKinds);
            foreach (var kind in declaredKinds)
            {
                var collectedCount = collectedKeys.Count(key =>
                    key.StartsWith(kind.Prefix, StringComparison.Ordinal) &&
                    key.EndsWith(kind.Suffix, StringComparison.Ordinal));
                Assert.Greater(collectedCount, 0, $"{kind.Prefix}<guid>{kind.Suffix}");
            }

            #region Internal

            List<(string Prefix, string Suffix)> BuildDeclaredKeyKinds()
            {
                // 生成ビルダーをプローブGuidで叩き、キー形状を接頭辞と接尾辞へ分解する
                // Invoke each generated builder with a probe GUID and split the key shape into prefix and suffix
                var probeSegment = ProbeGuid.ToString("D");
                var kinds = new List<(string Prefix, string Suffix)>();
                foreach (var builder in typeof(ContentLocalizationKeys)
                             .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (builder.ReturnType != typeof(ContentLocalizationKey)) continue;

                    var key = ((ContentLocalizationKey)builder.Invoke(null, new object[] { ProbeGuid })).Key;
                    var segmentIndex = key.IndexOf(probeSegment, StringComparison.Ordinal);
                    Assert.Greater(segmentIndex, 0, builder.Name);
                    kinds.Add((key.Substring(0, segmentIndex), key.Substring(segmentIndex + probeSegment.Length)));
                }

                return kinds;
            }

            #endregion
        }
    }
}
