// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

#pragma warning disable SA1509 // Opening braces should not be preceded by blank line

namespace MessagePack.Internal
{
    // Key = long, Value = int for UTF8String Dictionary

    /// <remarks>
    ///     This code is used by dynamically generated code as well as AOT generated code,
    ///     and thus must be public for the "C# generated and compiled into saved assembly" scenario.
    /// </remarks>
    public class AutomataDictionary : IEnumerable<KeyValuePair<string, int>>
    {
        private readonly AutomataNode root;

        public AutomataDictionary()
        {
            root = new AutomataNode(0);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<string, int>> GetEnumerator()
        {
            return YieldCore(root.YieldChildren()).GetEnumerator();
        }

        public void Add(string str, int value)
        {
            ReadOnlySpan<byte> bytes = Encoding.UTF8.GetBytes(str);
            var node = root;

            while (bytes.Length > 0)
            {
                var key = AutomataKeyGen.GetKey(ref bytes);

                if (bytes.Length == 0)
                    node = node.Add(key, value, str);
                else
                    node = node.Add(key);
            }
        }

        public bool TryGetValue(in ReadOnlySequence<byte> bytes, out int value)
        {
            return TryGetValue(bytes.ToArray(), out value);
        }

        public bool TryGetValue(ReadOnlySpan<byte> bytes, out int value)
        {
            var node = root;

            while (bytes.Length > 0 && node != null) node = node.SearchNext(ref bytes);

            if (node == null)
            {
                value = -1;
                return false;
            }

            value = node.Value;
            return true;
        }

        // for debugging
        public override string ToString()
        {
            var sb = new StringBuilder();
            ToStringCore(root.YieldChildren(), sb, 0);
            return sb.ToString();
        }

        private static void ToStringCore(IEnumerable<AutomataNode> nexts, StringBuilder sb, int depth)
        {
            foreach (var item in nexts)
            {
                if (depth != 0) sb.Append(' ', depth * 2);

                sb.Append("[" + item.Key + "]");
                if (item.Value != -1)
                {
                    sb.Append("(" + item.OriginalKey + ")");
                    sb.Append(" = ");
                    sb.Append(item.Value);
                }

                sb.AppendLine();
                ToStringCore(item.YieldChildren(), sb, depth + 1);
            }
        }

        private static IEnumerable<KeyValuePair<string, int>> YieldCore(IEnumerable<AutomataNode> nexts)
        {
            foreach (var item in nexts)
            {
                if (item.Value != -1) yield return new KeyValuePair<string, int>(item.OriginalKey, item.Value);

                foreach (var x in YieldCore(item.YieldChildren())) yield return x;
            }
        }

        /* IL Emit */

#if !NET_STANDARD_2_0

        public void EmitMatch(ILGenerator il, LocalBuilder bytesSpan, LocalBuilder key, Action<KeyValuePair<string, int>> onFound, Action onNotFound)
        {
            root.EmitSearchNext(il, bytesSpan, key, onFound, onNotFound);
        }

#endif

        private class AutomataNode : IComparable<AutomataNode>
        {
            private int count;
            private ulong[] nextKeys;

            private AutomataNode[] nexts;

            public AutomataNode(ulong key)
            {
                Key = key;
                Value = -1;
                nexts = Array.Empty<AutomataNode>();
                nextKeys = Array.Empty<ulong>();
                count = 0;
                OriginalKey = null;
            }

            public bool HasChildren => count != 0;

            public int CompareTo(AutomataNode other)
            {
                return Key.CompareTo(other.Key);
            }

            public AutomataNode Add(ulong key)
            {
                var index = Array.BinarySearch(nextKeys, 0, count, key);
                if (index < 0)
                {
                    if (nexts.Length == count)
                    {
                        Array.Resize(ref nexts, count == 0 ? 4 : count * 2);
                        Array.Resize(ref nextKeys, count == 0 ? 4 : count * 2);
                    }

                    count++;

                    var nextNode = new AutomataNode(key);
                    nexts[count - 1] = nextNode;
                    nextKeys[count - 1] = key;
                    Array.Sort(nexts, 0, count);
                    Array.Sort(nextKeys, 0, count);
                    return nextNode;
                }

                return nexts[index];
            }

            public AutomataNode Add(ulong key, int value, string originalKey)
            {
                var v = Add(key);
                v.Value = value;
                v.OriginalKey = originalKey;
                return v;
            }

            public AutomataNode SearchNext(ref ReadOnlySpan<byte> value)
            {
                var key = AutomataKeyGen.GetKey(ref value);
                if (count < 4)
                {
                    // linear search
                    for (var i = 0; i < count; i++)
                        if (nextKeys[i] == key)
                            return nexts[i];
                }
                else
                {
                    // binary search
                    var index = BinarySearch(nextKeys, 0, count, key);
                    if (index >= 0) return nexts[index];
                }

                return null;
            }

            internal static int BinarySearch(ulong[] array, int index, int length, ulong value)
            {
                var lo = index;
                var hi = index + length - 1;
                while (lo <= hi)
                {
                    var i = lo + ((hi - lo) >> 1);

                    var arrayValue = array[i];
                    int order;
                    if (arrayValue < value)
                        order = -1;
                    else if (arrayValue > value)
                        order = 1;
                    else
                        order = 0;

                    if (order == 0) return i;

                    if (order < 0)
                        lo = i + 1;
                    else
                        hi = i - 1;
                }

                return ~lo;
            }

            public IEnumerable<AutomataNode> YieldChildren()
            {
                for (var i = 0; i < count; i++) yield return nexts[i];
            }
#pragma warning disable SA1401 // Fields should be private
            internal readonly ulong Key;
            internal int Value;
            internal string OriginalKey;
#pragma warning restore SA1401 // Fields should be private

#if !NET_STANDARD_2_0

            // SearchNext(ref ReadOnlySpan<byte> bytes)
            public void EmitSearchNext(ILGenerator il, LocalBuilder bytesSpan, LocalBuilder key, Action<KeyValuePair<string, int>> onFound, Action onNotFound)
            {
                // key = AutomataKeyGen.GetKey(ref bytesSpan);
                il.EmitLdloca(bytesSpan);
                il.EmitCall(AutomataKeyGen.GetKeyMethod);
                il.EmitStloc(key);

                // match children.
                EmitSearchNextCore(il, bytesSpan, key, onFound, onNotFound, nexts, count);
            }

            private static void EmitSearchNextCore(ILGenerator il, LocalBuilder bytesSpan, LocalBuilder key, Action<KeyValuePair<string, int>> onFound, Action onNotFound, AutomataNode[] nexts, int count)
            {
                if (count < 4)
                {
                    // linear-search
                    var valueExists = nexts.Take(count).Where(x => x.Value != -1).ToArray();
                    var childrenExists = nexts.Take(count).Where(x => x.HasChildren).ToArray();
                    var gotoSearchNext = il.DefineLabel();
                    var gotoNotFound = il.DefineLabel();

                    {
                        // bytesSpan.Length
                        il.EmitLdloca(bytesSpan);
                        il.EmitCall(typeof(ReadOnlySpan<byte>).GetRuntimeProperty(nameof(ReadOnlySpan<byte>.Length)).GetMethod);
                        if (childrenExists.Length != 0 && valueExists.Length == 0)
                            il.Emit(OpCodes.Brfalse, gotoNotFound); // if(bytesSpan.Length == 0)
                        else
                            il.Emit(OpCodes.Brtrue, gotoSearchNext); // if(bytesSpan.Length != 0)
                    }

                    {
                        var ifValueNexts = Enumerable.Range(0, Math.Max(valueExists.Length - 1, 0)).Select(_ => il.DefineLabel()).ToArray();
                        for (var i = 0; i < valueExists.Length; i++)
                        {
                            var notFoundLabel = il.DefineLabel();
                            if (i != 0) il.MarkLabel(ifValueNexts[i - 1]);

                            il.EmitLdloc(key);
                            il.EmitULong(valueExists[i].Key);
                            il.Emit(OpCodes.Bne_Un, notFoundLabel);

                            // found
                            onFound(new KeyValuePair<string, int>(valueExists[i].OriginalKey, valueExists[i].Value));

                            // notfound
                            il.MarkLabel(notFoundLabel);
                            if (i != valueExists.Length - 1)
                                il.Emit(OpCodes.Br, ifValueNexts[i]);
                            else
                                onNotFound();
                        }
                    }

                    il.MarkLabel(gotoSearchNext);
                    var ifRecNext = Enumerable.Range(0, Math.Max(childrenExists.Length - 1, 0)).Select(_ => il.DefineLabel()).ToArray();
                    for (var i = 0; i < childrenExists.Length; i++)
                    {
                        var notFoundLabel = il.DefineLabel();
                        if (i != 0) il.MarkLabel(ifRecNext[i - 1]);

                        il.EmitLdloc(key);
                        il.EmitULong(childrenExists[i].Key);
                        il.Emit(OpCodes.Bne_Un, notFoundLabel);

                        // found
                        childrenExists[i].EmitSearchNext(il, bytesSpan, key, onFound, onNotFound);

                        // notfound
                        il.MarkLabel(notFoundLabel);
                        if (i != childrenExists.Length - 1)
                            il.Emit(OpCodes.Br, ifRecNext[i]);
                        else
                            onNotFound();
                    }

                    il.MarkLabel(gotoNotFound);
                    onNotFound();
                }
                else
                {
                    // binary-search
                    var midline = count / 2;
                    var mid = nexts[midline].Key;
                    var l = nexts.Take(count).Take(midline).ToArray();
                    var r = nexts.Take(count).Skip(midline).ToArray();

                    var gotoRight = il.DefineLabel();

                    // if(key < mid)
                    il.EmitLdloc(key);
                    il.EmitULong(mid);
                    il.Emit(OpCodes.Bge_Un, gotoRight);
                    EmitSearchNextCore(il, bytesSpan, key, onFound, onNotFound, l, l.Length);

                    // else
                    il.MarkLabel(gotoRight);
                    EmitSearchNextCore(il, bytesSpan, key, onFound, onNotFound, r, r.Length);
                }
            }

#endif
        }
    }

