using System.Collections.Generic;
using Core.Master;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    public static class CleanRoomChipDraw
    {
        private const ulong EuvSalt = 0xD1B54A32D192ED03UL;
        private const ulong BaseDrawSalt = 0x8CB92BA72F3D8DD7UL;
        private const ulong DownBinSalt = 0xDB4F0B9175AE2165UL;

        public enum Result
        {
            NotLeveled,
            NoOutput,
            Drawn,
        }

        public static Result TryDraw(IReadOnlyList<(int level, double weight, ItemId chipItemId)> dist,
            int maxLevel, double downBinRate, double euvSuccessRate,
            long deterministicSeed, int outputIndex, out ItemId itemId)
        {
            itemId = default;
            if (dist.Count == 0) return Result.NotLeveled;

            // EUV失敗は候補抽選より先に出力なしとして確定する
            // EUV failure resolves to no output before consuming the candidate draw
            if (Roll(deterministicSeed, EuvSalt, outputIndex) >= euvSuccessRate) return Result.NoOutput;

            // 最大レベルを超えた候補を除き、残った重みだけで抽選母集団を作る
            // Exclude candidates above the max level and build the draw pool from remaining weights
            var filtered = new List<(int level, double weight, ItemId chipItemId)>();
            var totalWeight = 0d;
            for (var i = 0; i < dist.Count; i++)
            {
                var entry = dist[i];
                if (entry.level > maxLevel) continue;

                filtered.Add(entry);
                totalWeight += entry.weight;
            }

            if (filtered.Count == 0 || totalWeight <= 0) return Result.NoOutput;

            // 切り捨て後の総重みへロールを写像し、浮動小数の端は最後の候補へ寄せる
            // Map the roll onto the truncated total weight and clamp rounding edges to the last candidate
            var selected = filtered[filtered.Count - 1];
            var roll = Roll(deterministicSeed, BaseDrawSalt, outputIndex) * totalWeight;
            var cumulative = 0d;
            for (var i = 0; i < filtered.Count; i++)
            {
                cumulative += filtered[i].weight;
                if (roll >= cumulative) continue;

                selected = filtered[i];
                break;
            }

            // ダウンビンは1段だけ下げ、欠番データでは現在レベルを維持する
            // Down-binning lowers by one step only and keeps the current level if data is missing
            if (selected.level > 1 && Roll(deterministicSeed, DownBinSalt, outputIndex) < downBinRate)
            {
                for (var i = 0; i < dist.Count; i++)
                {
                    if (dist[i].level != selected.level - 1) continue;

                    selected = dist[i];
                    break;
                }
            }

            itemId = selected.chipItemId;
            return Result.Drawn;
        }

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
