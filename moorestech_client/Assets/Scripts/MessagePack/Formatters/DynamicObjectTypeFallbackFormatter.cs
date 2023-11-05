// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using MessagePack.Internal;

namespace MessagePack.Formatters
{
    /// <summary>
    ///     This formatter can serialize any value whose static type is <see cref="object" />
    ///     for which another resolver can provide a formatter for the runtime type.
    ///     Its deserialization is limited to forwarding all calls to the <see cref="PrimitiveObjectFormatter" />.
    /// </summary>
    public sealed class DynamicObjectTypeFallbackFormatter : IMessagePackFormatter<object>
    {
        public static readonly IMessagePackFormatter<object> Instance = new DynamicObjectTypeFallbackFormatter();

        private static readonly ThreadsafeTypeKeyHashTable<SerializeMethod> SerializerDelegates = new();

        private DynamicObjectTypeFallbackFormatter()
        {
        }

        public void Serialize(ref MessagePackWriter writer, object value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            var type = value.GetType();
            var ti = type.GetTypeInfo();

            if (type == typeof(object))
            {
                // serialize to empty map
                writer.WriteMapHeader(0);
                return;
            }

            if (PrimitiveObjectFormatter.IsSupportedType(type, ti, value))
                if (!(value is IDictionary || value is ICollection))
                {
                    PrimitiveObjectFormatter.Instance.Serialize(ref writer, value, options);
                    return;
                }

            var formatter = options.Resolver.GetFormatterDynamicWithVerify(type);
            if (!SerializerDelegates.TryGetValue(type, out var serializerDelegate))
                lock (SerializerDelegates)
                {
                    if (!SerializerDelegates.TryGetValue(type, out serializerDelegate))
                    {
                        var formatterType = typeof(IMessagePackFormatter<>).MakeGenericType(type);
                        var param0 = Expression.Parameter(typeof(object), "formatter");
                        var param1 = Expression.Parameter(typeof(MessagePackWriter).MakeByRefType(), "writer");
                        var param2 = Expression.Parameter(typeof(object), "value");
                        var param3 = Expression.Parameter(typeof(MessagePackSerializerOptions), "options");

                        var serializeMethodInfo = formatterType.GetRuntimeMethod("Serialize", new[] { typeof(MessagePackWriter).MakeByRefType(), type, typeof(MessagePackSerializerOptions) });

                        var body = Expression.Call(
                            Expression.Convert(param0, formatterType),
                            serializeMethodInfo,
                            param1,
                            ti.IsValueType ? Expression.Unbox(param2, type) : Expression.Convert(param2, type),
                            param3);

                        serializerDelegate = Expression.Lambda<SerializeMethod>(body, param0, param1, param2, param3).Compile();

                        SerializerDelegates.TryAdd(type, serializerDelegate);
                    }
                }

            serializerDelegate(formatter, ref writer, value, options);
        }

        public object Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return PrimitiveObjectFormatter.Instance.Deserialize(ref reader, options);
        }

        private delegate void SerializeMethod(object dynamicFormatter, ref MessagePackWriter writer, object value, MessagePackSerializerOptions options);
    }
}