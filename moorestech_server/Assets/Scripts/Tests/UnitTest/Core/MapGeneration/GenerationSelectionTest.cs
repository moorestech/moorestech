using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.GenerationModule;
using NUnit.Framework;

namespace Tests.UnitTest.Core.MapGeneration
{
    /// <summary>
    ///     複数modのgeneration.json候補からpriority最大の1件を選ぶ純粋関数GenerationSelection.Selectを検証するテスト
    ///     Tests for the pure selection function GenerationSelection.Select, which picks the max-priority
    ///     entry across multiple mods' generation.json candidates
    /// </summary>
    public class GenerationSelectionTest
    {
        [Test]
        public void priorityが最大の候補が選ばれる()
        {
            // 3mod分の候補を用意しpriority最大(modB)が選ばれることを検証
            // Prepare 3 mods' candidates and assert the max priority one (modB) is selected
            var low = new Generation(Generation.AlgorithmConst.VanillaGenerator, 10, null);
            var high = new Generation(Generation.AlgorithmConst.VanillaGenerator, 500, null);
            var mid = new Generation(Generation.AlgorithmConst.VanillaGenerator, 100, null);

            var candidates = new List<(Generation Element, string ModId)>
            {
                (low, "modA"),
                (high, "modB"),
                (mid, "modC")
            };

            var selected = GenerationSelection.Select(candidates);

            Assert.AreSame(high, selected);
        }

        [Test]
        public void 同priorityはModIdのOrdinal昇順で若い方が選ばれる()
        {
            // priorityが同値の2候補からmodIdの文字列順で若い方(aaa)が選ばれることを検証
            // Two candidates share the same priority; assert the lexicographically-earlier modId (aaa) wins
            var candidateFromZzz = new Generation(Generation.AlgorithmConst.VanillaGenerator, 100, null);
            var candidateFromAaa = new Generation(Generation.AlgorithmConst.VanillaGenerator, 100, null);

            var candidates = new List<(Generation Element, string ModId)>
            {
                (candidateFromZzz, "zzz:mod"),
                (candidateFromAaa, "aaa:mod")
            };

            var selected = GenerationSelection.Select(candidates);

            Assert.AreSame(candidateFromAaa, selected);
        }

        [Test]
        public void algorithmがNoneのみまたは候補ゼロなら未定義になる()
        {
            // algorithm:Noneは選択対象から除外され、有効な候補が無ければ未定義(null)になる
            // algorithm:None is excluded from selection; with no valid candidates the result is undefined (null)
            var noneOnly = new Generation(Generation.AlgorithmConst.None, 999, null);
            var noneCandidates = new List<(Generation Element, string ModId)> { (noneOnly, "modA") };

            Assert.IsNull(GenerationSelection.Select(noneCandidates));
            Assert.IsNull(GenerationSelection.Select(new List<(Generation Element, string ModId)>()));
        }
    }
}
