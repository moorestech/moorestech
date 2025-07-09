// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !(UNITY_2018_3_OR_NEWER && NET_STANDARD_2_0)

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using MessagePack.Formatters;
using MessagePack.Internal;

#pragma warning disable SA1403 // File may only contain a single namespace

namespace MessagePack.Resolvers
{
    /// <summary>
    ///     ObjectResolver by dynamic code generation.
    /// </summary>
    public sealed class DynamicObjectResolver : IFormatterResolver
    {
        private const string ModuleName = "MessagePack.Resolvers.DynamicObjectResolver";

        /// <summary>
        ///     The singleton instance that can be used.
        /// </summary>
        public static readonly DynamicObjectResolver Instance;

        /// <summary>
        ///     A <see cref="MessagePackSerializerOptions" /> instance with this formatter pre-configured.
        /// </summary>
        public static readonly MessagePackSerializerOptions Options;

        internal static readonly Lazy<DynamicAssembly> DynamicAssembly;

        static DynamicObjectResolver()
        {
            Instance = new DynamicObjectResolver();
            Options = new MessagePackSerializerOptions(Instance);
            DynamicAssembly = new Lazy<DynamicAssembly>(() => new DynamicAssembly(ModuleName));
        }

        private DynamicObjectResolver()
        {
        }

#if NETFRAMEWORK
        public AssemblyBuilder Save()
        {
            return DynamicAssembly.Value.Save();
        }
#endif

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            return FormatterCache<T>.Formatter;
        }

        private static class FormatterCache<T>
        {
            public static readonly IMessagePackFormatter<T> Formatter;

            static FormatterCache()
            {
                var ti = typeof(T).GetTypeInfo();

                if (ti.IsInterface || ti.IsAbstract) return;

                if (ti.IsNullable())
                {
                    ti = ti.GenericTypeArguments[0].GetTypeInfo();

                    var innerFormatter = Instance.GetFormatterDynamic(ti.AsType());
                    if (innerFormatter == null) return;

                    Formatter = (IMessagePackFormatter<T>)Activator.CreateInstance(typeof(StaticNullableFormatter<>).MakeGenericType(ti.AsType()), innerFormatter);
                    return;
                }

                if (ti.IsAnonymous())
                {
                    Formatter = (IMessagePackFormatter<T>)DynamicObjectTypeBuilder.BuildFormatterToDynamicMethod(typeof(T), true, true, false);
                    return;
                }

                TypeInfo formatterTypeInfo;
                try
                {
                    formatterTypeInfo = DynamicObjectTypeBuilder.BuildType(DynamicAssembly.Value, typeof(T), false, false);
                }
                catch (InitAccessorInGenericClassNotSupportedException)
                {
                    Formatter = (IMessagePackFormatter<T>)DynamicObjectTypeBuilder.BuildFormatterToDynamicMethod(typeof(T), false, false, false);
                    return;
                }

                if (formatterTypeInfo == null) return;

                Formatter = (IMessagePackFormatter<T>)Activator.CreateInstance(formatterTypeInfo.AsType());
            }
        }
    }

    /// <summary>
    ///     ObjectResolver by dynamic code generation, allow private member.
    /// </summary>
    public sealed class DynamicObjectResolverAllowPrivate : IFormatterResolver
    {
        public static readonly DynamicObjectResolverAllowPrivate Instance = new();

        private DynamicObjectResolverAllowPrivate()
        {
        }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            return FormatterCache<T>.Formatter;
        }

        private static class FormatterCache<T>
        {
            internal static readonly IMessagePackFormatter<T> Formatter;

            static FormatterCache()
            {
                var ti = typeof(T).GetTypeInfo();

                if (ti.IsInterface || ti.IsAbstract) return;

                if (ti.IsNullable())
                {
                    ti = ti.GenericTypeArguments[0].GetTypeInfo();

                    var innerFormatter = Instance.GetFormatterDynamic(ti.AsType());
                    if (innerFormatter == null) return;

                    Formatter = (IMessagePackFormatter<T>)Activator.CreateInstance(typeof(StaticNullableFormatter<>).MakeGenericType(ti.AsType()), innerFormatter);
                    return;
                }

                if (ti.IsAnonymous())
                    Formatter = (IMessagePackFormatter<T>)DynamicObjectTypeBuilder.BuildFormatterToDynamicMethod(typeof(T), true, true, false);
                else
                    Formatter = (IMessagePackFormatter<T>)DynamicObjectTypeBuilder.BuildFormatterToDynamicMethod(typeof(T), false, false, true);
            }
        }
    }

    /// <summary>
    ///     ObjectResolver by dynamic code generation, no needs MessagePackObject attribute and serialized key as string.
    /// </summary>
    public sealed class DynamicContractlessObjectResolver : IFormatterResolver
    {
        public static readonly DynamicContractlessObjectResolver Instance = new();

        private const string ModuleName = "MessagePack.Resolvers.DynamicContractlessObjectResolver";

        private static readonly Lazy<DynamicAssembly> DynamicAssembly;

        private DynamicContractlessObjectResolver()
        {
        }

        static DynamicContractlessObjectResolver()
        {
            DynamicAssembly = new Lazy<DynamicAssembly>(() => new DynamicAssembly(ModuleName));
        }

#if NETFRAMEWORK
        public AssemblyBuilder Save()
        {
            return DynamicAssembly.Value.Save();
        }
#endif

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            return FormatterCache<T>.Formatter;
        }

        private static class FormatterCache<T>
        {
            public static readonly IMessagePackFormatter<T> Formatter;

            static FormatterCache()
            {
                if (typeof(T) == typeof(object)) return;

                var ti = typeof(T).GetTypeInfo();

                if (ti.IsInterface || ti.IsAbstract) return;

                if (ti.IsNullable())
                {
                    ti = ti.GenericTypeArguments[0].GetTypeInfo();

                    var innerFormatter = Instance.GetFormatterDynamic(ti.AsType());
                    if (innerFormatter == null) return;

                    Formatter = (IMessagePackFormatter<T>)Activator.CreateInstance(typeof(StaticNullableFormatter<>).MakeGenericType(ti.AsType()), innerFormatter);
                    return;
                }

                if (ti.IsAnonymous() || ti.HasPrivateCtorForSerialization())
                {
                    Formatter = (IMessagePackFormatter<T>)DynamicObjectTypeBuilder.BuildFormatterToDynamicMethod(typeof(T), true, true, false);
                    return;
                }

                var formatterTypeInfo = DynamicObjectTypeBuilder.BuildType(DynamicAssembly.Value, typeof(T), true, true);
                if (formatterTypeInfo == null) return;

                Formatter = (IMessagePackFormatter<T>)Activator.CreateInstance(formatterTypeInfo.AsType());
            }
        }
    }

    /// <summary>
    ///     ObjectResolver by dynamic code generation, no needs MessagePackObject attribute and serialized key as string, allow
    ///     private member.
    /// </summary>
    public sealed class DynamicContractlessObjectResolverAllowPrivate : IFormatterResolver
    {
        public static readonly DynamicContractlessObjectResolverAllowPrivate Instance = new();

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            return FormatterCache<T>.Formatter;
        }

        private static class FormatterCache<T>
        {
            internal static readonly IMessagePackFormatter<T> Formatter;

            static FormatterCache()
            {
                if (typeof(T) == typeof(object)) return;

                var ti = typeof(T).GetTypeInfo();

                if (ti.IsInterface || ti.IsAbstract) return;

                if (ti.IsNullable())
                {
                    ti = ti.GenericTypeArguments[0].GetTypeInfo();

                    var innerFormatter = Instance.GetFormatterDynamic(ti.AsType());
                    if (innerFormatter == null) return;

                    Formatter = (IMessagePackFormatter<T>)Activator.CreateInstance(typeof(StaticNullableFormatter<>).MakeGenericType(ti.AsType()), innerFormatter);
                    return;
                }

                if (ti.IsAnonymous())
                    Formatter = (IMessagePackFormatter<T>)DynamicObjectTypeBuilder.BuildFormatterToDynamicMethod(typeof(T), true, true, false);
                else
                    Formatter = (IMessagePackFormatter<T>)DynamicObjectTypeBuilder.BuildFormatterToDynamicMethod(typeof(T), true, true, true);
            }
        }
    }
}

namespace MessagePack.Internal
{
    internal static class DynamicObjectTypeBuilder
    {
#if !UNITY_2018_3_OR_NEWER
        private static readonly Regex SubtractFullNameRegex = new Regex(@", Version=\d+.\d+.\d+.\d+, Culture=\w+, PublicKeyToken=\w+", RegexOptions.Compiled);
#else
        private static readonly Regex SubtractFullNameRegex = new(@", Version=\d+.\d+.\d+.\d+, Culture=\w+, PublicKeyToken=\w+");
#endif

        private static int nameSequence;

        private static readonly HashSet<Type> ignoreTypes = new()
        {
            typeof(object),
            typeof(short),
            typeof(int),
            typeof(long),
            typeof(ushort),
            typeof(uint),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(bool),
            typeof(byte),
            typeof(sbyte),
            typeof(decimal),
            typeof(char),
            typeof(string),
            typeof(Guid),
            typeof(TimeSpan),
            typeof(DateTime),
            typeof(DateTimeOffset),
            typeof(Nil)
        };

