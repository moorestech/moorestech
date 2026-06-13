using System.Collections.Generic;

namespace Game.Block.Blocks.CleanRoom
{
    // 半導体チップのレベル抽選コア。EUV失敗→天井→基礎分布→down-bin→[品質シフト挿入点]→確定。
    // 設計書§4 の処理順（down-bin が品質シフトより先）を固定する。純粋・決定的・マスタ非依存。
    // Semiconductor level-draw core: EUV fail -> ceiling -> base draw -> down-bin -> [quality-shift slot] -> confirm.
    // Pure, deterministic, master-independent; pins the spec §4 order (down-bin before quality shift).
    public static class SemiconductorChipDraw
    {
        // RNG サブストリーム salt（相関回避）。0x…0004 は将来アップグレードB の品質シフト用に予約。
        // RNG sub-stream salts; 0x…0004 is reserved for the future upgrade-B quality shift.
        private const ulong SaltLevelDraw = 0xA5A5_0000_0000_0001UL;
        private const ulong SaltDownBin = 0xA5A5_0000_0000_0002UL;
        private const ulong SaltEuvFail = 0xA5A5_0000_0000_0003UL;

        // 抽選本体。dist は level 昇順前提（マスタ読み出し側でソート済み）。false=出力なし（EUV失敗 or MaxGrade=0）。
        // Main draw. dist must be sorted ascending by level. Returns false when no output (EUV fail / MaxGrade=0).
        public static bool TryDrawLevel(
            IReadOnlyList<(int level, double weight)> dist,
            int maxGrade, double downBinRate, double euvSuccessPercent,
            long deterministicSeed, int outputIndex, out int level)
        {
            level = 0;

            // 1. EUV catastrophic 失敗（出力なし）。レベル抽選とは別 salt で独立
            // 1. EUV catastrophic failure (no output), independent salt from the level draw
            if (Roll(deterministicSeed, SaltEuvFail, outputIndex) >= euvSuccessPercent) return false;

            // 2. 天井。MaxGrade=0（Out）は抽選せず出力なし（サイレント Lv1 禁止）
            // 2. Ceiling. MaxGrade=0 (Out) -> no draw, no output (no silent Lv1)
            if (maxGrade <= 0) return false;

            // 3. 基礎分布抽選（天井超え分を切り落として比例再正規化）
            // 3. Base draw, truncating above-ceiling mass and renormalizing proportionally
            var baseLv = DrawBaseLevel(dist, maxGrade, deterministicSeed, outputIndex);
            if (baseLv <= 0) return false; // 分布が天井以下に質量を持たない（マスタ不備）→出力なし

            // 4. down-bin：DownBinRate で1段格下げ（Lv1 は下げ先無しで据え置き）
            // 4. Down-bin: demote one level at DownBinRate (Lv1 has no lower target)
            var finalLv = baseLv;
            if (finalLv > 1 && Roll(deterministicSeed, SaltDownBin, outputIndex) < downBinRate) finalLv -= 1;

            // 5. [品質シフト挿入点] 将来アップグレードB がここで上位寄せを差す（salt 0x…0004 予約）。本フェーズは中立。
            //    設計書§4 の処理順どおり down-bin の後・確定の前に置く。
            // 5. [Quality-shift slot] Upgrade-B inserts the upward shift here (salt 0x…0004 reserved); neutral now.

            level = finalLv;
            return true;
        }

        #region Test helpers
        // テストが down-bin 前後を比較するための可視ヘルパ
        // Visible helper so tests can compare pre/post down-bin
        public static int DrawBaseLevelForTest(IReadOnlyList<(int level, double weight)> dist, int maxGrade, long seed, int outputIndex)
        {
            return DrawBaseLevel(dist, maxGrade, seed, outputIndex);
        }
        #endregion

        // 基礎分布から天井以下の Lv を1つ抽選。天井以下に質量が無ければ 0（=出力なし）を返す
        // Draw one Lv (<= ceiling); returns 0 (no output) when no mass remains under the ceiling
        private static int DrawBaseLevel(IReadOnlyList<(int level, double weight)> dist, int maxGrade, long seed, int outputIndex)
        {
            double totalWeight = 0;
            foreach (var (level, weight) in dist)
                if (level <= maxGrade) totalWeight += weight;
            if (totalWeight <= 0) return 0;

            var roll = Roll(seed, SaltLevelDraw, outputIndex) * totalWeight;
            double acc = 0;
            var chosen = 0;
            foreach (var (level, weight) in dist)
            {
                if (level > maxGrade) continue;
                acc += weight;
                chosen = level; // 浮動小数の端で roll≥totalWeight の場合は最後の（=最大の）天井内 Lv
                if (roll < acc) break;
            }
            return chosen;
        }

        // salt＋出力要素インデックス付き splitmix64：seed から [0,1) を決定的に返す
        // Salted splitmix64 with output-index mixing: deterministic [0,1) from the cycle seed
        private static double Roll(long seed, ulong salt, int outputIndex)
        {
            var x = (ulong)seed * 0x9E3779B97F4A7C15UL + salt + (ulong)(outputIndex + 1) * 0xBF58476D1CE4E5B9UL;
            x ^= x >> 30; x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27; x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return (x >> 11) * (1.0 / (1UL << 53));
        }
    }
}