    /// <remarks>
    ///     This is used by dynamically generated code. It can be made internal after we enable our dynamic assemblies to
    ///     access internals.
    ///     But that trick may require net46, so maybe we should leave this as public.
    /// </remarks>
    public static class AutomataKeyGen
    {
        public static readonly MethodInfo GetKeyMethod = typeof(AutomataKeyGen).GetRuntimeMethod(nameof(GetKey), new[] { typeof(ReadOnlySpan<byte>).MakeByRefType() });

        public static ulong GetKey(ref ReadOnlySpan<byte> span)
        {
            ulong key;

            unchecked
            {
                if (span.Length >= 8)
                {
                    key = SafeBitConverter.ToUInt64(span);
                    span = span.Slice(8);
                }
                else
                {
                    switch (span.Length)
                    {
                        case 1:
                        {
                            key = span[0];
                            span = span.Slice(1);
                            break;
                        }

                        case 2:
                        {
                            key = SafeBitConverter.ToUInt16(span);
                            span = span.Slice(2);
                            break;
                        }

                        case 3:
                        {
                            var a = span[0];
                            var b = SafeBitConverter.ToUInt16(span.Slice(1));
                            key = a | ((ulong)b << 8);
                            span = span.Slice(3);
                            break;
                        }

                        case 4:
                        {
                            key = SafeBitConverter.ToUInt32(span);
                            span = span.Slice(4);
                            break;
                        }

                        case 5:
                        {
                            var a = span[0];
                            var b = SafeBitConverter.ToUInt32(span.Slice(1));
                            key = a | ((ulong)b << 8);
                            span = span.Slice(5);
                            break;
                        }

                        case 6:
                        {
                            ulong a = SafeBitConverter.ToUInt16(span);
                            ulong b = SafeBitConverter.ToUInt32(span.Slice(2));
                            key = a | (b << 16);
                            span = span.Slice(6);
                            break;
                        }

                        case 7:
                        {
                            var a = span[0];
                            var b = SafeBitConverter.ToUInt16(span.Slice(1));
                            var c = SafeBitConverter.ToUInt32(span.Slice(3));
                            key = a | ((ulong)b << 8) | ((ulong)c << 24);
                            span = span.Slice(7);
                            break;
                        }

                        default:
                            throw new MessagePackSerializationException("Not Supported Length");
                    }
                }

                return key;
            }
        }
    }
}