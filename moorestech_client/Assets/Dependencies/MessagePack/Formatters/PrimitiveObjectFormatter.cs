// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace MessagePack.Formatters
{
    public class PrimitiveObjectFormatter : IMessagePackFormatter<object>
    {
        public static readonly IMessagePackFormatter<object> Instance = new PrimitiveObjectFormatter();

        private static readonly Dictionary<Type, int> TypeToJumpCode = new()
        {
            // When adding types whose size exceeds 32-bits, add support in MessagePackSecurity.GetHashCollisionResistantEqualityComparer<T>()
            { typeof(bool), 0 },
            { typeof(char), 1 },
            { typeof(sbyte), 2 },
            { typeof(byte), 3 },
            { typeof(short), 4 },
            { typeof(ushort), 5 },
            { typeof(int), 6 },
            { typeof(uint), 7 },
            { typeof(long), 8 },
            { typeof(ulong), 9 },
            { typeof(float), 10 },
            { typeof(double), 11 },
            { typeof(DateTime), 12 },
            { typeof(string), 13 },
            { typeof(byte[]), 14 }
        };

        protected PrimitiveObjectFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, object value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            var t = value.GetType();

            int code;
            if (TypeToJumpCode.TryGetValue(t, out code))
                switch (code)
                {
                    case 0:
                        writer.Write((bool)value);
                        return;
                    case 1:
                        writer.Write((char)value);
                        return;
                    case 2:
                        writer.WriteInt8((sbyte)value);
                        return;
                    case 3:
                        writer.WriteUInt8((byte)value);
                        return;
                    case 4:
                        writer.WriteInt16((short)value);
                        return;
                    case 5:
                        writer.WriteUInt16((ushort)value);
                        return;
                    case 6:
                        writer.WriteInt32((int)value);
                        return;
                    case 7:
                        writer.WriteUInt32((uint)value);
                        return;
                    case 8:
                        writer.WriteInt64((long)value);
                        return;
                    case 9:
                        writer.WriteUInt64((ulong)value);
                        return;
                    case 10:
                        writer.Write((float)value);
                        return;
                    case 11:
                        writer.Write((double)value);
                        return;
                    case 12:
                        writer.Write((DateTime)value);
                        return;
                    case 13:
                        writer.Write((string)value);
                        return;
                    case 14:
                        writer.Write((byte[])value);
                        return;
                    default:
                        throw new MessagePackSerializationException("Not supported primitive object resolver. type:" + t.Name);
                }
#if UNITY_2018_3_OR_NEWER && !NETFX_CORE
            if (t.IsEnum)
#else
                if (t.GetTypeInfo().IsEnum)
#endif
            {
                var underlyingType = Enum.GetUnderlyingType(t);
                var code2 = TypeToJumpCode[underlyingType];
                switch (code2)
                {
                    case 2:
                        writer.WriteInt8((sbyte)value);
                        return;
                    case 3:
                        writer.WriteUInt8((byte)value);
                        return;
                    case 4:
                        writer.WriteInt16((short)value);
                        return;
                    case 5:
                        writer.WriteUInt16((ushort)value);
                        return;
                    case 6:
                        writer.WriteInt32((int)value);
                        return;
                    case 7:
                        writer.WriteUInt32((uint)value);
                        return;
                    case 8:
                        writer.WriteInt64((long)value);
                        return;
                    case 9:
                        writer.WriteUInt64((ulong)value);
                        return;
                }
            }
            else if (value is IDictionary)
            {
                // check IDictionary first
                var d = value as IDictionary;
                writer.WriteMapHeader(d.Count);
                foreach (DictionaryEntry item in d)
                {
                    Serialize(ref writer, item.Key, options);
                    Serialize(ref writer, item.Value, options);
                }

                return;
            }
            else if (value is ICollection)
            {
                var c = value as ICollection;
                writer.WriteArrayHeader(c.Count);
                foreach (var item in c) Serialize(ref writer, item, options);

                return;
            }

            throw new MessagePackSerializationException("Not supported primitive object resolver. type:" + t.Name);
        }

        public object Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var type = reader.NextMessagePackType;
            var resolver = options.Resolver;
            switch (type)
            {
                case MessagePackType.Integer:
                    var code = reader.NextCode;
                    if (code >= MessagePackCode.MinNegativeFixInt && code <= MessagePackCode.MaxNegativeFixInt)
                        return reader.ReadSByte();
                    if (code >= MessagePackCode.MinFixInt && code <= MessagePackCode.MaxFixInt)
                        return reader.ReadByte();
                    if (code == MessagePackCode.Int8)
                        return reader.ReadSByte();
                    if (code == MessagePackCode.Int16)
                        return reader.ReadInt16();
                    if (code == MessagePackCode.Int32)
                        return reader.ReadInt32();
                    if (code == MessagePackCode.Int64)
                        return reader.ReadInt64();
                    if (code == MessagePackCode.UInt8)
                        return reader.ReadByte();
                    if (code == MessagePackCode.UInt16)
                        return reader.ReadUInt16();
                    if (code == MessagePackCode.UInt32)
                        return reader.ReadUInt32();
                    if (code == MessagePackCode.UInt64) return reader.ReadUInt64();

                    throw new MessagePackSerializationException("Invalid primitive bytes.");
                case MessagePackType.Boolean:
                    return reader.ReadBoolean();
                case MessagePackType.Float:
                    if (reader.NextCode == MessagePackCode.Float32)
                        return reader.ReadSingle();
                    return reader.ReadDouble();

                case MessagePackType.String:
                    return reader.ReadString();
                case MessagePackType.Binary:
                    // We must copy the sequence returned by ReadBytes since the reader's sequence is only valid during deserialization.
                    return reader.ReadBytes()?.ToArray();
                case MessagePackType.Extension:
                    var ext = reader.ReadExtensionFormatHeader();
                    if (ext.TypeCode == ReservedMessagePackExtensionTypeCode.DateTime) return reader.ReadDateTime(ext);

                    throw new MessagePackSerializationException("Invalid primitive bytes.");
                case MessagePackType.Array:
                {
                    var length = reader.ReadArrayHeader();
                    if (length == 0) return Array.Empty<object>();

                    var objectFormatter = resolver.GetFormatter<object>();
                    var array = new object[length];
                    options.Security.DepthStep(ref reader);
                    try
                    {
                        for (var i = 0; i < length; i++) array[i] = objectFormatter.Deserialize(ref reader, options);
                    }
                    finally
                    {
                        reader.Depth--;
                    }

                    return array;
                }

                case MessagePackType.Map:
                {
                    var length = reader.ReadMapHeader();

                    options.Security.DepthStep(ref reader);
                    try
                    {
                        return DeserializeMap(ref reader, length, options);
                    }
                    finally
                    {
                        reader.Depth--;
                    }
                }

                case MessagePackType.Nil:
                    reader.ReadNil();
                    return null;
                default:
                    throw new MessagePackSerializationException("Invalid primitive bytes.");
            }
        }

        public static bool IsSupportedType(Type type, TypeInfo typeInfo, object value)
        {
            if (value == null) return true;

            if (TypeToJumpCode.ContainsKey(type)) return true;

            if (typeInfo.IsEnum) return true;

            if (value is IDictionary) return true;

            if (value is ICollection) return true;

            return false;
        }

        protected virtual object DeserializeMap(ref MessagePackReader reader, int length, MessagePackSerializerOptions options)
        {
            var objectFormatter = options.Resolver.GetFormatter<object>();
            var dictionary = new Dictionary<object, object>(length, options.Security.GetEqualityComparer<object>());
            for (var i = 0; i < length; i++)
            {
                var key = objectFormatter.Deserialize(ref reader, options);
                var value = objectFormatter.Deserialize(ref reader, options);
                dictionary.Add(key, value);
            }

            return dictionary;
        }
    }
}