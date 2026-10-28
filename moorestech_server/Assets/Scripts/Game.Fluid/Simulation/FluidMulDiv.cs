using System;

namespace Game.Fluid.Simulation
{
    /// <summary>
    ///     a × b / c を 128 ビットの途中値で正確に求める(0 へ向けて切り捨て)。パイプ網の圧力(量 × 容量比)と比例縮小(希望 × 残量 / 希望合計)が使う。
    ///     どちらも商が被乗数以下になる形なので long に収まる。c は正。純粋な整数演算なので double を経由せず、サーバーとクライアントで同じ結果になる(ADR 0090 追記)。
    ///
    ///     Computes a × b / c exactly through a 128-bit intermediate, truncating toward zero. Used by the pipe network's pressure (amount × capacity ratio) and proportional scaling (desired × amount / sum).
    ///     Both have quotients no larger than the multiplicand, so the result fits in a long. c must be positive. Pure integer arithmetic, no double, so server and client agree bit for bit (ADR 0090 addendum).
    /// </summary>
    public static class FluidMulDiv
    {
        public static long MulDiv(long a, long b, long c)
        {
            if (c <= 0) throw new ArgumentOutOfRangeException(nameof(c), c, "divisor must be positive");

            var negative = (a < 0) != (b < 0);
            var ua = ToMagnitude(a);
            var ub = ToMagnitude(b);
            var uc = (ulong)c;

            Multiply(ua, ub, out var hi, out var lo);

            // 積が 64 ビットに収まる(大半の面)なら通常の割り算で済ませる
            // When the product fits in 64 bits (most faces) a plain division is enough
            var quotient = hi == 0 ? lo / uc : Divide128By64(hi, lo, uc);
            if (long.MaxValue < quotient) throw new OverflowException($"MulDiv quotient does not fit in a long: {a} * {b} / {c}");
            return negative ? -(long)quotient : (long)quotient;
        }

        // long.MinValue も扱えるよう、符号反転を ulong 側で行う
        // Negation is done on the ulong side so long.MinValue is handled too
        private static ulong ToMagnitude(long value)
        {
            return value < 0 ? (ulong)(-(value + 1)) + 1 : (ulong)value;
        }

        // 64 × 64 → 128 ビットの掛け算。32 ビットの半分同士の積 4 つを桁上がりつきで足す
        // 64 × 64 → 128-bit multiply from the four products of the 32-bit halves with carries
        private static void Multiply(ulong x, ulong y, out ulong hi, out ulong lo)
        {
            var x0 = (ulong)(uint)x;
            var x1 = x >> 32;
            var y0 = (ulong)(uint)y;
            var y1 = y >> 32;
            var p00 = x0 * y0;
            var p01 = x0 * y1;
            var p10 = x1 * y0;
            var p11 = x1 * y1;

            // 中段は 3 つの 32 ビット値の和なので 64 ビットに収まる
            // The middle column sums three 32-bit values, so it fits in 64 bits
            var mid = (p00 >> 32) + (uint)p01 + (uint)p10;
            lo = (mid << 32) | (uint)p00;
            hi = p11 + (p01 >> 32) + (p10 >> 32) + (mid >> 32);
        }

        // 128 ÷ 64 → 64 ビットの割り算(Hacker's Delight divlu2)。除数を正規化し、32 ビットの桁 2 つを順に推定して補正する。hi < divisor が前提
        // 128 ÷ 64 → 64-bit division (Hacker's Delight divlu2): normalize the divisor, then estimate and correct two 32-bit digits in turn. Requires hi < divisor
        private static ulong Divide128By64(ulong hi, ulong lo, ulong divisor)
        {
            if (divisor <= hi) throw new OverflowException("MulDiv quotient does not fit in 64 bits");

            const ulong b = 1UL << 32;
            var s = LeadingZeroCount(divisor);
            var v = divisor << s;
            var vn1 = v >> 32;
            var vn0 = v & 0xFFFFFFFFUL;
            var un32 = (hi << s) | (s == 0 ? 0UL : lo >> (64 - s));
            var un10 = lo << s;
            var un1 = un10 >> 32;
            var un0 = un10 & 0xFFFFFFFFUL;

            var q1 = un32 / vn1;
            var rhat = un32 - q1 * vn1;
            while (b <= q1 || b * rhat + un1 < q1 * vn0)
            {
                q1--;
                rhat += vn1;
                if (b <= rhat) break;
            }

            var un21 = un32 * b + un1 - q1 * v;
            var q0 = un21 / vn1;
            rhat = un21 - q0 * vn1;
            while (b <= q0 || b * rhat + un0 < q0 * vn0)
            {
                q0--;
                rhat += vn1;
                if (b <= rhat) break;
            }

            return q1 * b + q0;
        }

        private static int LeadingZeroCount(ulong value)
        {
            var count = 0;
            if (value >> 32 == 0) { count += 32; value <<= 32; }
            if (value >> 48 == 0) { count += 16; value <<= 16; }
            if (value >> 56 == 0) { count += 8; value <<= 8; }
            if (value >> 60 == 0) { count += 4; value <<= 4; }
            if (value >> 62 == 0) { count += 2; value <<= 2; }
            if (value >> 63 == 0) count += 1;
            return count;
        }
    }
}