        public static TypeInfo BuildType(DynamicAssembly assembly, Type type, bool forceStringKey, bool contractless)
        {
            if (ignoreTypes.Contains(type)) return null;

            var serializationInfo = ObjectSerializationInfo.CreateOrNull(type, forceStringKey, contractless, false, false);
            if (serializationInfo == null) return null;

            if (!(type.IsPublic || type.IsNestedPublic)) throw new MessagePackSerializationException("Building dynamic formatter only allows public type. Type: " + type.FullName);

            using (MonoProtection.EnterRefEmitLock())
            {
                var formatterType = typeof(IMessagePackFormatter<>).MakeGenericType(type);
                var typeBuilder = assembly.DefineType("MessagePack.Formatters." + SubtractFullNameRegex.Replace(type.FullName, string.Empty).Replace(".", "_") + "Formatter" + Interlocked.Increment(ref nameSequence), TypeAttributes.Public | TypeAttributes.Sealed, null, new[] { formatterType });

                FieldBuilder stringByteKeysField = null;
                Dictionary<ObjectSerializationInfo.EmittableMember, FieldInfo> customFormatterLookup = null;

                // string key needs string->int mapper for deserialize switch statement
                if (serializationInfo.IsStringKey)
                {
                    var method = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
                    stringByteKeysField = typeBuilder.DefineField("stringByteKeys", typeof(byte[][]), FieldAttributes.Private | FieldAttributes.InitOnly);

                    var il = method.GetILGenerator();
                    BuildConstructor(type, serializationInfo, method, stringByteKeysField, il);
                    customFormatterLookup = BuildCustomFormatterField(typeBuilder, serializationInfo, il);
                    il.Emit(OpCodes.Ret);
                }
                else
                {
                    var method = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
                    var il = method.GetILGenerator();
                    il.EmitLoadThis();
                    il.Emit(OpCodes.Call, objectCtor);
                    customFormatterLookup = BuildCustomFormatterField(typeBuilder, serializationInfo, il);
                    il.Emit(OpCodes.Ret);
                }

                {
                    var method = typeBuilder.DefineMethod(
                        "Serialize",
                        MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual,
                        null,
                        new[] { typeof(MessagePackWriter).MakeByRefType(), type, typeof(MessagePackSerializerOptions) });
                    method.DefineParameter(1, ParameterAttributes.None, "writer");
                    method.DefineParameter(2, ParameterAttributes.None, "value");
                    method.DefineParameter(3, ParameterAttributes.None, "options");

                    var il = method.GetILGenerator();
                    BuildSerialize(
                        type,
                        serializationInfo,
                        il,
                        () =>
                        {
                            il.EmitLoadThis();
                            il.EmitLdfld(stringByteKeysField);
                        },
                        (index, member) =>
                        {
                            FieldInfo fi;
                            if (!customFormatterLookup.TryGetValue(member, out fi)) return null;

                            return () =>
                            {
                                il.EmitLoadThis();
                                il.EmitLdfld(fi);
                            };
                        },
                        1);
                }

                {
                    var method = typeBuilder.DefineMethod(
                        "Deserialize",
                        MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual,
                        type,
                        new[] { refMessagePackReader, typeof(MessagePackSerializerOptions) });
                    method.DefineParameter(1, ParameterAttributes.None, "reader");
                    method.DefineParameter(2, ParameterAttributes.None, "options");

                    var il = method.GetILGenerator();
                    BuildDeserialize(
                        type,
                        serializationInfo,
                        il,
                        (index, member) =>
                        {
                            FieldInfo fi;
                            if (!customFormatterLookup.TryGetValue(member, out fi)) return null;

                            return () =>
                            {
                                il.EmitLoadThis();
                                il.EmitLdfld(fi);
                            };
                        },
                        1); // firstArgIndex:0 is this.
                }

                return typeBuilder.CreateTypeInfo();
            }
        }

        public static object BuildFormatterToDynamicMethod(Type type, bool forceStringKey, bool contractless, bool allowPrivate)
        {
            var serializationInfo = ObjectSerializationInfo.CreateOrNull(type, forceStringKey, contractless, allowPrivate, true);
            if (serializationInfo == null) return null;

            // internal delegate void AnonymousSerializeFunc<T>(byte[][] stringByteKeysField, object[] customFormatters, ref MessagePackWriter writer, T value, MessagePackSerializerOptions options);
            // internal delegate T AnonymousDeserializeFunc<T>(object[] customFormatters, ref MessagePackReader reader, MessagePackSerializerOptions options);
            var serialize = new DynamicMethod("Serialize", null, new[] { typeof(byte[][]), typeof(object[]), typeof(MessagePackWriter).MakeByRefType(), type, typeof(MessagePackSerializerOptions) }, type, true);
            DynamicMethod deserialize = null;

            var stringByteKeysField = new List<byte[]>();
            var serializeCustomFormatters = new List<object>();
            var deserializeCustomFormatters = new List<object>();

            if (serializationInfo.IsStringKey)
            {
                var i = 0;
                foreach (var item in serializationInfo.Members.Where(x => x.IsReadable))
                {
                    stringByteKeysField.Add(Utilities.GetWriterBytes(item.StringKey, (ref MessagePackWriter writer, string arg) => writer.Write(arg), SequencePool.Shared));
                    i++;
                }
            }

            foreach (var item in serializationInfo.Members.Where(x => x.IsReadable))
            {
                var attr = item.GetMessagePackFormatterAttribute();
                if (attr != null)
                {
                    var formatter = Activator.CreateInstance(attr.FormatterType, attr.Arguments);
                    serializeCustomFormatters.Add(formatter);
                }
                else
                {
                    serializeCustomFormatters.Add(null);
                }
            }

            foreach (var item in serializationInfo.Members)
            {
                // not only for writable because for use ctor.
                var attr = item.GetMessagePackFormatterAttribute();
                if (attr != null)
                {
                    var formatter = Activator.CreateInstance(attr.FormatterType, attr.Arguments);
                    deserializeCustomFormatters.Add(formatter);
                }
                else
                {
                    deserializeCustomFormatters.Add(null);
                }
            }

            {
                var il = serialize.GetILGenerator();
                BuildSerialize(
                    type,
                    serializationInfo,
                    il,
                    () => { il.EmitLdarg(0); },
                    (index, member) =>
                    {
                        if (serializeCustomFormatters.Count == 0) return null;

                        if (serializeCustomFormatters[index] == null) return null;

                        return () =>
                        {
                            il.EmitLdarg(1); // read object[]
                            il.EmitLdc_I4(index);
                            il.Emit(OpCodes.Ldelem_Ref); // object
                            il.Emit(OpCodes.Castclass, serializeCustomFormatters[index].GetType());
                        };
                    },
                    2); // 0, 1 is parameter.
            }

            if (serializationInfo.IsStruct || serializationInfo.BestmatchConstructor != null)
            {
                deserialize = new DynamicMethod("Deserialize", type, new[] { typeof(object[]), refMessagePackReader, typeof(MessagePackSerializerOptions) }, type, true);

                var il = deserialize.GetILGenerator();
                BuildDeserialize(
                    type,
                    serializationInfo,
                    il,
                    (index, member) =>
                    {
                        if (deserializeCustomFormatters.Count == 0) return null;

                        if (deserializeCustomFormatters[index] == null) return null;

                        return () =>
                        {
                            il.EmitLdarg(0); // read object[]
                            il.EmitLdc_I4(index);
                            il.Emit(OpCodes.Ldelem_Ref); // object
                            il.Emit(OpCodes.Castclass, deserializeCustomFormatters[index].GetType());
                        };
                    },
                    1);
            }

            object serializeDelegate = serialize.CreateDelegate(typeof(AnonymousSerializeFunc<>).MakeGenericType(type));
            var deserializeDelegate = deserialize == null
                ? null
                : (object)deserialize.CreateDelegate(typeof(AnonymousDeserializeFunc<>).MakeGenericType(type));
            var resultFormatter = Activator.CreateInstance(
                typeof(AnonymousSerializableFormatter<>).MakeGenericType(type), stringByteKeysField.ToArray(), serializeCustomFormatters.ToArray(), deserializeCustomFormatters.ToArray(), serializeDelegate, deserializeDelegate);
            return resultFormatter;
        }

        private static void BuildConstructor(Type type, ObjectSerializationInfo info, ConstructorInfo method, FieldBuilder stringByteKeysField, ILGenerator il)
        {
            il.EmitLoadThis();
            il.Emit(OpCodes.Call, objectCtor);

            var writeCount = info.Members.Count(x => x.IsReadable);
            il.EmitLoadThis();
            il.EmitLdc_I4(writeCount);
            il.Emit(OpCodes.Newarr, typeof(byte[]));

            var i = 0;
            foreach (var item in info.Members.Where(x => x.IsReadable))
            {
                il.Emit(OpCodes.Dup);
                il.EmitLdc_I4(i);
                il.Emit(OpCodes.Ldstr, item.StringKey);
                il.EmitCall(CodeGenHelpersTypeInfo.GetEncodedStringBytes);
                il.Emit(OpCodes.Stelem_Ref);
                i++;
            }

            il.Emit(OpCodes.Stfld, stringByteKeysField);
        }

