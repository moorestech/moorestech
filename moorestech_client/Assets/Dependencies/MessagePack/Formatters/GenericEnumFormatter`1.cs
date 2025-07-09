// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace MessagePack.Formatters
{
    public sealed class GenericEnumFormatter<T> : IMessagePackFormatter<T>
        where T : Enum
    {
        private readonly EnumDeserialize deserializer;

        private readonly EnumSerialize serializer;

        public GenericEnumFormatter()
        {
            var underlyingType = typeof(T).GetEnumUnderlyingType();
            switch (Type.GetTypeCode(underlyingType))
            {
#pragma warning disable SA1107 // Avoid multiple statements on same line.
                case TypeCode.Byte:
                    serializer = (ref MessagePackWriter writer, ref T value) => writer.Write(Unsafe.As<T, byte>(ref value));
                    deserializer = (ref MessagePackReader reader) =>
                    {
                        var v = reader.ReadByte();
                        return Unsafe.As<byte, T>(ref v);
                    };
                    break;
                case TypeCode.Int16:
                    serializer = (ref MessagePackWriter writer, ref T value) => writer.Write(Unsafe.As<T, short>(ref value));
                    deserializer = (ref MessagePackReader reader) =>
                    {
                        var v = reader.ReadInt16();
                        return Unsafe.As<short, T>(ref v);
                    };
                    break;
                case TypeCode.Int32:
                    serializer = (ref MessagePackWriter writer, ref T value) => writer.Write(Unsafe.As<T, int>(ref value));
                    deserializer = (ref MessagePackReader reader) =>
                    {
                        var v = reader.ReadInt32();
                        return Unsafe.As<int, T>(ref v);
                    };
                    break;
                case TypeCode.Int64:
                    serializer = (ref MessagePackWriter writer, ref T value) => writer.Write(Unsafe.As<T, long>(ref value));
                    deserializer = (ref MessagePackReader reader) =>
                    {
                        var v = reader.ReadInt64();
                        return Unsafe.As<long, T>(ref v);
                    };
                    break;
                case TypeCode.SByte:
                    serializer = (ref MessagePackWriter writer, ref T value) => writer.Write(Unsafe.As<T, sbyte>(ref value));
                    deserializer = (ref MessagePackReader reader) =>
                    {
                        var v = reader.ReadSByte();
                        return Unsafe.As<sbyte, T>(ref v);
                    };
                    break;
                case TypeCode.UInt16:
                    serializer = (ref MessagePackWriter writer, ref T value) => writer.Write(Unsafe.As<T, ushort>(ref value));
                    deserializer = (ref MessagePackReader reader) =>
                    {
                        var v = reader.ReadUInt16();
                        return Unsafe.As<ushort, T>(ref v);
                    };
                    break;
                case TypeCode.UInt32:
                    serializer = (ref MessagePackWriter writer, ref T value) => writer.Write(Unsafe.As<T, uint>(ref value));
                    deserializer = (ref MessagePackReader reader) =>
                    {
                        var v = reader.ReadUInt32();
                        return Unsafe.As<uint, T>(ref v);
                    };
                    break;
                case TypeCode.UInt64:
                    serializer = (ref MessagePackWriter writer, ref T value) => writer.Write(Unsafe.As<T, ulong>(ref value));
                    deserializer = (ref MessagePackReader reader) =>
                    {
                        var v = reader.ReadUInt64();
                        return Unsafe.As<ulong, T>(ref v);
                    };
                    break;
                default:
                    break;
#pragma warning restore SA1107 // Avoid multiple statements on same line.
            }
        }

        public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options)
        {
            serializer(ref writer, ref value);
        }

        public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return deserializer(ref reader);
        }

        private delegate void EnumSerialize(ref MessagePackWriter writer, ref T value);

        private delegate T EnumDeserialize(ref MessagePackReader reader);
    }
}