// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace MessagePack.Formatters
{
    // Note:This implementation is 'not' fastest, should more improve.
    public sealed class EnumAsStringFormatter<T> : IMessagePackFormatter<T>
    {
        private readonly IReadOnlyDictionary<string, string> clrToSerializationName;
        private readonly bool enumMemberOverridesPresent;
        private readonly bool isFlags;
        private readonly IReadOnlyDictionary<string, T> nameValueMapping;
        private readonly IReadOnlyDictionary<string, string> serializationToClrName;
        private readonly IReadOnlyDictionary<T, string> valueNameMapping;

        public EnumAsStringFormatter()
        {
            isFlags = typeof(T).GetCustomAttribute<FlagsAttribute>() is object;

            var fields = typeof(T).GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static);
            var nameValueMapping = new Dictionary<string, T>(fields.Length);
            var valueNameMapping = new Dictionary<T, string>();
            Dictionary<string, string> clrToSerializationName = null;
            Dictionary<string, string> serializationToClrName = null;

            foreach (var enumValueMember in fields)
            {
                var name = enumValueMember.Name;
                var value = (T)enumValueMember.GetValue(null);

                // Consider the case where the serialized form of the enum value is overridden via an attribute.
                var attribute = enumValueMember.GetCustomAttribute<EnumMemberAttribute>();
                if (attribute?.IsValueSetExplicitly ?? false)
                {
                    clrToSerializationName = clrToSerializationName ?? new Dictionary<string, string>();
                    serializationToClrName = serializationToClrName ?? new Dictionary<string, string>();

                    clrToSerializationName.Add(name, attribute.Value);
                    serializationToClrName.Add(attribute.Value, name);

                    name = attribute.Value;
                    enumMemberOverridesPresent = true;
                }

                nameValueMapping[name] = value;
                valueNameMapping[value] = name;
            }

            this.nameValueMapping = nameValueMapping;
            this.valueNameMapping = valueNameMapping;
            this.clrToSerializationName = clrToSerializationName;
            this.serializationToClrName = serializationToClrName;
        }

        public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options)
        {
            // Enum.ToString() is slow, so avoid it when we can.
            if (!valueNameMapping.TryGetValue(value, out var valueString))
                // fallback for flags, values with no name, etc
                valueString = GetSerializedNames(value.ToString());

            writer.Write(valueString);
        }

        public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var name = reader.ReadString();

            // Avoid Enum.Parse when we can because it is too slow.
            if (!nameValueMapping.TryGetValue(name, out var value)) value = (T)Enum.Parse(typeof(T), GetClrNames(name));

            return value;
        }

        private string GetClrNames(string serializedNames)
        {
            if (enumMemberOverridesPresent && isFlags && serializedNames.IndexOf(", ", StringComparison.Ordinal) >= 0) return Translate(serializedNames, serializationToClrName);

            // We don't need to consider the trivial case of no commas because our caller would have found that in the lookup table and not called us.
            return serializedNames;
        }

        private string GetSerializedNames(string clrNames)
        {
            if (enumMemberOverridesPresent && isFlags && clrNames.IndexOf(", ", StringComparison.Ordinal) >= 0) return Translate(clrNames, clrToSerializationName);

            // We don't need to consider the trivial case of no commas because our caller would have found that in the lookup table and not called us.
            return clrNames;
        }

        private static string Translate(string items, IReadOnlyDictionary<string, string> mapping)
        {
            var elements = items.Split(',');

            for (var i = 0; i < elements.Length; i++)
            {
                // Trim the leading space if there is one (due to the delimiter being ", ").
                if (i > 0 && elements[i].Length > 0 && elements[i][0] == ' ') elements[i] = elements[i].Substring(1);

                if (mapping.TryGetValue(elements[i], out var substituteValue)) elements[i] = substituteValue;
            }

            return string.Join(", ", elements);
        }
    }
}