        private static Dictionary<ObjectSerializationInfo.EmittableMember, FieldInfo> BuildCustomFormatterField(TypeBuilder builder, ObjectSerializationInfo info, ILGenerator il)
        {
            var dict = new Dictionary<ObjectSerializationInfo.EmittableMember, FieldInfo>();
            foreach (var item in info.Members.Where(x => x.IsReadable || x.IsActuallyWritable))
            {
                var attr = item.GetMessagePackFormatterAttribute();
                if (attr != null)
                {
                    var f = builder.DefineField(item.Name + "_formatter", attr.FormatterType, FieldAttributes.Private | FieldAttributes.InitOnly);

                    var bindingFlags = (int)(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    var attrVar = il.DeclareLocal(typeof(MessagePackFormatterAttribute));

                    il.Emit(OpCodes.Ldtoken, info.Type);
                    il.EmitCall(EmitInfo.GetTypeFromHandle);
                    il.Emit(OpCodes.Ldstr, item.Name);
                    il.EmitLdc_I4(bindingFlags);
                    if (item.IsProperty)
                        il.EmitCall(EmitInfo.TypeGetProperty);
                    else
                        il.EmitCall(EmitInfo.TypeGetField);

                    il.EmitTrue();
                    il.EmitCall(EmitInfo.GetCustomAttributeMessagePackFormatterAttribute);
                    il.EmitStloc(attrVar);

                    il.EmitLoadThis();

                    il.EmitLdloc(attrVar);
                    il.EmitCall(EmitInfo.MessagePackFormatterAttr.FormatterType);
                    il.EmitLdloc(attrVar);
                    il.EmitCall(EmitInfo.MessagePackFormatterAttr.Arguments);
                    il.EmitCall(EmitInfo.ActivatorCreateInstance);

                    il.Emit(OpCodes.Castclass, attr.FormatterType);
                    il.Emit(OpCodes.Stfld, f);

                    dict.Add(item, f);
                }
            }

            return dict;
        }

        // void Serialize(ref [arg:1]MessagePackWriter writer, [arg:2]T value, [arg:3]MessagePackSerializerOptions options);
        private static void BuildSerialize(Type type, ObjectSerializationInfo info, ILGenerator il, Action emitStringByteKeys, Func<int, ObjectSerializationInfo.EmittableMember, Action> tryEmitLoadCustomFormatter, int firstArgIndex)
        {
            var argWriter = new ArgumentField(il, firstArgIndex);
            var argValue = new ArgumentField(il, firstArgIndex + 1, type);
            var argOptions = new ArgumentField(il, firstArgIndex + 2);

            // if(value == null) return WriteNil
            if (type.GetTypeInfo().IsClass)
            {
                var elseBody = il.DefineLabel();

                argValue.EmitLoad();
                il.Emit(OpCodes.Brtrue_S, elseBody);
                argWriter.EmitLoad();
                il.EmitCall(MessagePackWriterTypeInfo.WriteNil);
                il.Emit(OpCodes.Ret);

                il.MarkLabel(elseBody);
            }

            // IMessagePackSerializationCallbackReceiver.OnBeforeSerialize()
            if (type.GetTypeInfo().ImplementedInterfaces.Any(x => x == typeof(IMessagePackSerializationCallbackReceiver)))
            {
                // call directly
                var runtimeMethods = type.GetRuntimeMethods().Where(x => x.Name == "OnBeforeSerialize").ToArray();
                if (runtimeMethods.Length == 1)
                {
                    argValue.EmitLoad();
                    il.Emit(OpCodes.Call, runtimeMethods[0]); // don't use EmitCall helper(must use 'Call')
                }
                else
                {
                    argValue.EmitLdarg(); // force ldarg
                    il.EmitBoxOrDoNothing(type);
                    il.EmitCall(onBeforeSerialize);
                }
            }

            // IFormatterResolver resolver = options.Resolver;
            var localResolver = il.DeclareLocal(typeof(IFormatterResolver));
            argOptions.EmitLoad();
            il.EmitCall(getResolverFromOptions);
            il.EmitStloc(localResolver);

            if (info.IsIntKey)
            {
                // use Array
                var maxKey = info.Members.Where(x => x.IsReadable).Select(x => x.IntKey).DefaultIfEmpty(-1).Max();
                var intKeyMap = info.Members.Where(x => x.IsReadable).ToDictionary(x => x.IntKey);

                var len = maxKey + 1;
                argWriter.EmitLoad();
                il.EmitLdc_I4(len);
                il.EmitCall(MessagePackWriterTypeInfo.WriteArrayHeader);

                var index = 0;
                for (var i = 0; i <= maxKey; i++)
                {
                    ObjectSerializationInfo.EmittableMember member;
                    if (intKeyMap.TryGetValue(i, out member))
                    {
                        EmitSerializeValue(il, type.GetTypeInfo(), member, index++, tryEmitLoadCustomFormatter, argWriter, argValue, argOptions, localResolver);
                    }
                    else
                    {
                        // Write Nil as Blanc
                        argWriter.EmitLoad();
                        il.EmitCall(MessagePackWriterTypeInfo.WriteNil);
                    }
                }
            }
            else
            {
                // use Map
                var writeCount = info.Members.Count(x => x.IsReadable);

                argWriter.EmitLoad();
                il.EmitLdc_I4(writeCount);
                ////if (writeCount <= MessagePackRange.MaxFixMapCount)
                ////{
                ////    il.EmitCall(MessagePackWriterTypeInfo.WriteFixedMapHeaderUnsafe);
                ////}
                ////else
                {
                    il.EmitCall(MessagePackWriterTypeInfo.WriteMapHeader);
                }

                var index = 0;
                foreach (var item in info.Members.Where(x => x.IsReadable))
                {
                    argWriter.EmitLoad();
                    emitStringByteKeys();
                    il.EmitLdc_I4(index);
                    il.Emit(OpCodes.Ldelem_Ref);
                    il.Emit(OpCodes.Call, ReadOnlySpanFromByteArray); // convert byte[] to ReadOnlySpan<byte>

                    // Optimize, WriteRaw(Unity, large) or UnsafeMemory32/64.WriteRawX
#if !UNITY_2018_3_OR_NEWER
                    var valueLen = CodeGenHelpers.GetEncodedStringBytes(item.StringKey).Length;
                    if (valueLen <= MessagePackRange.MaxFixStringLength)
                    {
                        if (UnsafeMemory.Is32Bit)
                        {
                            il.EmitCall(typeof(UnsafeMemory32).GetRuntimeMethod("WriteRaw" + valueLen, new[] { typeof(MessagePackWriter).MakeByRefType(), typeof(ReadOnlySpan<byte>) }));
                        }
                        else
                        {
                            il.EmitCall(typeof(UnsafeMemory64).GetRuntimeMethod("WriteRaw" + valueLen, new[] { typeof(MessagePackWriter).MakeByRefType(), typeof(ReadOnlySpan<byte>) }));
                        }
                    }
                    else
#endif
                    {
                        il.EmitCall(MessagePackWriterTypeInfo.WriteRaw);
                    }

                    EmitSerializeValue(il, type.GetTypeInfo(), item, index, tryEmitLoadCustomFormatter, argWriter, argValue, argOptions, localResolver);
                    index++;
                }
            }

            il.Emit(OpCodes.Ret);
        }

        private static void EmitSerializeValue(ILGenerator il, TypeInfo type, ObjectSerializationInfo.EmittableMember member, int index, Func<int, ObjectSerializationInfo.EmittableMember, Action> tryEmitLoadCustomFormatter, ArgumentField argWriter, ArgumentField argValue, ArgumentField argOptions, LocalBuilder localResolver)
        {
            var endLabel = il.DefineLabel();
            var t = member.Type;
            var emitter = tryEmitLoadCustomFormatter(index, member);
            if (emitter != null)
            {
                emitter();
                argWriter.EmitLoad();
                argValue.EmitLoad();
                member.EmitLoadValue(il);
                argOptions.EmitLoad();
                il.EmitCall(getSerialize(t));
            }
            else if (ObjectSerializationInfo.IsOptimizeTargetType(t))
            {
                if (!t.GetTypeInfo().IsValueType)
                {
                    // As a nullable type (e.g. byte[] and string) we need to call WriteNil for null values.
                    var writeNonNilValueLabel = il.DefineLabel();
                    var memberValue = il.DeclareLocal(t);
                    argValue.EmitLoad();
                    member.EmitLoadValue(il);
                    il.Emit(OpCodes.Dup);
                    il.EmitStloc(memberValue);
                    il.Emit(OpCodes.Brtrue, writeNonNilValueLabel);
                    argWriter.EmitLoad();
                    il.EmitCall(MessagePackWriterTypeInfo.WriteNil);
                    il.Emit(OpCodes.Br, endLabel);

                    il.MarkLabel(writeNonNilValueLabel);
                    argWriter.EmitLoad();
                    il.EmitLdloc(memberValue);
                }
                else
                {
                    argWriter.EmitLoad();
                    argValue.EmitLoad();
                    member.EmitLoadValue(il);
                }

                if (t == typeof(byte[]))
                {
                    il.EmitCall(ReadOnlySpanFromByteArray);
                    il.EmitCall(MessagePackWriterTypeInfo.WriteBytes);
                }
                else
                {
                    il.EmitCall(typeof(MessagePackWriter).GetRuntimeMethod("Write", new[] { t }));
                }
            }
            else
            {
                il.EmitLdloc(localResolver);
                il.Emit(OpCodes.Call, getFormatterWithVerify.MakeGenericMethod(t));

                argWriter.EmitLoad();
                argValue.EmitLoad();
                member.EmitLoadValue(il);
                argOptions.EmitLoad();
                il.EmitCall(getSerialize(t));
            }

            il.MarkLabel(endLabel);
        }

        // T Deserialize([arg:1]ref MessagePackReader reader, [arg:2]MessagePackSerializerOptions options);
        private static void BuildDeserialize(Type type, ObjectSerializationInfo info, ILGenerator il, Func<int, ObjectSerializationInfo.EmittableMember, Action> tryEmitLoadCustomFormatter, int firstArgIndex)
        {
            var argReader = new ArgumentField(il, firstArgIndex, true);
            var argOptions = new ArgumentField(il, firstArgIndex + 1);

            // if (reader.TryReadNil()) { throw / return; }
            BuildDeserializeInternalTryReadNil(type, il, ref argReader);

            // T ____result;
            var localResult = il.DeclareLocal(type);

            // where T : new()
            var canOverwrite = info.ConstructorParameters.Length == 0;
            if (canOverwrite)
                // ____result = new T();
                BuildDeserializeInternalCreateInstance(type, info, il, localResult);

            // options.Security.DepthStep(ref reader);
            BuildDeserializeInternalDepthStep(il, ref argReader, ref argOptions);

            // var length = reader.Read(Map|Array)Header();
            var localLength = BuildDeserializeInternalReadHeaderLength(info, il, ref argReader);

            // var resolver = options.Resolver;
            var localResolver = BuildDeserializeInternalResolver(info, il, ref argOptions);

            if (info.IsIntKey)
                // switch (key) { ... }
                BuildDeserializeInternalDeserializeEachPropertyIntKey(info, il, tryEmitLoadCustomFormatter, canOverwrite, ref argReader, ref argOptions, localResolver, localResult, localLength);
            else
                // var span = reader.ReadStringSpan();
                BuildDeserializeInternalDeserializeEachPropertyStringKey(info, il, tryEmitLoadCustomFormatter, canOverwrite, ref argReader, argOptions, localResolver, localResult, localLength);

            // ____result.OnAfterDeserialize()
            BuildDeserializeInternalOnAfterDeserialize(type, info, il, localResult);

            // reader.Depth--;
            BuildDeserializeInternalDepthUnStep(il, ref argReader);

            // return ____result;
            il.Emit(OpCodes.Ldloc, localResult);
            il.Emit(OpCodes.Ret);
        }

        private static void BuildDeserializeInternalDeserializeEachPropertyStringKey(ObjectSerializationInfo info, ILGenerator il, Func<int, ObjectSerializationInfo.EmittableMember, Action> tryEmitLoadCustomFormatter, bool canOverwrite, ref ArgumentField argReader, ArgumentField argOptions, LocalBuilder localResolver, LocalBuilder localResult, LocalBuilder localLength)
        {
            // Prepare local variables or assignment fields/properties
            var infoList = BuildDeserializeInternalDeserializationInfoArrayStringKey(info, il, canOverwrite);

            // Read Loop(for var i = 0; i < length; i++)
            BuildDeserializeInternalDeserializeLoopStringKey(il, tryEmitLoadCustomFormatter, ref argReader, ref argOptions, infoList, localResolver, localResult, localLength, canOverwrite, info);

            if (canOverwrite) return;

            // ____result = new T(...);
            BuildDeserializeInternalCreateInstanceWithArguments(info, il, infoList, localResult);

            // ... if (__field__IsInitialized) { ____result.field = __field__; } ...
            BuildDeserializeInternalAssignFieldFromLocalVariableStringKey(info, il, infoList, localResult);
        }

        private static void BuildDeserializeInternalDeserializeEachPropertyIntKey(ObjectSerializationInfo info, ILGenerator il, Func<int, ObjectSerializationInfo.EmittableMember, Action> tryEmitLoadCustomFormatter, bool canOverwrite, ref ArgumentField argReader, ref ArgumentField argOptions, LocalBuilder localResolver, LocalBuilder localResult, LocalBuilder localLength)
        {
            // Prepare local variables or assignment fields/properties
            var infoList = BuildDeserializeInternalDeserializationInfoArrayIntKey(info, il, canOverwrite, out var gotoDefault, out var maxKey);

            // Read Loop(for var i = 0; i < length; i++)
            BuildDeserializeInternalDeserializeLoopIntKey(il, tryEmitLoadCustomFormatter, ref argReader, ref argOptions, infoList, localResolver, localResult, localLength, canOverwrite, gotoDefault);

            if (canOverwrite) return;

            // ____result = new T(...);
            BuildDeserializeInternalCreateInstanceWithArguments(info, il, infoList, localResult);

            // ... ____result.field = __field__; ...
            BuildDeserializeInternalAssignFieldFromLocalVariableIntKey(info, il, infoList, localResult, localLength, maxKey);
        }

        private static void BuildDeserializeInternalAssignFieldFromLocalVariableStringKey(ObjectSerializationInfo info, ILGenerator il, DeserializeInfo[] infoList, LocalBuilder localResult)
        {
            foreach (var item in infoList)
            {
                if (item.MemberInfo == null || item.IsInitializedLocalVariable == null || item.MemberInfo.IsWrittenByConstructor) continue;

                // if (__field__IsInitialized) { ____result.field = __field__; }
                var skipLabel = il.DefineLabel();
                il.EmitLdloc(item.IsInitializedLocalVariable);
                il.Emit(OpCodes.Brfalse_S, skipLabel);

                if (info.IsClass)
                    il.EmitLdloc(localResult);
                else
                    il.EmitLdloca(localResult);

                il.EmitLdloc(item.LocalVariable);
                item.MemberInfo.EmitStoreValue(il);

                il.MarkLabel(skipLabel);
            }
        }

        private static void BuildDeserializeInternalAssignFieldFromLocalVariableIntKey(ObjectSerializationInfo info, ILGenerator il, DeserializeInfo[] infoList, LocalBuilder localResult, LocalBuilder localLength, int maxKey)
        {
            if (maxKey == -1) return;

            Label? memberAssignmentDoneLabel = null;
            var intKeyMap = infoList.Where(x => x.MemberInfo != null && x.MemberInfo.IsActuallyWritable).ToDictionary(x => x.MemberInfo.IntKey);
            for (var key = 0; key <= maxKey; key++)
            {
                if (!intKeyMap.TryGetValue(key, out var item)) continue;

                if (item.MemberInfo.IsWrittenByConstructor) continue;

                // if (length <= key) { goto MEMBER_ASSIGNMENT_DONE; }
                il.EmitLdloc(localLength);
                il.EmitLdc_I4(key);
                if (memberAssignmentDoneLabel == null) memberAssignmentDoneLabel = il.DefineLabel();

                il.Emit(OpCodes.Ble, memberAssignmentDoneLabel.Value);

                // ____result.field = __field__;
                if (info.IsClass)
                    il.EmitLdloc(localResult);
                else
                    il.EmitLdloca(localResult);

                il.EmitLdloc(item.LocalVariable);
                item.MemberInfo.EmitStoreValue(il);
            }

            // MEMBER_ASSIGNMENT_DONE:
            if (memberAssignmentDoneLabel != null) il.MarkLabel(memberAssignmentDoneLabel.Value);
        }

        private static void BuildDeserializeInternalCreateInstanceWithArguments(ObjectSerializationInfo info, ILGenerator il, DeserializeInfo[] infoList, LocalBuilder localResult)
        {
            foreach (var item in info.ConstructorParameters)
            {
                var local = infoList.First(x => x.MemberInfo == item.MemberInfo);
                il.EmitLdloc(local.LocalVariable);

                if (!item.ConstructorParameter.ParameterType.IsValueType && local.MemberInfo.IsValueType)
                    // When a constructor argument of type object is being provided by a serialized member value that is a value type
                    // then that value must be boxed in order for the generated code to be valid (see issue #987). This may occur because
                    // the only requirement when determining whether a member value may be used to populate a constructor argument in an
                    // IsAssignableFrom check and typeof(object) IsAssignableFrom typeof(int), for example.
                    il.Emit(OpCodes.Box, local.MemberInfo.Type);
            }

            il.Emit(OpCodes.Newobj, info.BestmatchConstructor);
            il.Emit(OpCodes.Stloc, localResult);
        }

        private static DeserializeInfo[] BuildDeserializeInternalDeserializationInfoArrayStringKey(ObjectSerializationInfo info, ILGenerator il, bool canOverwrite)
        {
            var infoList = new DeserializeInfo[info.Members.Length];
            for (var i = 0; i < infoList.Length; i++)
            {
                var item = info.Members[i];
                if (canOverwrite && item.IsActuallyWritable)
                {
                    infoList[i] = new DeserializeInfo
                    {
                        MemberInfo = item
                    };
                }
                else
                {
                    var isConstructorParameter = info.ConstructorParameters.Any(p => p.MemberInfo.Equals(item));
                    infoList[i] = new DeserializeInfo
                    {
                        MemberInfo = item,
                        LocalVariable = il.DeclareLocal(item.Type),
                        IsInitializedLocalVariable = isConstructorParameter ? default : il.DeclareLocal(typeof(bool))
                    };
                }
            }

            return infoList;
        }

        private static DeserializeInfo[] BuildDeserializeInternalDeserializationInfoArrayIntKey(ObjectSerializationInfo info, ILGenerator il, bool canOverwrite, out Label? gotoDefault, out int maxKey)
        {
            maxKey = info.Members.Select(x => x.IntKey).DefaultIfEmpty(-1).Max();
            var len = maxKey + 1;
            var intKeyMap = info.Members.ToDictionary(x => x.IntKey);
            gotoDefault = null;

            var infoList = new DeserializeInfo[len];
            for (var i = 0; i < infoList.Length; i++)
                if (intKeyMap.TryGetValue(i, out var member))
                {
                    if (canOverwrite && member.IsActuallyWritable)
                        infoList[i] = new DeserializeInfo
                        {
                            MemberInfo = member,
                            SwitchLabel = il.DefineLabel()
                        };
                    else
                        infoList[i] = new DeserializeInfo
                        {
                            MemberInfo = member,
                            LocalVariable = il.DeclareLocal(member.Type),
                            SwitchLabel = il.DefineLabel()
                        };
                }
                else
                {
                    // return null MemberInfo, should filter null
                    if (gotoDefault == null) gotoDefault = il.DefineLabel();

                    infoList[i] = new DeserializeInfo
                    {
                        SwitchLabel = gotoDefault.Value
                    };
                }

            return infoList;
        }

        private static void BuildDeserializeInternalDeserializeLoopIntKey(ILGenerator il, Func<int, ObjectSerializationInfo.EmittableMember, Action> tryEmitLoadCustomFormatter, ref ArgumentField argReader, ref ArgumentField argOptions, DeserializeInfo[] infoList, LocalBuilder localResolver, LocalBuilder localResult, LocalBuilder localLength, bool canOverwrite, Label? gotoDefault)
        {
            var key = il.DeclareLocal(typeof(int));
            var switchDefault = il.DefineLabel();
            var reader = argReader;
            var options = argOptions;

            void ForBody(LocalBuilder forILocal)
            {
                var loopEnd = il.DefineLabel();

                il.EmitLdloc(forILocal);
                il.EmitStloc(key);

                // switch... local = Deserialize
                il.EmitLdloc(key);

                il.Emit(OpCodes.Switch, infoList.Select(x => x.SwitchLabel).ToArray());

                il.MarkLabel(switchDefault);

                // default, only read. reader.ReadNextBlock();
                reader.EmitLdarg();
                il.EmitCall(MessagePackReaderTypeInfo.Skip);
                il.Emit(OpCodes.Br, loopEnd);

                if (gotoDefault != null)
                {
                    il.MarkLabel(gotoDefault.Value);
                    il.Emit(OpCodes.Br, switchDefault);
                }

                var i = 0;
                foreach (var item in infoList)
                {
                    if (item.MemberInfo == null) continue;

                    il.MarkLabel(item.SwitchLabel);
                    if (canOverwrite)
                        BuildDeserializeInternalDeserializeValueAssignDirectly(il, item, i++, tryEmitLoadCustomFormatter, ref reader, ref options, localResolver, localResult);
                    else
                        BuildDeserializeInternalDeserializeValueAssignLocalVariable(il, item, i++, tryEmitLoadCustomFormatter, ref reader, ref options, localResolver, localResult);

                    il.Emit(OpCodes.Br, loopEnd);
                }

                il.MarkLabel(loopEnd);
            }

            il.EmitIncrementFor(localLength, ForBody);
        }

        private static void BuildDeserializeInternalDeserializeLoopStringKey(ILGenerator il, Func<int, ObjectSerializationInfo.EmittableMember, Action> tryEmitLoadCustomFormatter, ref ArgumentField argReader, ref ArgumentField argOptions, DeserializeInfo[] infoList, LocalBuilder localResolver, LocalBuilder localResult, LocalBuilder localLength, bool canOverwrite, ObjectSerializationInfo info)
        {
            var automata = new AutomataDictionary();
            for (var i = 0; i < info.Members.Length; i++) automata.Add(info.Members[i].StringKey, i);

            var buffer = il.DeclareLocal(typeof(ReadOnlySpan<byte>));
            var longKey = il.DeclareLocal(typeof(ulong));
            var reader = argReader;
            var options = argOptions;

            // for (int i = 0; i < len; i++)
            void ForBody(LocalBuilder forILocal)
            {
                var readNext = il.DefineLabel();
                var loopEnd = il.DefineLabel();

                reader.EmitLdarg();
                il.EmitCall(ReadStringSpan);
                il.EmitStloc(buffer);

                // gen automata name lookup
                void OnFoundAssignDirect(KeyValuePair<string, int> x)
                {
                    var i = x.Value;
                    var item = infoList[i];
                    if (item.MemberInfo != null)
                    {
                        BuildDeserializeInternalDeserializeValueAssignDirectly(il, item, i, tryEmitLoadCustomFormatter, ref reader, ref options, localResolver, localResult);
                        il.Emit(OpCodes.Br, loopEnd);
                    }
                    else
                    {
                        il.Emit(OpCodes.Br, readNext);
                    }
                }

                void OnFoundAssignLocalVariable(KeyValuePair<string, int> x)
                {
                    var i = x.Value;
                    var item = infoList[i];
                    if (item.MemberInfo != null)
                    {
                        BuildDeserializeInternalDeserializeValueAssignLocalVariable(il, item, i, tryEmitLoadCustomFormatter, ref reader, ref options, localResolver, localResult);
                        il.Emit(OpCodes.Br, loopEnd);
                    }
                    else
                    {
                        il.Emit(OpCodes.Br, readNext);
                    }
                }

                void OnNotFound()
                {
                    il.Emit(OpCodes.Br, readNext);
                }

                if (canOverwrite)
                    automata.EmitMatch(il, buffer, longKey, OnFoundAssignDirect, OnNotFound);
                else
                    automata.EmitMatch(il, buffer, longKey, OnFoundAssignLocalVariable, OnNotFound);

                il.MarkLabel(readNext);
                reader.EmitLdarg();
                il.EmitCall(MessagePackReaderTypeInfo.Skip);

                il.MarkLabel(loopEnd);
            }

            il.EmitIncrementFor(localLength, ForBody);
        }

        private static void BuildDeserializeInternalTryReadNil(Type type, ILGenerator il, ref ArgumentField argReader)
        {
            // if(reader.TryReadNil()) { return null; }
            var falseLabel = il.DefineLabel();
            argReader.EmitLdarg();
            il.EmitCall(MessagePackReaderTypeInfo.TryReadNil);
            il.Emit(OpCodes.Brfalse_S, falseLabel);
            if (type.GetTypeInfo().IsClass)
            {
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Ret);
            }
            else
            {
                il.Emit(OpCodes.Ldstr, "typecode is null, struct not supported");
                il.Emit(OpCodes.Newobj, messagePackSerializationExceptionMessageOnlyConstructor);
                il.Emit(OpCodes.Throw);
            }

            il.MarkLabel(falseLabel);
        }

