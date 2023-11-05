// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;

namespace MessagePack
{
#if MESSAGEPACK_INTERNAL
    internal
#else
    public
#endif
        struct ExtensionResult
    {
        public ExtensionResult(sbyte typeCode, Memory<byte> data)
        {
            TypeCode = typeCode;
            Data = new ReadOnlySequence<byte>(data);
        }

        public ExtensionResult(sbyte typeCode, ReadOnlySequence<byte> data)
        {
            TypeCode = typeCode;
            Data = data;
        }

        public sbyte TypeCode { get; }

        public ReadOnlySequence<byte> Data { get; }

        public ExtensionHeader Header => new(TypeCode, (uint)Data.Length);
    }
}