// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MessagePack
{
#if MESSAGEPACK_INTERNAL
    internal
#else
    public
#endif
        struct ExtensionHeader : IEquatable<ExtensionHeader>
    {
        public sbyte TypeCode { get; }

        public uint Length { get; }

        public ExtensionHeader(sbyte typeCode, uint length)
        {
            TypeCode = typeCode;
            Length = length;
        }

        public ExtensionHeader(sbyte typeCode, int length)
        {
            TypeCode = typeCode;
            Length = (uint)length;
        }

        public bool Equals(ExtensionHeader other)
        {
            return TypeCode == other.TypeCode && Length == other.Length;
        }
    }
}