﻿// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using MessagePack.Formatters;
using UnityEngine;

#pragma warning disable SA1402 // multiple types in a file
#pragma warning disable SA1649 // file name matches type name

namespace MessagePack.Unity.Extension
{
    // use ext instead of ArrayFormatter to extremely boost up performance.
    // Layout: [extHeader, byteSize(integer), isLittleEndian(bool), bytes()]
    // Used Ext:30~36
    public abstract class UnsafeBlitFormatterBase<T> : IMessagePackFormatter<T[]>
        where T : struct
    {
        protected abstract sbyte TypeCode { get; }

        public void Serialize(ref MessagePackWriter writer, T[] value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            var byteLen = value.Length * Marshal.SizeOf<T>();

            writer.WriteExtensionFormatHeader(new ExtensionHeader(TypeCode, byteLen));
            writer.Write(byteLen); // write original header(not array header)
            writer.Write(BitConverter.IsLittleEndian);
            writer.WriteRaw(MemoryMarshal.Cast<T, byte>(value));
        }

        public T[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;

            var header = reader.ReadExtensionFormatHeader();
            if (header.TypeCode != TypeCode) throw new InvalidOperationException("Invalid typeCode.");

            var byteLength = reader.ReadInt32();
            var isLittleEndian = reader.ReadBoolean();

            // Allocate a T[] that we will return. We'll then cast the T[] as byte[] so we can copy the byte sequence directly into it.
            var result = new T[byteLength / Marshal.SizeOf<T>()];
            var resultAsBytes = MemoryMarshal.Cast<T, byte>(result);
            reader.ReadRaw(byteLength).CopyTo(resultAsBytes);

            // Reverse the byte order if necessary.
            if (isLittleEndian != BitConverter.IsLittleEndian)
                for (int i = 0, j = resultAsBytes.Length - 1; i < j; i++, j--)
                {
                    var tmp = resultAsBytes[i];
                    resultAsBytes[i] = resultAsBytes[j];
                    resultAsBytes[j] = tmp;
                }

            return result;
        }

        protected void CopyDeserializeUnsafe(ReadOnlySpan<byte> src, Span<T> dest)
        {
            src.CopyTo(MemoryMarshal.Cast<T, byte>(dest));
        }
    }

    public class Vector2ArrayBlitFormatter : UnsafeBlitFormatterBase<Vector2>
    {
        protected override sbyte TypeCode => ThisLibraryExtensionTypeCodes.UnityVector2;
    }

    public class Vector3ArrayBlitFormatter : UnsafeBlitFormatterBase<Vector3>
    {
        protected override sbyte TypeCode => ThisLibraryExtensionTypeCodes.UnityVector3;
    }

    public class Vector4ArrayBlitFormatter : UnsafeBlitFormatterBase<Vector4>
    {
        protected override sbyte TypeCode => ThisLibraryExtensionTypeCodes.UnityVector4;
    }

    public class QuaternionArrayBlitFormatter : UnsafeBlitFormatterBase<Quaternion>
    {
        protected override sbyte TypeCode => ThisLibraryExtensionTypeCodes.UnityQuaternion;
    }

    public class ColorArrayBlitFormatter : UnsafeBlitFormatterBase<Color>
    {
        protected override sbyte TypeCode => ThisLibraryExtensionTypeCodes.UnityColor;
    }

    public class BoundsArrayBlitFormatter : UnsafeBlitFormatterBase<Bounds>
    {
        protected override sbyte TypeCode => ThisLibraryExtensionTypeCodes.UnityBounds;
    }

    public class RectArrayBlitFormatter : UnsafeBlitFormatterBase<Rect>
    {
        protected override sbyte TypeCode => ThisLibraryExtensionTypeCodes.UnityRect;
    }

    public class IntArrayBlitFormatter : UnsafeBlitFormatterBase<int>
    {
        protected override sbyte TypeCode => ThisLibraryExtensionTypeCodes.UnityInt;
    }

    public class FloatArrayBlitFormatter : UnsafeBlitFormatterBase<float>
    {
        protected override sbyte TypeCode => ThisLibraryExtensionTypeCodes.UnityFloat;
    }

    public class DoubleArrayBlitFormatter : UnsafeBlitFormatterBase<double>
    {
        protected override sbyte TypeCode => ThisLibraryExtensionTypeCodes.UnityDouble;
    }
}