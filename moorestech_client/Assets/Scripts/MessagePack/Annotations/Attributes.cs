// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

#pragma warning disable SA1649 // File name should match first type name

namespace MessagePack
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class MessagePackObjectAttribute : Attribute
    {
        public MessagePackObjectAttribute(bool keyAsPropertyName = false)
        {
            KeyAsPropertyName = keyAsPropertyName;
        }

        public bool KeyAsPropertyName { get; private set; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class KeyAttribute : Attribute
    {
        public KeyAttribute(int x)
        {
            IntKey = x;
        }

        public KeyAttribute(string x)
        {
            StringKey = x;
        }

        public int? IntKey { get; private set; }

        public string StringKey { get; private set; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class IgnoreMemberAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class UnionAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="UnionAttribute" /> class.
        /// </summary>
        /// <param name="key">The distinguishing value that identifies a particular subtype.</param>
        /// <param name="subType">The derived or implementing type.</param>
        public UnionAttribute(int key, Type subType)
        {
            Key = key;
            SubType = subType;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="UnionAttribute" /> class.
        /// </summary>
        /// <param name="key">The distinguishing value that identifies a particular subtype.</param>
        /// <param name="subType">The full name (should be assembly qualified) of the derived or implementing type.</param>
        public UnionAttribute(int key, string subType)
        {
            Key = key;
            SubType = Type.GetType(subType, true);
        }

        /// <summary>
        ///     Gets the distinguishing value that identifies a particular subtype.
        /// </summary>
        public int Key { get; private set; }

        /// <summary>
        ///     Gets the derived or implementing type.
        /// </summary>
        public Type SubType { get; private set; }
    }

    [AttributeUsage(AttributeTargets.Constructor)]
    public class SerializationConstructorAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Field | AttributeTargets.Property)]
    public class MessagePackFormatterAttribute : Attribute
    {
        public MessagePackFormatterAttribute(Type formatterType)
        {
            FormatterType = formatterType;
        }

        public MessagePackFormatterAttribute(Type formatterType, params object[] arguments)
        {
            FormatterType = formatterType;
            Arguments = arguments;
        }

        public Type FormatterType { get; private set; }

        public object[] Arguments { get; private set; }
    }
}