        private static void BuildDeserializeInternalDepthUnStep(ILGenerator il, ref ArgumentField argReader)
        {
            argReader.EmitLdarg();
            il.Emit(OpCodes.Dup);
            il.EmitCall(readerDepthGet);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Sub_Ovf);
            il.EmitCall(readerDepthSet);
        }

        private static void BuildDeserializeInternalOnAfterDeserialize(Type type, ObjectSerializationInfo info, ILGenerator il, LocalBuilder localResult)
        {
            if (type.GetTypeInfo().ImplementedInterfaces.All(x => x != typeof(IMessagePackSerializationCallbackReceiver))) return;

            if (info.IsClass) il.EmitLdloc(localResult);

            // call directly
            var runtimeMethod = type.GetRuntimeMethods().SingleOrDefault(x => x.Name == "OnAfterDeserialize");
            if (runtimeMethod != null)
            {
                if (info.IsStruct) il.EmitLdloca(localResult);

                il.Emit(OpCodes.Call, runtimeMethod); // don't use EmitCall helper(must use 'Call')
            }
            else
            {
                if (info.IsStruct)
                {
                    il.EmitLdloc(localResult);
                    il.Emit(OpCodes.Box, type);
                }

                il.EmitCall(onAfterDeserialize);
            }
        }

