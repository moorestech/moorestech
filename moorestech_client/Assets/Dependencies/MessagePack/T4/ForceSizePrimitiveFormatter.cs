// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/* THIS (.cs) FILE IS GENERATED. DO NOT CHANGE IT.
 * CHANGE THE .tt FILE INSTEAD. */

using System;

#pragma warning disable SA1649 // File name should match first type name

namespace MessagePack.Formatters
{
    public sealed class ForceInt16BlockFormatter : IMessagePackFormatter<short>
    {
        public static readonly ForceInt16BlockFormatter Instance = new();

        private ForceInt16BlockFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, short value, MessagePackSerializerOptions options)
        {
            writer.WriteInt16(value);
        }

        public short Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadInt16();
        }
    }

    public sealed class NullableForceInt16BlockFormatter : IMessagePackFormatter<short?>
    {
        public static readonly NullableForceInt16BlockFormatter Instance = new();

        private NullableForceInt16BlockFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, short? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.WriteInt16(value.Value);
        }

        public short? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadInt16();
        }
    }

    public sealed class ForceInt16BlockArrayFormatter : IMessagePackFormatter<short[]>
    {
        public static readonly ForceInt16BlockArrayFormatter Instance = new();

        private ForceInt16BlockArrayFormatter()
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
                for (var i = 0; i < value.Length; i++) writer.WriteInt16(value[i]);
            }
        }

        public short[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<short>();

            var array = new short[len];
            options.Security.DepthStep(ref reader);
            try
            {
                for (var i = 0; i < array.Length; i++) array[i] = reader.ReadInt16();
            }
            finally
            {
                reader.Depth--;
            }

            return array;
        }
    }

    public sealed class ForceInt32BlockFormatter : IMessagePackFormatter<int>
    {
        public static readonly ForceInt32BlockFormatter Instance = new();

        private ForceInt32BlockFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, int value, MessagePackSerializerOptions options)
        {
            writer.WriteInt32(value);
        }

        public int Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadInt32();
        }
    }

    public sealed class NullableForceInt32BlockFormatter : IMessagePackFormatter<int?>
    {
        public static readonly NullableForceInt32BlockFormatter Instance = new();

        private NullableForceInt32BlockFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, int? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.WriteInt32(value.Value);
        }

        public int? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadInt32();
        }
    }

    public sealed class ForceInt32BlockArrayFormatter : IMessagePackFormatter<int[]>
    {
        public static readonly ForceInt32BlockArrayFormatter Instance = new();

        private ForceInt32BlockArrayFormatter()
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
                for (var i = 0; i < value.Length; i++) writer.WriteInt32(value[i]);
            }
        }

        public int[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<int>();

            var array = new int[len];
            options.Security.DepthStep(ref reader);
            try
            {
                for (var i = 0; i < array.Length; i++) array[i] = reader.ReadInt32();
            }
            finally
            {
                reader.Depth--;
            }

            return array;
        }
    }

    public sealed class ForceInt64BlockFormatter : IMessagePackFormatter<long>
    {
        public static readonly ForceInt64BlockFormatter Instance = new();

        private ForceInt64BlockFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, long value, MessagePackSerializerOptions options)
        {
            writer.WriteInt64(value);
        }

        public long Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadInt64();
        }
    }

    public sealed class NullableForceInt64BlockFormatter : IMessagePackFormatter<long?>
    {
        public static readonly NullableForceInt64BlockFormatter Instance = new();

        private NullableForceInt64BlockFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, long? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.WriteInt64(value.Value);
        }

        public long? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadInt64();
        }
    }

    public sealed class ForceInt64BlockArrayFormatter : IMessagePackFormatter<long[]>
    {
        public static readonly ForceInt64BlockArrayFormatter Instance = new();

        private ForceInt64BlockArrayFormatter()
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
                for (var i = 0; i < value.Length; i++) writer.WriteInt64(value[i]);
            }
        }

        public long[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<long>();

            var array = new long[len];
            options.Security.DepthStep(ref reader);
            try
            {
                for (var i = 0; i < array.Length; i++) array[i] = reader.ReadInt64();
            }
            finally
            {
                reader.Depth--;
            }

            return array;
        }
    }

    public sealed class ForceUInt16BlockFormatter : IMessagePackFormatter<ushort>
    {
        public static readonly ForceUInt16BlockFormatter Instance = new();

        private ForceUInt16BlockFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, ushort value, MessagePackSerializerOptions options)
        {
            writer.WriteUInt16(value);
        }

        public ushort Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadUInt16();
        }
    }

    public sealed class NullableForceUInt16BlockFormatter : IMessagePackFormatter<ushort?>
    {
        public static readonly NullableForceUInt16BlockFormatter Instance = new();

        private NullableForceUInt16BlockFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, ushort? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.WriteUInt16(value.Value);
        }

        public ushort? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadUInt16();
        }
    }

    public sealed class ForceUInt16BlockArrayFormatter : IMessagePackFormatter<ushort[]>
    {
        public static readonly ForceUInt16BlockArrayFormatter Instance = new();

        private ForceUInt16BlockArrayFormatter()
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
                for (var i = 0; i < value.Length; i++) writer.WriteUInt16(value[i]);
            }
        }

        public ushort[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<ushort>();

            var array = new ushort[len];
            options.Security.DepthStep(ref reader);
            try
            {
                for (var i = 0; i < array.Length; i++) array[i] = reader.ReadUInt16();
            }
            finally
            {
                reader.Depth--;
            }

            return array;
        }
    }

    public sealed class ForceUInt32BlockFormatter : IMessagePackFormatter<uint>
    {
        public static readonly ForceUInt32BlockFormatter Instance = new();

        private ForceUInt32BlockFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, uint value, MessagePackSerializerOptions options)
        {
            writer.WriteUInt32(value);
        }

        public uint Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadUInt32();
        }
    }

    public sealed class NullableForceUInt32BlockFormatter : IMessagePackFormatter<uint?>
    {
        public static readonly NullableForceUInt32BlockFormatter Instance = new();

        private NullableForceUInt32BlockFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, uint? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.WriteUInt32(value.Value);
        }

        public uint? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadUInt32();
        }
    }

    public sealed class ForceUInt32BlockArrayFormatter : IMessagePackFormatter<uint[]>
    {
        public static readonly ForceUInt32BlockArrayFormatter Instance = new();

        private ForceUInt32BlockArrayFormatter()
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
                for (var i = 0; i < value.Length; i++) writer.WriteUInt32(value[i]);
            }
        }

        public uint[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<uint>();

            var array = new uint[len];
            options.Security.DepthStep(ref reader);
            try
            {
                for (var i = 0; i < array.Length; i++) array[i] = reader.ReadUInt32();
            }
            finally
            {
                reader.Depth--;
            }

            return array;
        }
    }

    public sealed class ForceUInt64BlockFormatter : IMessagePackFormatter<ulong>
    {
        public static readonly ForceUInt64BlockFormatter Instance = new();

        private ForceUInt64BlockFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, ulong value, MessagePackSerializerOptions options)
        {
            writer.WriteUInt64(value);
        }

        public ulong Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadUInt64();
        }
    }

    public sealed class NullableForceUInt64BlockFormatter : IMessagePackFormatter<ulong?>
    {
        public static readonly NullableForceUInt64BlockFormatter Instance = new();

        private NullableForceUInt64BlockFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, ulong? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.WriteUInt64(value.Value);
        }

        public ulong? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadUInt64();
        }
    }

    public sealed class ForceUInt64BlockArrayFormatter : IMessagePackFormatter<ulong[]>
    {
        public static readonly ForceUInt64BlockArrayFormatter Instance = new();

        private ForceUInt64BlockArrayFormatter()
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
                for (var i = 0; i < value.Length; i++) writer.WriteUInt64(value[i]);
            }
        }

        public ulong[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<ulong>();

            var array = new ulong[len];
            options.Security.DepthStep(ref reader);
            try
            {
                for (var i = 0; i < array.Length; i++) array[i] = reader.ReadUInt64();
            }
            finally
            {
                reader.Depth--;
            }

            return array;
        }
    }

    public sealed class ForceByteBlockFormatter : IMessagePackFormatter<byte>
    {
        public static readonly ForceByteBlockFormatter Instance = new();

        private ForceByteBlockFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, byte value, MessagePackSerializerOptions options)
        {
            writer.WriteUInt8(value);
        }

        public byte Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadByte();
        }
    }

    public sealed class NullableForceByteBlockFormatter : IMessagePackFormatter<byte?>
    {
        public static readonly NullableForceByteBlockFormatter Instance = new();

        private NullableForceByteBlockFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, byte? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.WriteUInt8(value.Value);
        }

        public byte? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadByte();
        }
    }

    public sealed class ForceSByteBlockFormatter : IMessagePackFormatter<sbyte>
    {
        public static readonly ForceSByteBlockFormatter Instance = new();

        private ForceSByteBlockFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, sbyte value, MessagePackSerializerOptions options)
        {
            writer.WriteInt8(value);
        }

        public sbyte Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return reader.ReadSByte();
        }
    }

    public sealed class NullableForceSByteBlockFormatter : IMessagePackFormatter<sbyte?>
    {
        public static readonly NullableForceSByteBlockFormatter Instance = new();

        private NullableForceSByteBlockFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, sbyte? value, MessagePackSerializerOptions options)
        {
            if (value == null)
                writer.WriteNil();
            else
                writer.WriteInt8(value.Value);
        }

        public sbyte? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return default;
            return reader.ReadSByte();
        }
    }

    public sealed class ForceSByteBlockArrayFormatter : IMessagePackFormatter<sbyte[]>
    {
        public static readonly ForceSByteBlockArrayFormatter Instance = new();

        private ForceSByteBlockArrayFormatter()
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
                for (var i = 0; i < value.Length; i++) writer.WriteInt8(value[i]);
            }
        }

        public sbyte[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return default;

            var len = reader.ReadArrayHeader();
            if (len == 0) return Array.Empty<sbyte>();

            var array = new sbyte[len];
            options.Security.DepthStep(ref reader);
            try
            {
                for (var i = 0; i < array.Length; i++) array[i] = reader.ReadSByte();
            }
            finally
            {
                reader.Depth--;
            }

            return array;
        }
    }
}