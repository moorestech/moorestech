using System;

namespace Core.Update
{
    // 世界状態に影響する乱数の唯一の供給源。状態をセーブへ含め、同一スナップショット＋同一パケット列で同一結果にする
    // Sole source of world-affecting randomness; its state is saved so the same snapshot plus packets replays identically
    public static class GameRandom
    {
        public const int StateLength = 4;

        // 状態はtickスレッド専有。世界を進める処理以外（受信スレッド・書き出しスレッド・クライアント）から引いてはならない
        // The state belongs to the tick thread alone; no receive thread, writer thread, or client may draw from it
        private static ulong _s0, _s1, _s2, _s3;

        static GameRandom()
        {
            Reseed(0UL);
        }

        // SplitMix64で4語の状態を初期化する（移行スクリプトと同じ手順であること）
        // Seed the four state words with SplitMix64 (must match the migration script)
        public static void Reseed(ulong seed)
        {
            var x = seed;
            _s0 = SplitMix64(ref x);
            _s1 = SplitMix64(ref x);
            _s2 = SplitMix64(ref x);
            _s3 = SplitMix64(ref x);
        }

        public static ulong[] ExportState()
        {
            return new[] { _s0, _s1, _s2, _s3 };
        }

        public static void RestoreState(ulong[] state)
        {
            if (state == null || state.Length != StateLength) throw new ArgumentException($"randomState は {StateLength} 語でなければならない");
            _s0 = state[0];
            _s1 = state[1];
            _s2 = state[2];
            _s3 = state[3];
        }

        // xoshiro256**
        public static ulong NextUlong()
        {
            var result = RotateLeft(_s1 * 5UL, 7) * 9UL;
            var t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = RotateLeft(_s3, 45);
            return result;
        }

        // 上位53bitを使い [0,1) を作る
        // Build [0,1) from the top 53 bits
        public static double NextDouble()
        {
            return (NextUlong() >> 11) * (1.0 / 9007199254740992.0);
        }

        public static int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentException("maxExclusive は minInclusive より大きくなければならない");
            var range = (ulong)((long)maxExclusive - minInclusive);
            return (int)(minInclusive + (long)(NextUlong() % range));
        }

        public static int NextInt()
        {
            return (int)(uint)(NextUlong() >> 32);
        }

        public static long NextLong()
        {
            return (long)NextUlong();
        }

        // 128bitを取りRFC4122のversion4形式に整えて決定的なGuidを作る
        // Take 128 bits and shape them as an RFC4122 version-4 GUID deterministically
        public static Guid NextGuid()
        {
            var bytes = new byte[16];
            BitConverter.GetBytes(NextUlong()).CopyTo(bytes, 0);
            BitConverter.GetBytes(NextUlong()).CopyTo(bytes, 8);
            bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
            return new Guid(bytes);
        }

        private static ulong SplitMix64(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            var z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private static ulong RotateLeft(ulong x, int k)
        {
            return (x << k) | (x >> (64 - k));
        }
    }
}
