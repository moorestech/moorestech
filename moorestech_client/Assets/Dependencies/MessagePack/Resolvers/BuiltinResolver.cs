// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using MessagePack.Formatters;
using MessagePack.Internal;

#pragma warning disable SA1403 // File may only contain a single namespace
#pragma warning disable SA1509 // Opening braces should not be preceded by blank line

namespace MessagePack.Resolvers
{
    public sealed class BuiltinResolver : IFormatterResolver
    {
        /// <summary>
        ///     The singleton instance that can be used.
        /// </summary>
        public static readonly BuiltinResolver Instance = new();

        private BuiltinResolver()
        {
        }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            return FormatterCache<T>.Formatter;
        }

        private static class FormatterCache<T>
        {
            public static readonly IMessagePackFormatter<T> Formatter;

            static FormatterCache()
            {
                // Reduce IL2CPP code generate size(don't write long code in <T>)
                Formatter = (IMessagePackFormatter<T>)BuiltinResolverGetFormatterHelper.GetFormatter(typeof(T));
            }
        }
    }
}

namespace MessagePack.Internal
{
    internal static class BuiltinResolverGetFormatterHelper
    {
        private static readonly Dictionary<Type, object> FormatterMap = new()
        {
            // Primitive
            { typeof(short), Int16Formatter.Instance },
            { typeof(int), Int32Formatter.Instance },
            { typeof(long), Int64Formatter.Instance },
            { typeof(ushort), UInt16Formatter.Instance },
            { typeof(uint), UInt32Formatter.Instance },
            { typeof(ulong), UInt64Formatter.Instance },
            { typeof(float), SingleFormatter.Instance },
            { typeof(double), DoubleFormatter.Instance },
            { typeof(bool), BooleanFormatter.Instance },
            { typeof(byte), ByteFormatter.Instance },
            { typeof(sbyte), SByteFormatter.Instance },
            { typeof(DateTime), DateTimeFormatter.Instance },
            { typeof(char), CharFormatter.Instance },

            // Nulllable Primitive
            { typeof(short?), NullableInt16Formatter.Instance },
            { typeof(int?), NullableInt32Formatter.Instance },
            { typeof(long?), NullableInt64Formatter.Instance },
            { typeof(ushort?), NullableUInt16Formatter.Instance },
            { typeof(uint?), NullableUInt32Formatter.Instance },
            { typeof(ulong?), NullableUInt64Formatter.Instance },
            { typeof(float?), NullableSingleFormatter.Instance },
            { typeof(double?), NullableDoubleFormatter.Instance },
            { typeof(bool?), NullableBooleanFormatter.Instance },
            { typeof(byte?), NullableByteFormatter.Instance },
            { typeof(sbyte?), NullableSByteFormatter.Instance },
            { typeof(DateTime?), NullableDateTimeFormatter.Instance },
            { typeof(char?), NullableCharFormatter.Instance },

            // StandardClassLibraryFormatter
            { typeof(string), NullableStringFormatter.Instance },
            { typeof(decimal), DecimalFormatter.Instance },
            { typeof(decimal?), new StaticNullableFormatter<decimal>(DecimalFormatter.Instance) },
            { typeof(TimeSpan), TimeSpanFormatter.Instance },
            { typeof(TimeSpan?), new StaticNullableFormatter<TimeSpan>(TimeSpanFormatter.Instance) },
            { typeof(DateTimeOffset), DateTimeOffsetFormatter.Instance },
            { typeof(DateTimeOffset?), new StaticNullableFormatter<DateTimeOffset>(DateTimeOffsetFormatter.Instance) },
            { typeof(Guid), GuidFormatter.Instance },
            { typeof(Guid?), new StaticNullableFormatter<Guid>(GuidFormatter.Instance) },
            { typeof(Uri), UriFormatter.Instance },
            { typeof(Version), VersionFormatter.Instance },
            { typeof(StringBuilder), StringBuilderFormatter.Instance },
            { typeof(BitArray), BitArrayFormatter.Instance },
            { typeof(Type), TypeFormatter<Type>.Instance },

            // special primitive
            { typeof(byte[]), ByteArrayFormatter.Instance },

            // Nil
            { typeof(Nil), NilFormatter.Instance },
            { typeof(Nil?), NullableNilFormatter.Instance },

            // optimized primitive array formatter
            { typeof(short[]), Int16ArrayFormatter.Instance },
            { typeof(int[]), Int32ArrayFormatter.Instance },
            { typeof(long[]), Int64ArrayFormatter.Instance },
            { typeof(ushort[]), UInt16ArrayFormatter.Instance },
            { typeof(uint[]), UInt32ArrayFormatter.Instance },
            { typeof(ulong[]), UInt64ArrayFormatter.Instance },
            { typeof(float[]), SingleArrayFormatter.Instance },
            { typeof(double[]), DoubleArrayFormatter.Instance },
            { typeof(bool[]), BooleanArrayFormatter.Instance },
            { typeof(sbyte[]), SByteArrayFormatter.Instance },
            { typeof(DateTime[]), DateTimeArrayFormatter.Instance },
            { typeof(char[]), CharArrayFormatter.Instance },
            { typeof(string[]), NullableStringArrayFormatter.Instance },

            // well known collections
            { typeof(List<short>), new ListFormatter<short>() },
            { typeof(List<int>), new ListFormatter<int>() },
            { typeof(List<long>), new ListFormatter<long>() },
            { typeof(List<ushort>), new ListFormatter<ushort>() },
            { typeof(List<uint>), new ListFormatter<uint>() },
            { typeof(List<ulong>), new ListFormatter<ulong>() },
            { typeof(List<float>), new ListFormatter<float>() },
            { typeof(List<double>), new ListFormatter<double>() },
            { typeof(List<bool>), new ListFormatter<bool>() },
            { typeof(List<byte>), new ListFormatter<byte>() },
            { typeof(List<sbyte>), new ListFormatter<sbyte>() },
            { typeof(List<DateTime>), new ListFormatter<DateTime>() },
            { typeof(List<char>), new ListFormatter<char>() },
            { typeof(List<string>), new ListFormatter<string>() },

            { typeof(object[]), new ArrayFormatter<object>() },
            { typeof(List<object>), new ListFormatter<object>() },

            { typeof(Memory<byte>), ByteMemoryFormatter.Instance },
            { typeof(Memory<byte>?), new StaticNullableFormatter<Memory<byte>>(ByteMemoryFormatter.Instance) },
            { typeof(ReadOnlyMemory<byte>), ByteReadOnlyMemoryFormatter.Instance },
            { typeof(ReadOnlyMemory<byte>?), new StaticNullableFormatter<ReadOnlyMemory<byte>>(ByteReadOnlyMemoryFormatter.Instance) },
            { typeof(ReadOnlySequence<byte>), ByteReadOnlySequenceFormatter.Instance },
            { typeof(ReadOnlySequence<byte>?), new StaticNullableFormatter<ReadOnlySequence<byte>>(ByteReadOnlySequenceFormatter.Instance) },
            { typeof(ArraySegment<byte>), ByteArraySegmentFormatter.Instance },
            { typeof(ArraySegment<byte>?), new StaticNullableFormatter<ArraySegment<byte>>(ByteArraySegmentFormatter.Instance) },

            { typeof(BigInteger), BigIntegerFormatter.Instance },
            { typeof(BigInteger?), new StaticNullableFormatter<BigInteger>(BigIntegerFormatter.Instance) },
            { typeof(Complex), ComplexFormatter.Instance },
            { typeof(Complex?), new StaticNullableFormatter<Complex>(ComplexFormatter.Instance) },

#if NET5_0_OR_GREATER
            { typeof(System.Half), HalfFormatter.Instance },
#endif
        };

        internal static object GetFormatter(Type t)
        {
            object formatter;
            if (FormatterMap.TryGetValue(t, out formatter)) return formatter;

            if (typeof(Type).IsAssignableFrom(t)) return typeof(TypeFormatter<>).MakeGenericType(t).GetField(nameof(TypeFormatter<Type>.Instance)).GetValue(null);

            return null;
        }
    }
}