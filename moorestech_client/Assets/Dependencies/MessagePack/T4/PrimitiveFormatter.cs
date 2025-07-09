// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/* THIS (.cs) FILE IS GENERATED. DO NOT CHANGE IT.
 * CHANGE THE .tt FILE INSTEAD. */

using System;

#pragma warning disable SA1649 // File name should match first type name

namespace MessagePack.Formatters
{
    public sealed class Int16Formatter : IMessagePackFormatter<short>
    {
        public static readonly Int16Formatter Instance = new();

        private Int16Formatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, short value, MessagePackSerializerOptions options)
        {
            writer.Write(value);
        }

        public short Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadInt16();
        }
    }

    public sealed class NullableInt16Formatter : IMessagePackFormatter<short?>
    {
        public static readonly NullableInt16Formatter Instance = new();

        private NullableInt16Formatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, short? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.Write(value.Value);
        }

        public short? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadInt16();
        }
    }

    public sealed class Int16ArrayFormatter : IMessagePackFormatter<short[]>
    {
        public static readonly Int16ArrayFormatter Instance = new();

        private Int16ArrayFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, short[] value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
            }
            else
            {
                writer.WriteArrayHeader(value.Length);
                for (var i = 0; i < value.Length; i++) writer.Write(value[i]);
            }
        }

        public short[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<short>();

            var array = new short[len];
            for (var i = 0; i < array.Length; i++) array[i] = reader.ReadInt16();

            return array;
        }
    }

    public sealed class Int32Formatter : IMessagePackFormatter<int>
    {
        public static readonly Int32Formatter Instance = new();

        private Int32Formatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, int value, MessagePackSerializerOptions options)
        {
            writer.Write(value);
        }

        public int Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadInt32();
        }
    }

    public sealed class NullableInt32Formatter : IMessagePackFormatter<int?>
    {
        public static readonly NullableInt32Formatter Instance = new();

        private NullableInt32Formatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, int? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.Write(value.Value);
        }

        public int? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadInt32();
        }
    }

    public sealed class Int32ArrayFormatter : IMessagePackFormatter<int[]>
    {
        public static readonly Int32ArrayFormatter Instance = new();

        private Int32ArrayFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, int[] value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
            }
            else
            {
                writer.WriteArrayHeader(value.Length);
                for (var i = 0; i < value.Length; i++) writer.Write(value[i]);
            }
        }

        public int[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<int>();

            var array = new int[len];
            for (var i = 0; i < array.Length; i++) array[i] = reader.ReadInt32();

            return array;
        }
    }

    public sealed class Int64Formatter : IMessagePackFormatter<long>
    {
        public static readonly Int64Formatter Instance = new();

        private Int64Formatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, long value, MessagePackSerializerOptions options)
        {
            writer.Write(value);
        }

        public long Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadInt64();
        }
    }

    public sealed class NullableInt64Formatter : IMessagePackFormatter<long?>
    {
        public static readonly NullableInt64Formatter Instance = new();

        private NullableInt64Formatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, long? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.Write(value.Value);
        }

        public long? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadInt64();
        }
    }

    public sealed class Int64ArrayFormatter : IMessagePackFormatter<long[]>
    {
        public static readonly Int64ArrayFormatter Instance = new();

        private Int64ArrayFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, long[] value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
            }
            else
            {
                writer.WriteArrayHeader(value.Length);
                for (var i = 0; i < value.Length; i++) writer.Write(value[i]);
            }
        }

        public long[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<long>();

            var array = new long[len];
            for (var i = 0; i < array.Length; i++) array[i] = reader.ReadInt64();

            return array;
        }
    }

    public sealed class UInt16Formatter : IMessagePackFormatter<ushort>
    {
        public static readonly UInt16Formatter Instance = new();

        private UInt16Formatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, ushort value, MessagePackSerializerOptions options)
        {
            writer.Write(value);
        }

        public ushort Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadUInt16();
        }
    }

    public sealed class NullableUInt16Formatter : IMessagePackFormatter<ushort?>
    {
        public static readonly NullableUInt16Formatter Instance = new();

        private NullableUInt16Formatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, ushort? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.Write(value.Value);
        }

        public ushort? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadUInt16();
        }
    }

    public sealed class UInt16ArrayFormatter : IMessagePackFormatter<ushort[]>
    {
        public static readonly UInt16ArrayFormatter Instance = new();

        private UInt16ArrayFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, ushort[] value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
            }
            else
            {
                writer.WriteArrayHeader(value.Length);
                for (var i = 0; i < value.Length; i++) writer.Write(value[i]);
            }
        }

        public ushort[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<ushort>();

            var array = new ushort[len];
            for (var i = 0; i < array.Length; i++) array[i] = reader.ReadUInt16();

            return array;
        }
    }

    public sealed class UInt32Formatter : IMessagePackFormatter<uint>
    {
        public static readonly UInt32Formatter Instance = new();

        private UInt32Formatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, uint value, MessagePackSerializerOptions options)
        {
            writer.Write(value);
        }

        public uint Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadUInt32();
        }
    }

    public sealed class NullableUInt32Formatter : IMessagePackFormatter<uint?>
    {
        public static readonly NullableUInt32Formatter Instance = new();

        private NullableUInt32Formatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, uint? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.Write(value.Value);
        }

        public uint? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadUInt32();
        }
    }

    public sealed class UInt32ArrayFormatter : IMessagePackFormatter<uint[]>
    {
        public static readonly UInt32ArrayFormatter Instance = new();

        private UInt32ArrayFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, uint[] value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
            }
            else
            {
                writer.WriteArrayHeader(value.Length);
                for (var i = 0; i < value.Length; i++) writer.Write(value[i]);
            }
        }

        public uint[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<uint>();

            var array = new uint[len];
            for (var i = 0; i < array.Length; i++) array[i] = reader.ReadUInt32();

            return array;
        }
    }

    public sealed class UInt64Formatter : IMessagePackFormatter<ulong>
    {
        public static readonly UInt64Formatter Instance = new();

        private UInt64Formatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, ulong value, MessagePackSerializerOptions options)
        {
            writer.Write(value);
        }

        public ulong Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadUInt64();
        }
    }

    public sealed class NullableUInt64Formatter : IMessagePackFormatter<ulong?>
    {
        public static readonly NullableUInt64Formatter Instance = new();

        private NullableUInt64Formatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, ulong? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.Write(value.Value);
        }

        public ulong? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadUInt64();
        }
    }

    public sealed class UInt64ArrayFormatter : IMessagePackFormatter<ulong[]>
    {
        public static readonly UInt64ArrayFormatter Instance = new();

        private UInt64ArrayFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, ulong[] value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
            }
            else
            {
                writer.WriteArrayHeader(value.Length);
                for (var i = 0; i < value.Length; i++) writer.Write(value[i]);
            }
        }

        public ulong[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<ulong>();

            var array = new ulong[len];
            for (var i = 0; i < array.Length; i++) array[i] = reader.ReadUInt64();

            return array;
        }
    }

    public sealed class SingleFormatter : IMessagePackFormatter<float>
    {
        public static readonly SingleFormatter Instance = new();

        private SingleFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, float value, MessagePackSerializerOptions options)
        {
            writer.Write(value);
        }

        public float Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadSingle();
        }
    }

    public sealed class NullableSingleFormatter : IMessagePackFormatter<float?>
    {
        public static readonly NullableSingleFormatter Instance = new();

        private NullableSingleFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, float? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.Write(value.Value);
        }

        public float? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadSingle();
        }
    }

    public sealed class SingleArrayFormatter : IMessagePackFormatter<float[]>
    {
        public static readonly SingleArrayFormatter Instance = new();

        private SingleArrayFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, float[] value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
            }
            else
            {
                writer.WriteArrayHeader(value.Length);
                for (var i = 0; i < value.Length; i++) writer.Write(value[i]);
            }
        }

        public float[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<float>();

            var array = new float[len];
            for (var i = 0; i < array.Length; i++) array[i] = reader.ReadSingle();

            return array;
        }
    }

    public sealed class DoubleFormatter : IMessagePackFormatter<double>
    {
        public static readonly DoubleFormatter Instance = new();

        private DoubleFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, double value, MessagePackSerializerOptions options)
        {
            writer.Write(value);
        }

        public double Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadDouble();
        }
    }

    public sealed class NullableDoubleFormatter : IMessagePackFormatter<double?>
    {
        public static readonly NullableDoubleFormatter Instance = new();

        private NullableDoubleFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, double? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.Write(value.Value);
        }

        public double? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadDouble();
        }
    }

    public sealed class DoubleArrayFormatter : IMessagePackFormatter<double[]>
    {
        public static readonly DoubleArrayFormatter Instance = new();

        private DoubleArrayFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, double[] value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
            }
            else
            {
                writer.WriteArrayHeader(value.Length);
                for (var i = 0; i < value.Length; i++) writer.Write(value[i]);
            }
        }

        public double[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<double>();

            var array = new double[len];
            for (var i = 0; i < array.Length; i++) array[i] = reader.ReadDouble();

            return array;
        }
    }

    public sealed class BooleanFormatter : IMessagePackFormatter<bool>
    {
        public static readonly BooleanFormatter Instance = new();

        private BooleanFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, bool value, MessagePackSerializerOptions options)
        {
            writer.Write(value);
        }

        public bool Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadBoolean();
        }
    }

    public sealed class NullableBooleanFormatter : IMessagePackFormatter<bool?>
    {
        public static readonly NullableBooleanFormatter Instance = new();

        private NullableBooleanFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, bool? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.Write(value.Value);
        }

        public bool? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadBoolean();
        }
    }

    public sealed class BooleanArrayFormatter : IMessagePackFormatter<bool[]>
    {
        public static readonly BooleanArrayFormatter Instance = new();

        private BooleanArrayFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, bool[] value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
            }
            else
            {
                writer.WriteArrayHeader(value.Length);
                for (var i = 0; i < value.Length; i++) writer.Write(value[i]);
            }
        }

        public bool[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<bool>();

            var array = new bool[len];
            for (var i = 0; i < array.Length; i++) array[i] = reader.ReadBoolean();

            return array;
        }
    }

    public sealed class ByteFormatter : IMessagePackFormatter<byte>
    {
        public static readonly ByteFormatter Instance = new();

        private ByteFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, byte value, MessagePackSerializerOptions options)
        {
            writer.Write(value);
        }

        public byte Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadByte();
        }
    }

    public sealed class NullableByteFormatter : IMessagePackFormatter<byte?>
    {
        public static readonly NullableByteFormatter Instance = new();

        private NullableByteFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, byte? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.Write(value.Value);
        }

        public byte? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadByte();
        }
    }

    public sealed class SByteFormatter : IMessagePackFormatter<sbyte>
    {
        public static readonly SByteFormatter Instance = new();

        private SByteFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, sbyte value, MessagePackSerializerOptions options)
        {
            writer.Write(value);
        }

        public sbyte Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadSByte();
        }
    }

    public sealed class NullableSByteFormatter : IMessagePackFormatter<sbyte?>
    {
        public static readonly NullableSByteFormatter Instance = new();

        private NullableSByteFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, sbyte? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.Write(value.Value);
        }

        public sbyte? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadSByte();
        }
    }

    public sealed class SByteArrayFormatter : IMessagePackFormatter<sbyte[]>
    {
        public static readonly SByteArrayFormatter Instance = new();

        private SByteArrayFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, sbyte[] value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
            }
            else
            {
                writer.WriteArrayHeader(value.Length);
                for (var i = 0; i < value.Length; i++) writer.Write(value[i]);
            }
        }

        public sbyte[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<sbyte>();

            var array = new sbyte[len];
            for (var i = 0; i < array.Length; i++) array[i] = reader.ReadSByte();

            return array;
        }
    }

    public sealed class CharFormatter : IMessagePackFormatter<char>
    {
        public static readonly CharFormatter Instance = new();

        private CharFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, char value, MessagePackSerializerOptions options)
        {
            writer.Write(value);
        }

        public char Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadChar();
        }
    }

    public sealed class NullableCharFormatter : IMessagePackFormatter<char?>
    {
        public static readonly NullableCharFormatter Instance = new();

        private NullableCharFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, char? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.Write(value.Value);
        }

        public char? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadChar();
        }
    }

    public sealed class CharArrayFormatter : IMessagePackFormatter<char[]>
    {
        public static readonly CharArrayFormatter Instance = new();

        private CharArrayFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, char[] value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
            }
            else
            {
                writer.WriteArrayHeader(value.Length);
                for (var i = 0; i < value.Length; i++) writer.Write(value[i]);
            }
        }

        public char[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<char>();

            var array = new char[len];
            for (var i = 0; i < array.Length; i++) array[i] = reader.ReadChar();

            return array;
        }
    }

    public sealed class DateTimeFormatter : IMessagePackFormatter<DateTime>
    {
        public static readonly DateTimeFormatter Instance = new();

        private DateTimeFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, DateTime value, MessagePackSerializerOptions options)
        {
            writer.Write(value);
        }

        public DateTime Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadDateTime();
        }
    }

    public sealed class NullableDateTimeFormatter : IMessagePackFormatter<DateTime?>
    {
        public static readonly NullableDateTimeFormatter Instance = new();

        private NullableDateTimeFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, DateTime? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.Write(value.Value);
        }

        public DateTime? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadDateTime();
        }
    }

    public sealed class DateTimeArrayFormatter : IMessagePackFormatter<DateTime[]>
    {
        public static readonly DateTimeArrayFormatter Instance = new();

        private DateTimeArrayFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, DateTime[] value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
            }
            else
            {
                writer.WriteArrayHeader(value.Length);
                for (var i = 0; i < value.Length; i++) writer.Write(value[i]);
            }
        }

        public DateTime[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<DateTime>();

            var array = new DateTime[len];
            for (var i = 0; i < array.Length; i++) array[i] = reader.ReadDateTime();

            return array;
        }
    }
}