        private static LocalBuilder BuildDeserializeInternalResolver(ObjectSerializationInfo info, ILGenerator il, ref ArgumentField argOptions)
        {
            if (!info.ShouldUseFormatterResolver) return default;

            // IFormatterResolver resolver = options.Resolver;
            var localResolver = il.DeclareLocal(typeof(IFormatterResolver));
            argOptions.EmitLoad();
            il.EmitCall(getResolverFromOptions);
            il.EmitStloc(localResolver);
            return localResolver;
        }

        private static LocalBuilder BuildDeserializeInternalReadHeaderLength(ObjectSerializationInfo info, ILGenerator il, ref ArgumentField argReader)
        {
            // var length = ReadMapHeader(ref byteSequence);
            var length = il.DeclareLocal(typeof(int)); // [loc:1]
            argReader.EmitLdarg();

            il.EmitCall(info.IsIntKey ? MessagePackReaderTypeInfo.ReadArrayHeader : MessagePackReaderTypeInfo.ReadMapHeader);

            il.EmitStloc(length);
            return length;
        }

        private static void BuildDeserializeInternalDepthStep(ILGenerator il, ref ArgumentField argReader, ref ArgumentField argOptions)
        {
            argOptions.EmitLoad();
            il.EmitCall(getSecurityFromOptions);
            argReader.EmitLdarg();
            il.EmitCall(securityDepthStep);
        }

        // where T : new();
        private static void BuildDeserializeInternalCreateInstance(Type type, ObjectSerializationInfo info, ILGenerator il, LocalBuilder localResult)
        {
            // var result = new T();
            if (info.IsClass)
            {
                il.Emit(OpCodes.Newobj, info.BestmatchConstructor);
                il.EmitStloc(localResult);
            }
            else
            {
                il.Emit(OpCodes.Ldloca, localResult);
                il.Emit(OpCodes.Initobj, type);
            }
        }

        private static void BuildDeserializeInternalDeserializeValueAssignDirectly(ILGenerator il, DeserializeInfo info, int index, Func<int, ObjectSerializationInfo.EmittableMember, Action> tryEmitLoadCustomFormatter, ref ArgumentField argReader, ref ArgumentField argOptions, LocalBuilder localResolver, LocalBuilder localResult)
        {
            var storeLabel = il.DefineLabel();
            var member = info.MemberInfo;
            var t = member.Type;
            var emitter = tryEmitLoadCustomFormatter(index, member);

            if (member.IsActuallyWritable)
            {
                if (localResult.LocalType.IsClass)
                    il.EmitLdloc(localResult);
                else
                    il.EmitLdloca(localResult);
            }
            else if (info.IsInitializedLocalVariable != null)
            {
                il.EmitLdc_I4(1);
                il.EmitStloc(info.IsInitializedLocalVariable);
            }

            if (emitter != null)
            {
                emitter();
                argReader.EmitLdarg();
                argOptions.EmitLoad();
                il.EmitCall(getDeserialize(t));
            }
            else if (ObjectSerializationInfo.IsOptimizeTargetType(t))
            {
                if (!t.GetTypeInfo().IsValueType)
                {
                    // As a nullable type (e.g. byte[] and string) we need to first call TryReadNil
                    // if (reader.TryReadNil())
                    var readNonNilValueLabel = il.DefineLabel();
                    argReader.EmitLdarg();
                    il.EmitCall(MessagePackReaderTypeInfo.TryReadNil);
                    il.Emit(OpCodes.Brfalse_S, readNonNilValueLabel);
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Br, storeLabel);

                    il.MarkLabel(readNonNilValueLabel);
                }

                argReader.EmitLdarg();
                if (t == typeof(byte[]))
                {
                    var local = il.DeclareLocal(typeof(ReadOnlySequence<byte>?));
                    il.EmitCall(MessagePackReaderTypeInfo.ReadBytes);
                    il.EmitStloc(local);
                    il.EmitLdloca(local);
                    il.EmitCall(ArrayFromNullableReadOnlySequence);
                }
                else
                {
                    il.EmitCall(MessagePackReaderTypeInfo.TypeInfo.GetDeclaredMethods("Read" + t.Name).First(x => x.GetParameters().Length == 0));
                }
            }
            else
            {
                il.EmitLdloc(localResolver);
                il.EmitCall(getFormatterWithVerify.MakeGenericMethod(t));
                argReader.EmitLdarg();
                argOptions.EmitLoad();
                il.EmitCall(getDeserialize(t));
            }

            il.MarkLabel(storeLabel);
            if (member.IsActuallyWritable)
                member.EmitStoreValue(il);
            else
                il.Emit(OpCodes.Pop);
        }

        private static void BuildDeserializeInternalDeserializeValueAssignLocalVariable(ILGenerator il, DeserializeInfo info, int index, Func<int, ObjectSerializationInfo.EmittableMember, Action> tryEmitLoadCustomFormatter, ref ArgumentField argReader, ref ArgumentField argOptions, LocalBuilder localResolver, LocalBuilder localResult)
        {
            var storeLabel = il.DefineLabel();
            var member = info.MemberInfo;
            var t = member.Type;
            var emitter = tryEmitLoadCustomFormatter(index, member);

            if (info.IsInitializedLocalVariable != null)
            {
                il.EmitLdc_I4(1);
                il.EmitStloc(info.IsInitializedLocalVariable);
            }

            if (emitter != null)
            {
                emitter();
                argReader.EmitLdarg();
                argOptions.EmitLoad();
                il.EmitCall(getDeserialize(t));
            }
            else if (ObjectSerializationInfo.IsOptimizeTargetType(t))
            {
                if (!t.GetTypeInfo().IsValueType)
                {
                    // As a nullable type (e.g. byte[] and string) we need to first call TryReadNil
                    // if (reader.TryReadNil())
                    var readNonNilValueLabel = il.DefineLabel();
                    argReader.EmitLdarg();
                    il.EmitCall(MessagePackReaderTypeInfo.TryReadNil);
                    il.Emit(OpCodes.Brfalse_S, readNonNilValueLabel);
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Br, storeLabel);

                    il.MarkLabel(readNonNilValueLabel);
                }

                argReader.EmitLdarg();
                if (t == typeof(byte[]))
                {
                    var local = il.DeclareLocal(typeof(ReadOnlySequence<byte>?));
                    il.EmitCall(MessagePackReaderTypeInfo.ReadBytes);
                    il.EmitStloc(local);
                    il.EmitLdloca(local);
                    il.EmitCall(ArrayFromNullableReadOnlySequence);
                }
                else
                {
                    il.EmitCall(MessagePackReaderTypeInfo.TypeInfo.GetDeclaredMethods("Read" + t.Name).First(x => x.GetParameters().Length == 0));
                }
            }
            else
            {
                il.EmitLdloc(localResolver);
                il.EmitCall(getFormatterWithVerify.MakeGenericMethod(t));
                argReader.EmitLdarg();
                argOptions.EmitLoad();
                il.EmitCall(getDeserialize(t));
            }

            il.MarkLabel(storeLabel);
            il.EmitStloc(info.LocalVariable);
        }

