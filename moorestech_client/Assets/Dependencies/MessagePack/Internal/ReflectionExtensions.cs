﻿// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MessagePack.Internal
{
    internal static class ReflectionExtensions
    {
        public static bool IsNullable(this TypeInfo type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static bool IsPublic(this TypeInfo type)
        {
            return type.IsPublic;
        }

        public static bool IsAnonymous(this TypeInfo type)
        {
            return type.Namespace == null
                   && type.IsSealed
                   && (type.Name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal)
                       || type.Name.StartsWith("<>__AnonType", StringComparison.Ordinal)
                       || type.Name.StartsWith("VB$AnonymousType_", StringComparison.Ordinal))
                   && type.IsDefined(typeof(CompilerGeneratedAttribute), false);
        }

        public static bool IsIndexer(this PropertyInfo propertyInfo)
        {
            return propertyInfo.GetIndexParameters().Length > 0;
        }

        public static bool IsConstructedGenericType(this TypeInfo type)
        {
            return type.AsType().IsConstructedGenericType;
        }

        public static MethodInfo GetGetMethod(this PropertyInfo propInfo)
        {
            return propInfo.GetMethod;
        }

        public static MethodInfo GetSetMethod(this PropertyInfo propInfo)
        {
            return propInfo.SetMethod;
        }

        public static bool HasPrivateCtorForSerialization(this TypeInfo type)
        {
            var markedCtor = type.DeclaredConstructors.SingleOrDefault(x => x.GetCustomAttribute<SerializationConstructorAttribute>(false) != null);
            return markedCtor?.Attributes.HasFlag(MethodAttributes.Private) ?? false;
        }
    }
}