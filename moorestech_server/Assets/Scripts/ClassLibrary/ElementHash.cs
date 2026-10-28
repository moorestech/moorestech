using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace ClassLibrary
{
    // 要素ごとに独立したハッシュを作り、順序に依存しない加算で畳むための部品。Begin(種別, 識別子) → Add で項目を順に取り込む → Finish の値を呼び手が足す
    // Building block for per-element hashes folded by order-independent addition: Begin(kind, id) → Add each field → the caller sums the Finish values
    // 要素間に依存が無いので、どの順でも、何分割しても、別スレッドで分けて足しても同じ値になる。前の値に次を掛ける直列の鎖(FNV-1a)は分割すると値が変わるので使わない
    // Elements do not depend on each other, so any order, any partitioning and any threaded split-and-add gives the same value; a serial chain multiplying the previous value (FNV-1a) changes with the split, so it is not used
    // 取り込みは XOR→乗算→上位bitの折り返し。同期ずれの検出用で、暗号用途ではない。doubleとfloatはビット列で取り込むので1bitの差も別の値になる
    // Each word goes in by XOR, multiply and a high-bit fold; this detects synchronization drift and is not cryptographic. Doubles and floats go in as bit patterns, so a one-bit difference changes the value
    public static class ElementHash
    {
        // 種別と番号から要素を始める。番号を混ぜるので並びの入れ替えは別の値になる
        // Starts an element from its kind and index; mixing the index makes a reordering a different value
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Begin(uint kindSeed, int index)
        {
            unchecked { return kindSeed ^ ((uint)index * 0x9E3779B1u); }
        }

        // 件数だけの要素。空の網と1件の網を区別する
        // A count-only element, telling an empty network from one with a single entry
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Count(uint kindSeed, int count)
        {
            unchecked { return Finish(kindSeed ^ (uint)count); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Add(uint hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 0x85EBCA6Bu;
                return hash ^ (hash >> 15);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Add(uint hash, bool value)
        {
            return Add(hash, value ? 1 : 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Add(uint hash, long value)
        {
            unchecked { return Add(Add(hash, (int)value), (int)(value >> 32)); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Add(uint hash, double value)
        {
            return Add(hash, BitConverter.DoubleToInt64Bits(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Add(uint hash, float value)
        {
            return Add(hash, BitConverter.SingleToInt32Bits(value));
        }

        // Guid は16バイトをスタック上に書き出し、4バイトずつ little endian で取り込む(配列を確保しない)
        // A Guid is written to a 16-byte stack buffer and taken in as four little-endian words (no array allocation)
        public static uint Add(uint hash, Guid value)
        {
            Span<byte> bytes = stackalloc byte[16];
            value.TryWriteBytes(bytes);
            for (var i = 0; i < 16; i += 4) hash = Add(hash, BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(i, 4)));
            return hash;
        }

        // 最終撹拌。加算で畳んでも要素同士が打ち消し合いにくいよう、全bitへ広げてから足す
        // Final scramble; spreads over every bit before the additive fold so elements rarely cancel each other
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Finish(uint hash)
        {
            unchecked
            {
                hash *= 0xC2B2AE35u;
                return hash ^ (hash >> 16);
            }
        }
    }
}