#pragma warning disable SA1311 // Static readonly fields should begin with upper-case letter

        // EmitInfos...
        private static readonly Type refMessagePackReader = typeof(MessagePackReader).MakeByRefType();

        private static readonly MethodInfo ReadOnlySpanFromByteArray = typeof(ReadOnlySpan<byte>).GetRuntimeMethod("op_Implicit", new[] { typeof(byte[]) });
        private static readonly MethodInfo ReadStringSpan = typeof(CodeGenHelpers).GetRuntimeMethod(nameof(CodeGenHelpers.ReadStringSpan), new[] { typeof(MessagePackReader).MakeByRefType() });
        private static readonly MethodInfo ArrayFromNullableReadOnlySequence = typeof(CodeGenHelpers).GetRuntimeMethod(nameof(CodeGenHelpers.GetArrayFromNullableSequence), new[] { typeof(ReadOnlySequence<byte>?).MakeByRefType() });

        private static readonly MethodInfo getFormatterWithVerify = typeof(FormatterResolverExtensions).GetRuntimeMethods().First(x => x.Name == nameof(FormatterResolverExtensions.GetFormatterWithVerify));
        private static readonly MethodInfo getResolverFromOptions = typeof(MessagePackSerializerOptions).GetRuntimeProperty(nameof(MessagePackSerializerOptions.Resolver)).GetMethod;
        private static readonly MethodInfo getSecurityFromOptions = typeof(MessagePackSerializerOptions).GetRuntimeProperty(nameof(MessagePackSerializerOptions.Security)).GetMethod;
        private static readonly MethodInfo securityDepthStep = typeof(MessagePackSecurity).GetRuntimeMethod(nameof(MessagePackSecurity.DepthStep), new[] { typeof(MessagePackReader).MakeByRefType() });
        private static readonly MethodInfo readerDepthGet = typeof(MessagePackReader).GetRuntimeProperty(nameof(MessagePackReader.Depth)).GetMethod;
        private static readonly MethodInfo readerDepthSet = typeof(MessagePackReader).GetRuntimeProperty(nameof(MessagePackReader.Depth)).SetMethod;
        private static readonly Func<Type, MethodInfo> getSerialize = t => typeof(IMessagePackFormatter<>).MakeGenericType(t).GetRuntimeMethod(nameof(IMessagePackFormatter<int>.Serialize), new[] { typeof(MessagePackWriter).MakeByRefType(), t, typeof(MessagePackSerializerOptions) });

        private static readonly Func<Type, MethodInfo> getDeserialize = t => typeof(IMessagePackFormatter<>).MakeGenericType(t).GetRuntimeMethod(nameof(IMessagePackFormatter<int>.Deserialize), new[] { refMessagePackReader, typeof(MessagePackSerializerOptions) });

        //// static readonly ConstructorInfo dictionaryConstructor = typeof(ByteArrayStringHashTable).GetTypeInfo().DeclaredConstructors.First(x => { var p = x.GetParameters(); return p.Length == 1 && p[0].ParameterType == typeof(int); });
        //// static readonly MethodInfo dictionaryAdd = typeof(ByteArrayStringHashTable).GetRuntimeMethod("Add", new[] { typeof(string), typeof(int) });
        //// static readonly MethodInfo dictionaryTryGetValue = typeof(ByteArrayStringHashTable).GetRuntimeMethod("TryGetValue", new[] { typeof(ArraySegment<byte>), refInt });
        private static readonly ConstructorInfo messagePackSerializationExceptionMessageOnlyConstructor = typeof(MessagePackSerializationException).GetTypeInfo().DeclaredConstructors.First(x =>
        {
            var p = x.GetParameters();
            return p.Length == 1 && p[0].ParameterType == typeof(string);
        });

        private static readonly MethodInfo onBeforeSerialize = typeof(IMessagePackSerializationCallbackReceiver).GetRuntimeMethod(nameof(IMessagePackSerializationCallbackReceiver.OnBeforeSerialize), Type.EmptyTypes);
        private static readonly MethodInfo onAfterDeserialize = typeof(IMessagePackSerializationCallbackReceiver).GetRuntimeMethod(nameof(IMessagePackSerializationCallbackReceiver.OnAfterDeserialize), Type.EmptyTypes);

        private static readonly ConstructorInfo objectCtor = typeof(object).GetTypeInfo().DeclaredConstructors.First(x => x.GetParameters().Length == 0);

