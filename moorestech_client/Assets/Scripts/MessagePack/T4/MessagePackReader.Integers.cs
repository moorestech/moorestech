// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/* THIS (.cs) FILE IS GENERATED. DO NOT CHANGE IT.
 * CHANGE THE .tt FILE INSTEAD. */

using System;

namespace MessagePack
{
#pragma warning disable SA1205 // Partial elements should declare access
    ref partial struct MessagePackReader
#pragma warning restore SA1205 // Partial elements should declare access
    {
        /// <summary>
        ///     Reads an <see cref="byte" /> value from:
        ///     Some value between <see cref="MessagePackCode.MinNegativeFixInt" /> and
        ///     <see cref="MessagePackCode.MaxNegativeFixInt" />,
        ///     Some value between <see cref="MessagePackCode.MinFixInt" /> and <see cref="MessagePackCode.MaxFixInt" />,
        ///     or any of the other MsgPack integer types.
        /// </summary>
        /// <returns>The value.</returns>
        /// <exception cref="OverflowException">Thrown when the value exceeds what can be stored in the returned type.</exception>
        public byte ReadByte()
        {
            ThrowInsufficientBufferUnless(reader.TryRead(out var code));

            switch (code)
            {
                case MessagePackCode.UInt8:
                    ThrowInsufficientBufferUnless(reader.TryRead(out var byteResult));
                    return byteResult;
                case MessagePackCode.Int8:
                    ThrowInsufficientBufferUnless(reader.TryRead(out sbyte sbyteResult));
                    return checked((byte)sbyteResult);
                case MessagePackCode.UInt16:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out ushort ushortResult));
                    return checked((byte)ushortResult);
                case MessagePackCode.Int16:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out short shortResult));
                    return checked((byte)shortResult);
                case MessagePackCode.UInt32:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out uint uintResult));
                    return checked((byte)uintResult);
                case MessagePackCode.Int32:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out int intResult));
                    return checked((byte)intResult);
                case MessagePackCode.UInt64:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out ulong ulongResult));
                    return checked((byte)ulongResult);
                case MessagePackCode.Int64:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out long longResult));
                    return checked((byte)longResult);
                default:
                    if (code >= MessagePackCode.MinNegativeFixInt && code <= MessagePackCode.MaxNegativeFixInt) return checked((byte)unchecked((sbyte)code));

                    if (code >= MessagePackCode.MinFixInt && code <= MessagePackCode.MaxFixInt) return code;

                    throw ThrowInvalidCode(code);
            }
        }

        /// <summary>
        ///     Reads an <see cref="UInt16" /> value from:
        ///     Some value between <see cref="MessagePackCode.MinNegativeFixInt" /> and
        ///     <see cref="MessagePackCode.MaxNegativeFixInt" />,
        ///     Some value between <see cref="MessagePackCode.MinFixInt" /> and <see cref="MessagePackCode.MaxFixInt" />,
        ///     or any of the other MsgPack integer types.
        /// </summary>
        /// <returns>The value.</returns>
        /// <exception cref="OverflowException">Thrown when the value exceeds what can be stored in the returned type.</exception>
        public ushort ReadUInt16()
        {
            ThrowInsufficientBufferUnless(reader.TryRead(out var code));

            switch (code)
            {
                case MessagePackCode.UInt8:
                    ThrowInsufficientBufferUnless(reader.TryRead(out var byteResult));
                    return byteResult;
                case MessagePackCode.Int8:
                    ThrowInsufficientBufferUnless(reader.TryRead(out sbyte sbyteResult));
                    return checked((ushort)sbyteResult);
                case MessagePackCode.UInt16:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out ushort ushortResult));
                    return ushortResult;
                case MessagePackCode.Int16:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out short shortResult));
                    return checked((ushort)shortResult);
                case MessagePackCode.UInt32:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out uint uintResult));
                    return checked((ushort)uintResult);
                case MessagePackCode.Int32:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out int intResult));
                    return checked((ushort)intResult);
                case MessagePackCode.UInt64:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out ulong ulongResult));
                    return checked((ushort)ulongResult);
                case MessagePackCode.Int64:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out long longResult));
                    return checked((ushort)longResult);
                default:
                    if (code >= MessagePackCode.MinNegativeFixInt && code <= MessagePackCode.MaxNegativeFixInt) return checked((ushort)unchecked((sbyte)code));

                    if (code >= MessagePackCode.MinFixInt && code <= MessagePackCode.MaxFixInt) return code;

                    throw ThrowInvalidCode(code);
            }
        }

        /// <summary>
        ///     Reads an <see cref="UInt32" /> value from:
        ///     Some value between <see cref="MessagePackCode.MinNegativeFixInt" /> and
        ///     <see cref="MessagePackCode.MaxNegativeFixInt" />,
        ///     Some value between <see cref="MessagePackCode.MinFixInt" /> and <see cref="MessagePackCode.MaxFixInt" />,
        ///     or any of the other MsgPack integer types.
        /// </summary>
        /// <returns>The value.</returns>
        /// <exception cref="OverflowException">Thrown when the value exceeds what can be stored in the returned type.</exception>
        public uint ReadUInt32()
        {
            ThrowInsufficientBufferUnless(reader.TryRead(out var code));

            switch (code)
            {
                case MessagePackCode.UInt8:
                    ThrowInsufficientBufferUnless(reader.TryRead(out var byteResult));
                    return byteResult;
                case MessagePackCode.Int8:
                    ThrowInsufficientBufferUnless(reader.TryRead(out sbyte sbyteResult));
                    return checked((uint)sbyteResult);
                case MessagePackCode.UInt16:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out ushort ushortResult));
                    return ushortResult;
                case MessagePackCode.Int16:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out short shortResult));
                    return checked((uint)shortResult);
                case MessagePackCode.UInt32:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out uint uintResult));
                    return uintResult;
                case MessagePackCode.Int32:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out int intResult));
                    return checked((uint)intResult);
                case MessagePackCode.UInt64:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out ulong ulongResult));
                    return checked((uint)ulongResult);
                case MessagePackCode.Int64:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out long longResult));
                    return checked((uint)longResult);
                default:
                    if (code >= MessagePackCode.MinNegativeFixInt && code <= MessagePackCode.MaxNegativeFixInt) return checked((uint)unchecked((sbyte)code));

                    if (code >= MessagePackCode.MinFixInt && code <= MessagePackCode.MaxFixInt) return code;

                    throw ThrowInvalidCode(code);
            }
        }

        /// <summary>
        ///     Reads an <see cref="UInt64" /> value from:
        ///     Some value between <see cref="MessagePackCode.MinNegativeFixInt" /> and
        ///     <see cref="MessagePackCode.MaxNegativeFixInt" />,
        ///     Some value between <see cref="MessagePackCode.MinFixInt" /> and <see cref="MessagePackCode.MaxFixInt" />,
        ///     or any of the other MsgPack integer types.
        /// </summary>
        /// <returns>The value.</returns>
        /// <exception cref="OverflowException">Thrown when the value exceeds what can be stored in the returned type.</exception>
        public ulong ReadUInt64()
        {
            ThrowInsufficientBufferUnless(reader.TryRead(out var code));

            switch (code)
            {
                case MessagePackCode.UInt8:
                    ThrowInsufficientBufferUnless(reader.TryRead(out var byteResult));
                    return byteResult;
                case MessagePackCode.Int8:
                    ThrowInsufficientBufferUnless(reader.TryRead(out sbyte sbyteResult));
                    return checked((ulong)sbyteResult);
                case MessagePackCode.UInt16:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out ushort ushortResult));
                    return ushortResult;
                case MessagePackCode.Int16:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out short shortResult));
                    return checked((ulong)shortResult);
                case MessagePackCode.UInt32:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out uint uintResult));
                    return uintResult;
                case MessagePackCode.Int32:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out int intResult));
                    return checked((ulong)intResult);
                case MessagePackCode.UInt64:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out ulong ulongResult));
                    return ulongResult;
                case MessagePackCode.Int64:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out long longResult));
                    return checked((ulong)longResult);
                default:
                    if (code >= MessagePackCode.MinNegativeFixInt && code <= MessagePackCode.MaxNegativeFixInt) return checked((ulong)unchecked((sbyte)code));

                    if (code >= MessagePackCode.MinFixInt && code <= MessagePackCode.MaxFixInt) return code;

                    throw ThrowInvalidCode(code);
            }
        }

        /// <summary>
        ///     Reads an <see cref="SByte" /> value from:
        ///     Some value between <see cref="MessagePackCode.MinNegativeFixInt" /> and
        ///     <see cref="MessagePackCode.MaxNegativeFixInt" />,
        ///     Some value between <see cref="MessagePackCode.MinFixInt" /> and <see cref="MessagePackCode.MaxFixInt" />,
        ///     or any of the other MsgPack integer types.
        /// </summary>
        /// <returns>The value.</returns>
        /// <exception cref="OverflowException">Thrown when the value exceeds what can be stored in the returned type.</exception>
        public sbyte ReadSByte()
        {
            ThrowInsufficientBufferUnless(reader.TryRead(out var code));

            switch (code)
            {
                case MessagePackCode.UInt8:
                    ThrowInsufficientBufferUnless(reader.TryRead(out var byteResult));
                    return checked((sbyte)byteResult);
                case MessagePackCode.Int8:
                    ThrowInsufficientBufferUnless(reader.TryRead(out sbyte sbyteResult));
                    return sbyteResult;
                case MessagePackCode.UInt16:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out ushort ushortResult));
                    return checked((sbyte)ushortResult);
                case MessagePackCode.Int16:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out short shortResult));
                    return checked((sbyte)shortResult);
                case MessagePackCode.UInt32:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out uint uintResult));
                    return checked((sbyte)uintResult);
                case MessagePackCode.Int32:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out int intResult));
                    return checked((sbyte)intResult);
                case MessagePackCode.UInt64:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out ulong ulongResult));
                    return checked((sbyte)ulongResult);
                case MessagePackCode.Int64:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out long longResult));
                    return checked((sbyte)longResult);
                default:
                    if (code >= MessagePackCode.MinNegativeFixInt && code <= MessagePackCode.MaxNegativeFixInt) return unchecked((sbyte)code);

                    if (code >= MessagePackCode.MinFixInt && code <= MessagePackCode.MaxFixInt) return (sbyte)code;

                    throw ThrowInvalidCode(code);
            }
        }

        /// <summary>
        ///     Reads an <see cref="Int16" /> value from:
        ///     Some value between <see cref="MessagePackCode.MinNegativeFixInt" /> and
        ///     <see cref="MessagePackCode.MaxNegativeFixInt" />,
        ///     Some value between <see cref="MessagePackCode.MinFixInt" /> and <see cref="MessagePackCode.MaxFixInt" />,
        ///     or any of the other MsgPack integer types.
        /// </summary>
        /// <returns>The value.</returns>
        /// <exception cref="OverflowException">Thrown when the value exceeds what can be stored in the returned type.</exception>
        public short ReadInt16()
        {
            ThrowInsufficientBufferUnless(reader.TryRead(out var code));

            switch (code)
            {
                case MessagePackCode.UInt8:
                    ThrowInsufficientBufferUnless(reader.TryRead(out var byteResult));
                    return byteResult;
                case MessagePackCode.Int8:
                    ThrowInsufficientBufferUnless(reader.TryRead(out sbyte sbyteResult));
                    return sbyteResult;
                case MessagePackCode.UInt16:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out ushort ushortResult));
                    return checked((short)ushortResult);
                case MessagePackCode.Int16:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out short shortResult));
                    return shortResult;
                case MessagePackCode.UInt32:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out uint uintResult));
                    return checked((short)uintResult);
                case MessagePackCode.Int32:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out int intResult));
                    return checked((short)intResult);
                case MessagePackCode.UInt64:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out ulong ulongResult));
                    return checked((short)ulongResult);
                case MessagePackCode.Int64:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out long longResult));
                    return checked((short)longResult);
                default:
                    if (code >= MessagePackCode.MinNegativeFixInt && code <= MessagePackCode.MaxNegativeFixInt) return unchecked((sbyte)code);

                    if (code >= MessagePackCode.MinFixInt && code <= MessagePackCode.MaxFixInt) return code;

                    throw ThrowInvalidCode(code);
            }
        }

        /// <summary>
        ///     Reads an <see cref="Int32" /> value from:
        ///     Some value between <see cref="MessagePackCode.MinNegativeFixInt" /> and
        ///     <see cref="MessagePackCode.MaxNegativeFixInt" />,
        ///     Some value between <see cref="MessagePackCode.MinFixInt" /> and <see cref="MessagePackCode.MaxFixInt" />,
        ///     or any of the other MsgPack integer types.
        /// </summary>
        /// <returns>The value.</returns>
        /// <exception cref="OverflowException">Thrown when the value exceeds what can be stored in the returned type.</exception>
        public int ReadInt32()
        {
            ThrowInsufficientBufferUnless(reader.TryRead(out var code));

            switch (code)
            {
                case MessagePackCode.UInt8:
                    ThrowInsufficientBufferUnless(reader.TryRead(out var byteResult));
                    return byteResult;
                case MessagePackCode.Int8:
                    ThrowInsufficientBufferUnless(reader.TryRead(out sbyte sbyteResult));
                    return sbyteResult;
                case MessagePackCode.UInt16:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out ushort ushortResult));
                    return ushortResult;
                case MessagePackCode.Int16:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out short shortResult));
                    return shortResult;
                case MessagePackCode.UInt32:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out uint uintResult));
                    return checked((int)uintResult);
                case MessagePackCode.Int32:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out int intResult));
                    return intResult;
                case MessagePackCode.UInt64:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out ulong ulongResult));
                    return checked((int)ulongResult);
                case MessagePackCode.Int64:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out long longResult));
                    return checked((int)longResult);
                default:
                    if (code >= MessagePackCode.MinNegativeFixInt && code <= MessagePackCode.MaxNegativeFixInt) return unchecked((sbyte)code);

                    if (code >= MessagePackCode.MinFixInt && code <= MessagePackCode.MaxFixInt) return code;

                    throw ThrowInvalidCode(code);
            }
        }

        /// <summary>
        ///     Reads an <see cref="Int64" /> value from:
        ///     Some value between <see cref="MessagePackCode.MinNegativeFixInt" /> and
        ///     <see cref="MessagePackCode.MaxNegativeFixInt" />,
        ///     Some value between <see cref="MessagePackCode.MinFixInt" /> and <see cref="MessagePackCode.MaxFixInt" />,
        ///     or any of the other MsgPack integer types.
        /// </summary>
        /// <returns>The value.</returns>
        /// <exception cref="OverflowException">Thrown when the value exceeds what can be stored in the returned type.</exception>
        public long ReadInt64()
        {
            ThrowInsufficientBufferUnless(reader.TryRead(out var code));

            switch (code)
            {
                case MessagePackCode.UInt8:
                    ThrowInsufficientBufferUnless(reader.TryRead(out var byteResult));
                    return byteResult;
                case MessagePackCode.Int8:
                    ThrowInsufficientBufferUnless(reader.TryRead(out sbyte sbyteResult));
                    return sbyteResult;
                case MessagePackCode.UInt16:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out ushort ushortResult));
                    return ushortResult;
                case MessagePackCode.Int16:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out short shortResult));
                    return shortResult;
                case MessagePackCode.UInt32:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out uint uintResult));
                    return uintResult;
                case MessagePackCode.Int32:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out int intResult));
                    return intResult;
                case MessagePackCode.UInt64:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out ulong ulongResult));
                    return checked((long)ulongResult);
                case MessagePackCode.Int64:
                    ThrowInsufficientBufferUnless(reader.TryReadBigEndian(out long longResult));
                    return longResult;
                default:
                    if (code >= MessagePackCode.MinNegativeFixInt && code <= MessagePackCode.MaxNegativeFixInt) return unchecked((sbyte)code);

                    if (code >= MessagePackCode.MinFixInt && code <= MessagePackCode.MaxFixInt) return code;

                    throw ThrowInvalidCode(code);
            }
        }
    }
}