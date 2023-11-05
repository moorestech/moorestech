// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace MessagePack.Internal
{
    // like ArraySegment<byte> hashtable.
    // Add is safe for construction phase only and requires capacity(does not do rehash)
    // and specialized for internal use(nongenerics, TValue is int)

    // internal, but code generator requires this class
    // or at least PerfBenchmarkDotNet
    public class ByteArrayStringHashTable : IEnumerable<KeyValuePair<string, int>>
    {
        private static readonly bool Is32Bit = IntPtr.Size == 4;
        private readonly Entry[][] buckets; // immutable array(faster than linkedlist)
        private readonly ulong indexFor;

        public ByteArrayStringHashTable(int capacity)
            : this(capacity, 0.42f) // default: 0.75f -> 0.42f
        {
        }

        public ByteArrayStringHashTable(int capacity, float loadFactor)
        {
            var tableSize = CalculateCapacity(capacity, loadFactor);
            buckets = new Entry[tableSize][];
            indexFor = (ulong)buckets.Length - 1;
        }

        // only for Debug use
        public IEnumerator<KeyValuePair<string, int>> GetEnumerator()
        {
            var b = buckets;

            foreach (var item in b)
            {
                if (item == null) continue;

                foreach (var item2 in item) yield return new KeyValuePair<string, int>(Encoding.UTF8.GetString(item2.Key), item2.Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(string key, int value)
        {
            if (!TryAddInternal(Encoding.UTF8.GetBytes(key), value)) throw new ArgumentException("Key was already exists. Key:" + key);
        }

        public void Add(byte[] key, int value)
        {
            if (!TryAddInternal(key, value)) throw new ArgumentException("Key was already exists. Key:" + key);
        }

        private bool TryAddInternal(byte[] key, int value)
        {
            var h = ByteArrayGetHashCode(key);
            var entry = new Entry { Key = key, Value = value };

            var array = buckets[h & indexFor];
            if (array == null)
            {
                buckets[h & indexFor] = new[] { entry };
            }
            else
            {
                // check duplicate
                for (var i = 0; i < array.Length; i++)
                {
                    var e = array[i].Key;
                    if (key.AsSpan().SequenceEqual(e)) return false;
                }

                var newArray = new Entry[array.Length + 1];
                Array.Copy(array, newArray, array.Length);
                array = newArray;
                array[array.Length - 1] = entry;
                buckets[h & indexFor] = array;
            }

            return true;
        }

        public bool TryGetValue(in ReadOnlySequence<byte> key, out int value)
        {
            return TryGetValue(CodeGenHelpers.GetSpanFromSequence(key), out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(ReadOnlySpan<byte> key, out int value)
        {
            var table = buckets;
            var hash = ByteArrayGetHashCode(key);
            var entry = table[hash & indexFor];

            if (entry == null)
            {
                value = default;
                return false;
            }

            ref var v = ref entry[0];
            if (key.SequenceEqual(v.Key))
            {
                value = v.Value;
                return true;
            }

            return TryGetValueSlow(key, entry, out value);
        }

        private bool TryGetValueSlow(ReadOnlySpan<byte> key, Entry[] entry, out int value)
        {
            for (var i = 1; i < entry.Length; i++)
            {
                ref var v = ref entry[i];
                if (key.SequenceEqual(v.Key))
                {
                    value = v.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ByteArrayGetHashCode(ReadOnlySpan<byte> x)
        {
            // FarmHash https://github.com/google/farmhash
            if (Is32Bit)
                return FarmHash.Hash32(x);
            return FarmHash.Hash64(x);
        }

        private static int CalculateCapacity(int collectionSize, float loadFactor)
        {
            var initialCapacity = (int)(collectionSize / loadFactor);
            var capacity = 1;
            while (capacity < initialCapacity) capacity <<= 1;

            if (capacity < 8) return 8;

            return capacity;
        }

        private struct Entry
        {
            public byte[] Key;
            public int Value;

            // for debugging
            public override string ToString()
            {
                return "(" + Encoding.UTF8.GetString(Key) + ", " + Value + ")";
            }
        }
    }
}