#pragma warning restore SA1311 // Static readonly fields should begin with upper-case letter

        /// <summary>
        ///     Helps match parameters when searching a method when the parameter is a generic type.
        /// </summary>
        private static bool Matches(MethodInfo m, int parameterIndex, Type desiredType)
        {
            var parameters = m.GetParameters();
            return parameters.Length > parameterIndex
                   ////&& parameters[0].ParameterType.IsGenericType // returns false for some bizarre reason
                   && parameters[parameterIndex].ParameterType.Name == desiredType.Name
                   && parameters[parameterIndex].ParameterType.Namespace == desiredType.Namespace;
        }

        internal static class MessagePackWriterTypeInfo
        {
            internal static readonly TypeInfo TypeInfo = typeof(MessagePackWriter).GetTypeInfo();

            internal static readonly MethodInfo WriteMapHeader = typeof(MessagePackWriter).GetRuntimeMethod(nameof(MessagePackWriter.WriteMapHeader), new[] { typeof(int) });
            internal static readonly MethodInfo WriteArrayHeader = typeof(MessagePackWriter).GetRuntimeMethod(nameof(MessagePackWriter.WriteArrayHeader), new[] { typeof(int) });
            internal static readonly MethodInfo WriteBytes = typeof(MessagePackWriter).GetRuntimeMethod(nameof(MessagePackWriter.Write), new[] { typeof(ReadOnlySpan<byte>) });
            internal static readonly MethodInfo WriteNil = typeof(MessagePackWriter).GetRuntimeMethod(nameof(MessagePackWriter.WriteNil), Type.EmptyTypes);
            internal static readonly MethodInfo WriteRaw = typeof(MessagePackWriter).GetRuntimeMethod(nameof(MessagePackWriter.WriteRaw), new[] { typeof(ReadOnlySpan<byte>) });
        }

        internal static class MessagePackReaderTypeInfo
        {
            internal static readonly TypeInfo TypeInfo = typeof(MessagePackReader).GetTypeInfo();

            internal static readonly MethodInfo ReadArrayHeader = typeof(MessagePackReader).GetRuntimeMethod(nameof(MessagePackReader.ReadArrayHeader), Type.EmptyTypes);
            internal static readonly MethodInfo ReadMapHeader = typeof(MessagePackReader).GetRuntimeMethod(nameof(MessagePackReader.ReadMapHeader), Type.EmptyTypes);
            internal static readonly MethodInfo ReadBytes = typeof(MessagePackReader).GetRuntimeMethod(nameof(MessagePackReader.ReadBytes), Type.EmptyTypes);
            internal static readonly MethodInfo TryReadNil = typeof(MessagePackReader).GetRuntimeMethod(nameof(MessagePackReader.TryReadNil), Type.EmptyTypes);
            internal static readonly MethodInfo Skip = typeof(MessagePackReader).GetRuntimeMethod(nameof(MessagePackReader.Skip), Type.EmptyTypes);
        }

        internal static class CodeGenHelpersTypeInfo
        {
            public static readonly MethodInfo GetEncodedStringBytes = typeof(CodeGenHelpers).GetRuntimeMethod(nameof(CodeGenHelpers.GetEncodedStringBytes), new[] { typeof(string) });
        }

        internal static class EmitInfo
        {
            public static readonly MethodInfo GetTypeFromHandle = ExpressionUtility.GetMethodInfo(() => Type.GetTypeFromHandle(default));
            public static readonly MethodInfo TypeGetProperty = ExpressionUtility.GetMethodInfo((Type t) => t.GetTypeInfo().GetProperty(default, default(BindingFlags)));
            public static readonly MethodInfo TypeGetField = ExpressionUtility.GetMethodInfo((Type t) => t.GetTypeInfo().GetField(default, default));
            public static readonly MethodInfo GetCustomAttributeMessagePackFormatterAttribute = ExpressionUtility.GetMethodInfo(() => default(MemberInfo).GetCustomAttribute<MessagePackFormatterAttribute>(default));
            public static readonly MethodInfo ActivatorCreateInstance = ExpressionUtility.GetMethodInfo(() => Activator.CreateInstance(default, default(object[])));

            internal static class MessagePackFormatterAttr
            {
                internal static readonly MethodInfo FormatterType = ExpressionUtility.GetPropertyInfo((MessagePackFormatterAttribute attr) => attr.FormatterType).GetGetMethod();
                internal static readonly MethodInfo Arguments = ExpressionUtility.GetPropertyInfo((MessagePackFormatterAttribute attr) => attr.Arguments).GetGetMethod();
            }
        }

        private class DeserializeInfo
        {
            public ObjectSerializationInfo.EmittableMember MemberInfo { get; set; }

            public LocalBuilder LocalVariable { get; set; }

            public LocalBuilder IsInitializedLocalVariable { get; set; }

            public Label SwitchLabel { get; set; }
        }
    }

    internal delegate void AnonymousSerializeFunc<T>(byte[][] stringByteKeysField, object[] customFormatters, ref MessagePackWriter writer, T value, MessagePackSerializerOptions options);

    internal delegate T AnonymousDeserializeFunc<T>(object[] customFormatters, ref MessagePackReader reader, MessagePackSerializerOptions options);

    internal class AnonymousSerializableFormatter<T> : IMessagePackFormatter<T>
    {
        private readonly AnonymousDeserializeFunc<T> deserialize;
        private readonly object[] deserializeCustomFormatters;
        private readonly AnonymousSerializeFunc<T> serialize;
        private readonly object[] serializeCustomFormatters;
        private readonly byte[][] stringByteKeysField;

        public AnonymousSerializableFormatter(byte[][] stringByteKeysField, object[] serializeCustomFormatters, object[] deserializeCustomFormatters, AnonymousSerializeFunc<T> serialize, AnonymousDeserializeFunc<T> deserialize)
        {
            this.stringByteKeysField = stringByteKeysField;
            this.serializeCustomFormatters = serializeCustomFormatters;
            this.deserializeCustomFormatters = deserializeCustomFormatters;
            this.serialize = serialize;
            this.deserialize = deserialize;
        }

        public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options)
        {
            if (serialize == null) throw new MessagePackSerializationException(GetType().Name + " does not support Serialize.");

            serialize(stringByteKeysField, serializeCustomFormatters, ref writer, value, options);
        }

        public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (deserialize == null) throw new MessagePackSerializationException(GetType().Name + " does not support Deserialize.");

            return deserialize(deserializeCustomFormatters, ref reader, options);
        }
    }

    internal class ObjectSerializationInfo
    {
        private ObjectSerializationInfo()
        {
        }

        public Type Type { get; set; }

        public bool IsIntKey { get; set; }

        public bool IsStringKey => !IsIntKey;

        public bool IsClass { get; set; }

        public bool IsStruct => !IsClass;

        public bool ShouldUseFormatterResolver { get; private set; }

        public ConstructorInfo BestmatchConstructor { get; set; }

        public EmittableMemberAndConstructorParameter[] ConstructorParameters { get; set; }

        public EmittableMember[] Members { get; set; }

        public static ObjectSerializationInfo CreateOrNull(Type type, bool forceStringKey, bool contractless, bool allowPrivate, bool dynamicMethod)
        {
            var ti = type.GetTypeInfo();
            var isClass = ti.IsClass || ti.IsInterface || ti.IsAbstract;
            var isClassRecord = isClass && IsClassRecord(ti);
            var isStruct = ti.IsValueType;

            var contractAttr = ti.GetCustomAttributes<MessagePackObjectAttribute>().FirstOrDefault();
            var dataContractAttr = ti.GetCustomAttribute<DataContractAttribute>();
            if (contractAttr == null && dataContractAttr == null && !forceStringKey && !contractless) return null;

            var isIntKey = true;
            var intMembers = new Dictionary<int, EmittableMember>();
            var stringMembers = new Dictionary<string, EmittableMember>();

            // When returning false, it means should ignoring this member.
            bool AddEmittableMemberOrIgnore(bool isIntKeyMode, EmittableMember member, bool checkConflicting)
            {
                if (checkConflicting)
                    if (isIntKeyMode ? intMembers.TryGetValue(member.IntKey, out var conflictingMember) : stringMembers.TryGetValue(member.StringKey, out conflictingMember))
                    {
                        // Quietly skip duplicate if this is an override property.
                        if (member.PropertyInfo != null && ((conflictingMember.PropertyInfo.SetMethod?.IsVirtual ?? false) || (conflictingMember.PropertyInfo.GetMethod?.IsVirtual ?? false))) return false;

                        var memberInfo = (MemberInfo)member.PropertyInfo ?? member.FieldInfo;
                        throw new MessagePackDynamicObjectResolverException($"key is duplicated, all members key must be unique. type:{type.FullName} member:{memberInfo.Name}");
                    }

                if (isIntKeyMode)
                    intMembers.Add(member.IntKey, member);
                else
                    stringMembers.Add(member.StringKey, member);

                return true;
            }

            EmittableMember CreateEmittableMember(MemberInfo m)
            {
                if (m.IsDefined(typeof(IgnoreMemberAttribute), true) || m.IsDefined(typeof(IgnoreDataMemberAttribute), true)) return null;

                EmittableMember result;
                switch (m)
                {
                    case PropertyInfo property:
                        if (property.IsIndexer()) return null;

                        if (isClassRecord && property.Name == "EqualityContract") return null;

                        var getMethod = property.GetGetMethod(true);
                        var setMethod = property.GetSetMethod(true);
                        result = new EmittableMember(dynamicMethod)
                        {
                            PropertyInfo = property,
                            IsReadable = getMethod != null && (allowPrivate || getMethod.IsPublic) && !getMethod.IsStatic,
                            IsWritable = setMethod != null && (allowPrivate || setMethod.IsPublic) && !setMethod.IsStatic
                        };
                        break;
                    case FieldInfo field:
                        if (field.GetCustomAttribute<CompilerGeneratedAttribute>(true) != null) return null;

                        if (field.IsStatic) return null;

                        result = new EmittableMember(dynamicMethod)
                        {
                            FieldInfo = field,
                            IsReadable = allowPrivate || field.IsPublic,
                            IsWritable = allowPrivate || (field.IsPublic && !field.IsInitOnly)
                        };
                        break;
                    default:
                        throw new MessagePackSerializationException("unexpected member type");
                }

                return result.IsReadable || result.IsWritable ? result : null;
            }

            // Determine whether to ignore MessagePackObjectAttribute or DataContract.
            if (forceStringKey || contractless || contractAttr?.KeyAsPropertyName == true)
            {
                // All public members are serialize target except [Ignore] member.
                isIntKey = !(forceStringKey || (contractAttr != null && contractAttr.KeyAsPropertyName));
                var hiddenIntKey = 0;

                // Group the properties and fields by name to qualify members of the same name
                // (declared with the 'new' keyword) with the declaring type.
                var membersByName = type.GetRuntimeProperties().Concat(type.GetRuntimeFields().Cast<MemberInfo>())
                    .OrderBy(m => m.DeclaringType, OrderBaseTypesBeforeDerivedTypes.Instance)
                    .GroupBy(m => m.Name);
                foreach (var memberGroup in membersByName)
                {
                    var first = true;
                    foreach (var member in memberGroup.Select(CreateEmittableMember).Where(n => n != null))
                    {
                        var memberInfo = (MemberInfo)member.PropertyInfo ?? member.FieldInfo;
                        if (first)
                        {
                            first = false;
                            member.StringKey = memberInfo.Name;
                        }
                        else
                        {
                            member.StringKey = $"{memberInfo.DeclaringType.FullName}.{memberInfo.Name}";
                        }

                        member.IntKey = hiddenIntKey++;
                        AddEmittableMemberOrIgnore(isIntKey, member, false);
                    }
                }
            }
            else
            {
                // Public members with KeyAttribute except [Ignore] member.
                var searchFirst = true;
                var hiddenIntKey = 0;

                var memberInfos = GetAllProperties(type).Cast<MemberInfo>().Concat(GetAllFields(type));
                foreach (var member in memberInfos.Select(CreateEmittableMember).Where(n => n != null))
                {
                    var memberInfo = (MemberInfo)member.PropertyInfo ?? member.FieldInfo;

                    KeyAttribute key;
                    if (contractAttr != null)
                    {
                        // MessagePackObjectAttribute. KeyAttribute must be marked, and IntKey or StringKey must be set.
                        key = memberInfo.GetCustomAttribute<KeyAttribute>(true) ??
                              throw new MessagePackDynamicObjectResolverException($"all public members must mark KeyAttribute or IgnoreMemberAttribute. type:{type.FullName} member:{memberInfo.Name}");
                        if (key.IntKey == null && key.StringKey == null) throw new MessagePackDynamicObjectResolverException($"both IntKey and StringKey are null. type: {type.FullName} member:{memberInfo.Name}");
                    }
                    else
                    {
                        // DataContractAttribute. Try to use the DataMemberAttribute to fake KeyAttribute.
                        // This member has no DataMemberAttribute nor IgnoreMemberAttribute.
                        // But the type *did* have a DataContractAttribute on it, so no attribute implies the member should not be serialized.
                        var pseudokey = memberInfo.GetCustomAttribute<DataMemberAttribute>(true);
                        if (pseudokey == null) continue;

                        key =
                            pseudokey.Order != -1 ? new KeyAttribute(pseudokey.Order) :
                            pseudokey.Name != null ? new KeyAttribute(pseudokey.Name) :
                            new KeyAttribute(memberInfo.Name);
                    }

                    member.IsExplicitContract = true;

                    // Cannot assign StringKey and IntKey at the same time.
                    if (searchFirst)
                    {
                        searchFirst = false;
                        isIntKey = key.IntKey != null;
                    }
                    else if ((isIntKey && key.IntKey == null) || (!isIntKey && key.StringKey == null))
                    {
                        throw new MessagePackDynamicObjectResolverException($"all members key type must be same. type: {type.FullName} member:{memberInfo.Name}");
                    }

                    if (isIntKey)
                    {
                        member.IntKey = key.IntKey.Value;
                    }
                    else
                    {
                        member.StringKey = key.StringKey;
                        member.IntKey = hiddenIntKey++;
                    }

                    if (!AddEmittableMemberOrIgnore(isIntKey, member, true)) continue;
                }
            }

            // GetConstructor
            IEnumerator<ConstructorInfo> ctorEnumerator = null;
            var ctor = ti.DeclaredConstructors.SingleOrDefault(x => x.GetCustomAttribute<SerializationConstructorAttribute>(false) != null);
            if (ctor == null)
            {
                ctorEnumerator =
                    ti.DeclaredConstructors.Where(x => !x.IsStatic && (allowPrivate || x.IsPublic)).OrderByDescending(x => x.GetParameters().Length)
                        .GetEnumerator();

                if (ctorEnumerator.MoveNext()) ctor = ctorEnumerator.Current;
            }

            // struct allows null ctor
            if (ctor == null && !isStruct) throw new MessagePackDynamicObjectResolverException("can't find public constructor. type:" + type.FullName);

            var constructorParameters = new List<EmittableMemberAndConstructorParameter>();
            if (ctor != null)
            {
                IReadOnlyDictionary<int, EmittableMember> ctorParamIndexIntMembersDictionary = intMembers.OrderBy(x => x.Key).Select((x, i) => (Key: x.Value, Index: i)).ToDictionary(x => x.Index, x => x.Key);
                var constructorLookupByKeyDictionary = stringMembers.ToLookup(x => x.Key, x => x, StringComparer.OrdinalIgnoreCase);
                var constructorLookupByMemberNameDictionary = stringMembers.ToLookup(x => x.Value.Name, x => x, StringComparer.OrdinalIgnoreCase);
                do
                {
                    constructorParameters.Clear();
                    var ctorParamIndex = 0;
                    foreach (var item in ctor.GetParameters())
                    {
                        EmittableMember paramMember;
                        if (isIntKey)
                        {
                            if (ctorParamIndexIntMembersDictionary.TryGetValue(ctorParamIndex, out paramMember))
                            {
                                if ((item.ParameterType == paramMember.Type ||
                                     item.ParameterType.GetTypeInfo().IsAssignableFrom(paramMember.Type))
                                    && paramMember.IsReadable)
                                {
                                    constructorParameters.Add(new EmittableMemberAndConstructorParameter { ConstructorParameter = item, MemberInfo = paramMember });
                                }
                                else
                                {
                                    if (ctorEnumerator != null)
                                    {
                                        ctor = null;
                                        break;
                                    }

                                    throw new MessagePackDynamicObjectResolverException("can't find matched constructor parameter, parameterType mismatch. type:" + type.FullName + " parameterIndex:" + ctorParamIndex + " paramterType:" + item.ParameterType.Name);
                                }
                            }
                            else
                            {
                                if (ctorEnumerator != null)
                                {
                                    ctor = null;
                                    break;
                                }

                                throw new MessagePackDynamicObjectResolverException("can't find matched constructor parameter, index not found. type:" + type.FullName + " parameterIndex:" + ctorParamIndex);
                            }
                        }
                        else
                        {
                            // Lookup by both string key name and member name
                            var hasKey = constructorLookupByKeyDictionary[item.Name];
                            var hasKeyByMemberName = constructorLookupByMemberNameDictionary[item.Name];

                            var lenByKey = hasKey.Count();
                            var lenByMemberName = hasKeyByMemberName.Count();

                            var len = lenByKey;

                            // Prefer to use string key name unless a matching string key is not found but a matching member name is
                            if (lenByKey == 0 && lenByMemberName != 0)
                            {
                                len = lenByMemberName;
                                hasKey = hasKeyByMemberName;
                            }

                            if (len != 0)
                            {
                                if (len != 1)
                                {
                                    if (ctorEnumerator != null)
                                    {
                                        ctor = null;
                                        break;
                                    }

                                    throw new MessagePackDynamicObjectResolverException("duplicate matched constructor parameter name:" + type.FullName + " parameterName:" + item.Name + " paramterType:" + item.ParameterType.Name);
                                }

                                paramMember = hasKey.First().Value;
                                if (item.ParameterType.IsAssignableFrom(paramMember.Type) && paramMember.IsReadable)
                                {
                                    constructorParameters.Add(new EmittableMemberAndConstructorParameter { ConstructorParameter = item, MemberInfo = paramMember });
                                }
                                else
                                {
                                    if (ctorEnumerator != null)
                                    {
                                        ctor = null;
                                        break;
                                    }

                                    throw new MessagePackDynamicObjectResolverException("can't find matched constructor parameter, parameterType mismatch. type:" + type.FullName + " parameterName:" + item.Name + " paramterType:" + item.ParameterType.Name);
                                }
                            }
                            else
                            {
                                if (ctorEnumerator != null)
                                {
                                    ctor = null;
                                    break;
                                }

                                throw new MessagePackDynamicObjectResolverException("can't find matched constructor parameter, index not found. type:" + type.FullName + " parameterName:" + item.Name);
                            }
                        }

                        ctorParamIndex++;
                    }
                } while (TryGetNextConstructor(ctorEnumerator, ref ctor));

                if (ctor == null) throw new MessagePackDynamicObjectResolverException("can't find matched constructor. type:" + type.FullName);
            }

            EmittableMember[] members;
            if (isIntKey)
                members = intMembers.Values.OrderBy(x => x.IntKey).ToArray();
            else
                members = stringMembers.Values
                    .OrderBy(x =>
                    {
                        var attr = x.GetDataMemberAttribute();
                        if (attr == null) return int.MaxValue;

                        return attr.Order;
                    })
                    .ToArray();

            var shouldUseFormatterResolver = false;

            // Mark each member that will be set by way of the constructor.
            foreach (var item in constructorParameters) item.MemberInfo.IsWrittenByConstructor = true;

            var membersArray = members.Where(m => m.IsExplicitContract || m.IsWrittenByConstructor || m.IsWritable).ToArray();
            foreach (var member in membersArray)
            {
                if (IsOptimizeTargetType(member.Type)) continue;

                var attr = member.GetMessagePackFormatterAttribute();
                if (!(attr is null)) continue;

                shouldUseFormatterResolver = true;
                break;
            }

            // Under a certain combination of conditions, throw to draw attention to the fact that we cannot set a property.
            if (!allowPrivate)
            {
                // A property is not actually problematic if we can set it via the type's constructor.
                var problematicProperties = membersArray
                    .Where(m => m.IsProblematicInitProperty && !constructorParameters.Any(cp => cp.MemberInfo == m));
                problematicProperties.FirstOrDefault()?.ThrowIfNotWritable();
            }

            return new ObjectSerializationInfo
            {
                Type = type,
                IsClass = isClass,
                ShouldUseFormatterResolver = shouldUseFormatterResolver,
                BestmatchConstructor = ctor,
                ConstructorParameters = constructorParameters.ToArray(),
                IsIntKey = isIntKey,
                Members = membersArray
            };
        }

        /// <devremarks>
        ///     Keep this list in sync with ShouldUseFormatterResolverHelper.PrimitiveTypes.
        /// </devremarks>
        internal static bool IsOptimizeTargetType(Type type)
        {
            return type == typeof(short)
                   || type == typeof(int)
                   || type == typeof(long)
                   || type == typeof(ushort)
                   || type == typeof(uint)
                   || type == typeof(ulong)
                   || type == typeof(float)
                   || type == typeof(double)
                   || type == typeof(bool)
                   || type == typeof(byte)
                   || type == typeof(sbyte)
                   || type == typeof(char)
                   || type == typeof(byte[])

                // Do not include types that resolvers are allowed to modify.
                ////|| type == typeof(DateTime) // OldSpec has no support, so for that and perf reasons a .NET native DateTime resolver exists.
                ////|| type == typeof(string) // https://github.com/Cysharp/MasterMemory provides custom formatter for string interning.
                ;
        }

        private static IEnumerable<FieldInfo> GetAllFields(Type type)
        {
            if (type.BaseType is object)
                foreach (var item in GetAllFields(type.BaseType))
                    yield return item;

            // with declared only
            foreach (var item in type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) yield return item;
        }

        private static IEnumerable<PropertyInfo> GetAllProperties(Type type)
        {
            if (type.BaseType is object)
                foreach (var item in GetAllProperties(type.BaseType))
                    yield return item;

            // with declared only
            foreach (var item in type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) yield return item;
        }

        private static bool IsClassRecord(TypeInfo type)
        {
            // The only truly unique thing about a C# 9 record class is the presence of a <Clone>$ method,
            // which cannot be declared in C# because of the reserved characters in its name.
            return type.IsClass
                   && type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is object;
        }

        private static bool TryGetNextConstructor(IEnumerator<ConstructorInfo> ctorEnumerator, ref ConstructorInfo ctor)
        {
            if (ctorEnumerator == null || ctor != null) return false;

            if (ctorEnumerator.MoveNext())
            {
                ctor = ctorEnumerator.Current;
                return true;
            }

            ctor = null;
            return false;
        }

        public class EmittableMemberAndConstructorParameter
        {
            public EmittableMember MemberInfo { get; set; }

            public ParameterInfo ConstructorParameter { get; set; }
        }

        public class EmittableMember
        {
            private readonly bool dynamicMethod;

            internal EmittableMember(bool dynamicMethod)
            {
                this.dynamicMethod = dynamicMethod;
            }

            public bool IsProperty => PropertyInfo != null;

            public bool IsField => FieldInfo != null;

            public bool IsWritable { get; set; }

            public bool IsWrittenByConstructor { get; set; }

            /// <summary>
            ///     Gets a value indicating whether the property can only be set by an object initializer, a constructor, or another
            ///     `init` member.
            /// </summary>
            public bool IsInitOnly => PropertyInfo?.GetSetMethod(true)?.ReturnParameter.GetRequiredCustomModifiers().Any(modifierType => modifierType.FullName == "System.Runtime.CompilerServices.IsExternalInit") ?? false;

            public bool IsReadable { get; set; }

            public int IntKey { get; set; }

            public string StringKey { get; set; }

            public Type Type => IsField ? FieldInfo.FieldType : PropertyInfo.PropertyType;

            public FieldInfo FieldInfo { get; set; }

            public PropertyInfo PropertyInfo { get; set; }

            public string Name => IsProperty ? PropertyInfo.Name : FieldInfo.Name;

            public bool IsValueType
            {
                get
                {
                    var t = IsProperty ? PropertyInfo.PropertyType : FieldInfo.FieldType;
                    return t.IsValueType;
                }
            }

            /// <summary>
            ///     Gets or sets a value indicating whether this member is explicitly opted in with an attribute.
            /// </summary>
            public bool IsExplicitContract { get; set; }

            /// <summary>
            ///     Gets a value indicating whether a dynamic resolver can write to this property,
            ///     going beyond <see cref="IsWritable" /> by also considering CLR bugs.
            /// </summary>
            internal bool IsActuallyWritable => IsWritable && (dynamicMethod || !IsProblematicInitProperty);

            /// <summary>
            ///     Gets a value indicating whether this member is a property with an <see langword="init" /> property setter
            ///     and is declared on a generic class.
            /// </summary>
            /// <remarks>
            ///     <see href="https://github.com/neuecc/MessagePack-CSharp/issues/1134">A bug</see> in <see cref="MethodBuilder" />
            ///     blocks its ability to invoke property init accessors when in a generic class.
            /// </remarks>
            internal bool IsProblematicInitProperty => PropertyInfo is PropertyInfo property && property.DeclaringType.IsGenericType && IsInitOnly;

            public MessagePackFormatterAttribute GetMessagePackFormatterAttribute()
            {
                if (IsProperty)
                    return PropertyInfo.GetCustomAttribute<MessagePackFormatterAttribute>(true);
                return FieldInfo.GetCustomAttribute<MessagePackFormatterAttribute>(true);
            }

            public DataMemberAttribute GetDataMemberAttribute()
            {
                if (IsProperty)
                    return PropertyInfo.GetCustomAttribute<DataMemberAttribute>(true);
                return FieldInfo.GetCustomAttribute<DataMemberAttribute>(true);
            }

            public void EmitLoadValue(ILGenerator il)
            {
                if (IsProperty)
                    il.EmitCall(PropertyInfo.GetGetMethod(true));
                else
                    il.Emit(OpCodes.Ldfld, FieldInfo);
            }

            public void EmitStoreValue(ILGenerator il)
            {
                if (IsProperty)
                    il.EmitCall(PropertyInfo.GetSetMethod(true));
                else
                    il.Emit(OpCodes.Stfld, FieldInfo);
            }

            internal void ThrowIfNotWritable()
            {
                if (IsProblematicInitProperty && !dynamicMethod)
                    throw new InitAccessorInGenericClassNotSupportedException(
                        $"`init` property accessor {PropertyInfo.SetMethod.DeclaringType.FullName}.{PropertyInfo.Name} found in generic type, " +
                        "which is not supported with the DynamicObjectResolver. Use the AllowPrivate variety of the resolver instead. " +
                        "See https://github.com/neuecc/MessagePack-CSharp/issues/1134 for details.");
            }

            ////public object ReflectionLoadValue(object value)
            ////{
            ////    if (IsProperty)
            ////    {
            ////        return PropertyInfo.GetValue(value, null);
            ////    }
            ////    else
            ////    {
            ////        return FieldInfo.GetValue(value);
            ////    }
            ////}

            ////public void ReflectionStoreValue(object obj, object value)
            ////{
            ////    if (IsProperty)
            ////    {
            ////        PropertyInfo.SetValue(obj, value, null);
            ////    }
            ////    else
            ////    {
            ////        FieldInfo.SetValue(obj, value);
            ////    }
            ////}
        }

        private class OrderBaseTypesBeforeDerivedTypes : IComparer<Type>
        {
            internal static readonly OrderBaseTypesBeforeDerivedTypes Instance = new();

            private OrderBaseTypesBeforeDerivedTypes()
            {
            }

            public int Compare(Type x, Type y)
            {
                return
                    x.IsEquivalentTo(y) ? 0 :
                    x.IsAssignableFrom(y) ? -1 :
                    y.IsAssignableFrom(x) ? 1 :
                    0;
            }
        }
    }

    internal class MessagePackDynamicObjectResolverException : MessagePackSerializationException
    {
        public MessagePackDynamicObjectResolverException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    ///     Identifies the unsupported scenario of
    ///     <see href="https://github.com/neuecc/MessagePack-CSharp/issues/1134">
    ///         an
    ///         <see langword="init" /> property accessor within a generic class
    ///     </see>
    ///     .
    /// </summary>
    [Serializable]
    internal class InitAccessorInGenericClassNotSupportedException : NotSupportedException
    {
        public InitAccessorInGenericClassNotSupportedException()
        {
        }

        public InitAccessorInGenericClassNotSupportedException(string message)
            : base(message)
        {
        }

        public InitAccessorInGenericClassNotSupportedException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected InitAccessorInGenericClassNotSupportedException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }
}

#endif