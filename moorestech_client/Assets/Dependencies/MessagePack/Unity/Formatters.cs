// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MessagePack.Formatters;
using UnityEngine;

#pragma warning disable SA1312 // variable naming
#pragma warning disable SA1402 // one type per file
#pragma warning disable SA1649 // file name matches type name

namespace MessagePack.Unity
{
    public sealed class Vector2Formatter : IMessagePackFormatter<Vector2>
    {
        public void Serialize(ref MessagePackWriter writer, Vector2 value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(2);
            writer.Write(value.x);
            writer.Write(value.y);
        }

        public Vector2 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");

            var length = reader.ReadArrayHeader();
            var x = default(float);
            var y = default(float);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        x = reader.ReadSingle();
                        break;
                    case 1:
                        y = reader.ReadSingle();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var result = new Vector2(x, y);
            return result;
        }
    }

    public sealed class Vector3Formatter : IMessagePackFormatter<Vector3>
    {
        public void Serialize(ref MessagePackWriter writer, Vector3 value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(3);
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        public Vector3 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");

            var length = reader.ReadArrayHeader();
            var x = default(float);
            var y = default(float);
            var z = default(float);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        x = reader.ReadSingle();
                        break;
                    case 1:
                        y = reader.ReadSingle();
                        break;
                    case 2:
                        z = reader.ReadSingle();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var result = new Vector3(x, y, z);
            return result;
        }
    }

    public sealed class Vector4Formatter : IMessagePackFormatter<Vector4>
    {
        public void Serialize(ref MessagePackWriter writer, Vector4 value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(4);
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
            writer.Write(value.w);
        }

        public Vector4 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");

            var length = reader.ReadArrayHeader();
            var x = default(float);
            var y = default(float);
            var z = default(float);
            var w = default(float);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        x = reader.ReadSingle();
                        break;
                    case 1:
                        y = reader.ReadSingle();
                        break;
                    case 2:
                        z = reader.ReadSingle();
                        break;
                    case 3:
                        w = reader.ReadSingle();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var result = new Vector4(x, y, z, w);
            return result;
        }
    }

    public sealed class QuaternionFormatter : IMessagePackFormatter<Quaternion>
    {
        public void Serialize(ref MessagePackWriter writer, Quaternion value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(4);
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
            writer.Write(value.w);
        }

        public Quaternion Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");

            var length = reader.ReadArrayHeader();
            var x = default(float);
            var y = default(float);
            var z = default(float);
            var w = default(float);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        x = reader.ReadSingle();
                        break;
                    case 1:
                        y = reader.ReadSingle();
                        break;
                    case 2:
                        z = reader.ReadSingle();
                        break;
                    case 3:
                        w = reader.ReadSingle();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var result = new Quaternion(x, y, z, w);
            return result;
        }
    }

    public sealed class ColorFormatter : IMessagePackFormatter<Color>
    {
        public void Serialize(ref MessagePackWriter writer, Color value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(4);
            writer.Write(value.r);
            writer.Write(value.g);
            writer.Write(value.b);
            writer.Write(value.a);
        }

        public Color Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");

            var length = reader.ReadArrayHeader();
            var r = default(float);
            var g = default(float);
            var b = default(float);
            var a = default(float);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        r = reader.ReadSingle();
                        break;
                    case 1:
                        g = reader.ReadSingle();
                        break;
                    case 2:
                        b = reader.ReadSingle();
                        break;
                    case 3:
                        a = reader.ReadSingle();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var result = new Color(r, g, b, a);
            return result;
        }
    }

    public sealed class BoundsFormatter : IMessagePackFormatter<Bounds>
    {
        public void Serialize(ref MessagePackWriter writer, Bounds value, MessagePackSerializerOptions options)
        {
            var resolver = options.Resolver;
            writer.WriteArrayHeader(2);
            resolver.GetFormatterWithVerify<Vector3>().Serialize(ref writer, value.center, options);
            resolver.GetFormatterWithVerify<Vector3>().Serialize(ref writer, value.size, options);
        }

        public Bounds Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");

            var resolver = options.Resolver;
            var length = reader.ReadArrayHeader();
            var center = default(Vector3);
            var size = default(Vector3);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        center = resolver.GetFormatterWithVerify<Vector3>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        size = resolver.GetFormatterWithVerify<Vector3>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var result = new Bounds(center, size);
            return result;
        }
    }

    public sealed class RectFormatter : IMessagePackFormatter<Rect>
    {
        public void Serialize(ref MessagePackWriter writer, Rect value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(4);
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.width);
            writer.Write(value.height);
        }

        public Rect Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");

            var length = reader.ReadArrayHeader();
            var x = default(float);
            var y = default(float);
            var width = default(float);
            var height = default(float);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        x = reader.ReadSingle();
                        break;
                    case 1:
                        y = reader.ReadSingle();
                        break;
                    case 2:
                        width = reader.ReadSingle();
                        break;
                    case 3:
                        height = reader.ReadSingle();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var result = new Rect(x, y, width, height);
            return result;
        }
    }

    // additional
    public sealed class WrapModeFormatter : IMessagePackFormatter<WrapMode>
    {
        public void Serialize(ref MessagePackWriter writer, WrapMode value, MessagePackSerializerOptions options)
        {
            writer.Write((int)value);
        }

        public WrapMode Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return (WrapMode)reader.ReadInt32();
        }
    }

    public sealed class GradientModeFormatter : IMessagePackFormatter<GradientMode>
    {
        public void Serialize(ref MessagePackWriter writer, GradientMode value, MessagePackSerializerOptions options)
        {
            writer.Write((int)value);
        }

        public GradientMode Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return (GradientMode)reader.ReadInt32();
        }
    }

    public sealed class KeyframeFormatter : IMessagePackFormatter<Keyframe>
    {
        public void Serialize(ref MessagePackWriter writer, Keyframe value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(4);
            writer.Write(value.time);
            writer.Write(value.value);
            writer.Write(value.inTangent);
            writer.Write(value.outTangent);
        }

        public Keyframe Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");

            var length = reader.ReadArrayHeader();
            var __time__ = default(float);
            var __value__ = default(float);
            var __inTangent__ = default(float);
            var __outTangent__ = default(float);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        __time__ = reader.ReadSingle();
                        break;
                    case 1:
                        __value__ = reader.ReadSingle();
                        break;
                    case 2:
                        __inTangent__ = reader.ReadSingle();
                        break;
                    case 3:
                        __outTangent__ = reader.ReadSingle();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new Keyframe(__time__, __value__, __inTangent__, __outTangent__);
            ____result.time = __time__;
            ____result.value = __value__;
            ____result.inTangent = __inTangent__;
            ____result.outTangent = __outTangent__;
            return ____result;
        }
    }

    public sealed class AnimationCurveFormatter : IMessagePackFormatter<AnimationCurve>
    {
        public void Serialize(ref MessagePackWriter writer, AnimationCurve value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            var resolver = options.Resolver;
            writer.WriteArrayHeader(3);
            resolver.GetFormatterWithVerify<Keyframe[]>().Serialize(ref writer, value.keys, options);
            resolver.GetFormatterWithVerify<WrapMode>().Serialize(ref writer, value.postWrapMode, options);
            resolver.GetFormatterWithVerify<WrapMode>().Serialize(ref writer, value.preWrapMode, options);
        }

        public AnimationCurve Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) return null;

            var resolver = options.Resolver;
            var length = reader.ReadArrayHeader();
            var __keys__ = default(Keyframe[]);
            var __postWrapMode__ = default(WrapMode);
            var __preWrapMode__ = default(WrapMode);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        __keys__ = resolver.GetFormatterWithVerify<Keyframe[]>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        __postWrapMode__ = resolver.GetFormatterWithVerify<WrapMode>().Deserialize(ref reader, options);
                        break;
                    case 2:
                        __preWrapMode__ = resolver.GetFormatterWithVerify<WrapMode>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new AnimationCurve();
            ____result.keys = __keys__;
            ____result.postWrapMode = __postWrapMode__;
            ____result.preWrapMode = __preWrapMode__;
            return ____result;
        }
    }

    public sealed class Matrix4x4Formatter : IMessagePackFormatter<Matrix4x4>
    {
        public void Serialize(ref MessagePackWriter writer, Matrix4x4 value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(16);
            writer.Write(value.m00);
            writer.Write(value.m10);
            writer.Write(value.m20);
            writer.Write(value.m30);
            writer.Write(value.m01);
            writer.Write(value.m11);
            writer.Write(value.m21);
            writer.Write(value.m31);
            writer.Write(value.m02);
            writer.Write(value.m12);
            writer.Write(value.m22);
            writer.Write(value.m32);
            writer.Write(value.m03);
            writer.Write(value.m13);
            writer.Write(value.m23);
            writer.Write(value.m33);
        }

        public Matrix4x4 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");

            var length = reader.ReadArrayHeader();
            var __m00__ = default(float);
            var __m10__ = default(float);
            var __m20__ = default(float);
            var __m30__ = default(float);
            var __m01__ = default(float);
            var __m11__ = default(float);
            var __m21__ = default(float);
            var __m31__ = default(float);
            var __m02__ = default(float);
            var __m12__ = default(float);
            var __m22__ = default(float);
            var __m32__ = default(float);
            var __m03__ = default(float);
            var __m13__ = default(float);
            var __m23__ = default(float);
            var __m33__ = default(float);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        __m00__ = reader.ReadSingle();
                        break;
                    case 1:
                        __m10__ = reader.ReadSingle();
                        break;
                    case 2:
                        __m20__ = reader.ReadSingle();
                        break;
                    case 3:
                        __m30__ = reader.ReadSingle();
                        break;
                    case 4:
                        __m01__ = reader.ReadSingle();
                        break;
                    case 5:
                        __m11__ = reader.ReadSingle();
                        break;
                    case 6:
                        __m21__ = reader.ReadSingle();
                        break;
                    case 7:
                        __m31__ = reader.ReadSingle();
                        break;
                    case 8:
                        __m02__ = reader.ReadSingle();
                        break;
                    case 9:
                        __m12__ = reader.ReadSingle();
                        break;
                    case 10:
                        __m22__ = reader.ReadSingle();
                        break;
                    case 11:
                        __m32__ = reader.ReadSingle();
                        break;
                    case 12:
                        __m03__ = reader.ReadSingle();
                        break;
                    case 13:
                        __m13__ = reader.ReadSingle();
                        break;
                    case 14:
                        __m23__ = reader.ReadSingle();
                        break;
                    case 15:
                        __m33__ = reader.ReadSingle();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = default(Matrix4x4);
            ____result.m00 = __m00__;
            ____result.m10 = __m10__;
            ____result.m20 = __m20__;
            ____result.m30 = __m30__;
            ____result.m01 = __m01__;
            ____result.m11 = __m11__;
            ____result.m21 = __m21__;
            ____result.m31 = __m31__;
            ____result.m02 = __m02__;
            ____result.m12 = __m12__;
            ____result.m22 = __m22__;
            ____result.m32 = __m32__;
            ____result.m03 = __m03__;
            ____result.m13 = __m13__;
            ____result.m23 = __m23__;
            ____result.m33 = __m33__;
            return ____result;
        }
    }

    public sealed class GradientColorKeyFormatter : IMessagePackFormatter<GradientColorKey>
    {
        public void Serialize(ref MessagePackWriter writer, GradientColorKey value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(2);
            options.Resolver.GetFormatterWithVerify<Color>().Serialize(ref writer, value.color, options);
            writer.Write(value.time);
        }

        public GradientColorKey Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");

            var length = reader.ReadArrayHeader();
            var __color__ = default(Color);
            var __time__ = default(float);
            var resolver = options.Resolver;
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        __color__ = resolver.GetFormatterWithVerify<Color>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        __time__ = reader.ReadSingle();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new GradientColorKey(__color__, __time__);
            ____result.color = __color__;
            ____result.time = __time__;
            return ____result;
        }
    }

    public sealed class GradientAlphaKeyFormatter : IMessagePackFormatter<GradientAlphaKey>
    {
        public void Serialize(ref MessagePackWriter writer, GradientAlphaKey value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(2);
            writer.Write(value.alpha);
            writer.Write(value.time);
        }

        public GradientAlphaKey Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");

            var length = reader.ReadArrayHeader();
            var __alpha__ = default(float);
            var __time__ = default(float);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        __alpha__ = reader.ReadSingle();
                        break;
                    case 1:
                        __time__ = reader.ReadSingle();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new GradientAlphaKey(__alpha__, __time__);
            ____result.alpha = __alpha__;
            ____result.time = __time__;
            return ____result;
        }
    }

    public sealed class GradientFormatter : IMessagePackFormatter<Gradient>
    {
        public void Serialize(ref MessagePackWriter writer, Gradient value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            var resolver = options.Resolver;
            writer.WriteArrayHeader(3);
            resolver.GetFormatterWithVerify<GradientColorKey[]>().Serialize(ref writer, value.colorKeys, options);
            resolver.GetFormatterWithVerify<GradientAlphaKey[]>().Serialize(ref writer, value.alphaKeys, options);
            resolver.GetFormatterWithVerify<GradientMode>().Serialize(ref writer, value.mode, options);
        }

        public Gradient Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) return null;

            var resolver = options.Resolver;
            var length = reader.ReadArrayHeader();
            var __colorKeys__ = default(GradientColorKey[]);
            var __alphaKeys__ = default(GradientAlphaKey[]);
            var __mode__ = default(GradientMode);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        __colorKeys__ = resolver.GetFormatterWithVerify<GradientColorKey[]>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        __alphaKeys__ = resolver.GetFormatterWithVerify<GradientAlphaKey[]>().Deserialize(ref reader, options);
                        break;
                    case 2:
                        __mode__ = resolver.GetFormatterWithVerify<GradientMode>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new Gradient();
            ____result.colorKeys = __colorKeys__;
            ____result.alphaKeys = __alphaKeys__;
            ____result.mode = __mode__;
            return ____result;
        }
    }

    public sealed class Color32Formatter : IMessagePackFormatter<Color32>
    {
        public void Serialize(ref MessagePackWriter writer, Color32 value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(4);
            writer.Write(value.r);
            writer.Write(value.g);
            writer.Write(value.b);
            writer.Write(value.a);
        }

        public Color32 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");

            var length = reader.ReadArrayHeader();
            var __r__ = default(byte);
            var __g__ = default(byte);
            var __b__ = default(byte);
            var __a__ = default(byte);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        __r__ = reader.ReadByte();
                        break;
                    case 1:
                        __g__ = reader.ReadByte();
                        break;
                    case 2:
                        __b__ = reader.ReadByte();
                        break;
                    case 3:
                        __a__ = reader.ReadByte();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new Color32(__r__, __g__, __b__, __a__);
            ____result.r = __r__;
            ____result.g = __g__;
            ____result.b = __b__;
            ____result.a = __a__;
            return ____result;
        }
    }

    public sealed class RectOffsetFormatter : IMessagePackFormatter<RectOffset>
    {
        public void Serialize(ref MessagePackWriter writer, RectOffset value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(4);
            writer.Write(value.left);
            writer.Write(value.right);
            writer.Write(value.top);
            writer.Write(value.bottom);
        }

        public RectOffset Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) return null;

            var length = reader.ReadArrayHeader();
            var __left__ = default(int);
            var __right__ = default(int);
            var __top__ = default(int);
            var __bottom__ = default(int);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        __left__ = reader.ReadInt32();
                        break;
                    case 1:
                        __right__ = reader.ReadInt32();
                        break;
                    case 2:
                        __top__ = reader.ReadInt32();
                        break;
                    case 3:
                        __bottom__ = reader.ReadInt32();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new RectOffset();
            ____result.left = __left__;
            ____result.right = __right__;
            ____result.top = __top__;
            ____result.bottom = __bottom__;
            return ____result;
        }
    }

    public sealed class LayerMaskFormatter : IMessagePackFormatter<LayerMask>
    {
        public void Serialize(ref MessagePackWriter writer, LayerMask value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(1);
            writer.Write(value.value);
        }

        public LayerMask Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");

            var length = reader.ReadArrayHeader();
            var __value__ = default(int);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        __value__ = reader.ReadInt32();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = default(LayerMask);
            ____result.value = __value__;
            return ____result;
        }
    }
#if UNITY_2017_2_OR_NEWER
    public sealed class Vector2IntFormatter : IMessagePackFormatter<Vector2Int>
    {
        public void Serialize(ref MessagePackWriter writer, Vector2Int value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(2);
            writer.WriteInt32(value.x);
            writer.WriteInt32(value.y);
        }

        public Vector2Int Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");
            var length = reader.ReadArrayHeader();
            var __x__ = default(int);
            var __y__ = default(int);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        __x__ = reader.ReadInt32();
                        break;
                    case 1:
                        __y__ = reader.ReadInt32();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new Vector2Int(__x__, __y__);
            ____result.x = __x__;
            ____result.y = __y__;
            return ____result;
        }
    }

    public sealed class Vector3IntFormatter : IMessagePackFormatter<Vector3Int>
    {
        public void Serialize(ref MessagePackWriter writer, Vector3Int value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(3);
            writer.WriteInt32(value.x);
            writer.WriteInt32(value.y);
            writer.WriteInt32(value.z);
        }

        public Vector3Int Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");
            var length = reader.ReadArrayHeader();
            var __x__ = default(int);
            var __y__ = default(int);
            var __z__ = default(int);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        __x__ = reader.ReadInt32();
                        break;
                    case 1:
                        __y__ = reader.ReadInt32();
                        break;
                    case 2:
                        __z__ = reader.ReadInt32();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new Vector3Int(__x__, __y__, __z__);
            ____result.x = __x__;
            ____result.y = __y__;
            ____result.z = __z__;
            return ____result;
        }
    }

    public sealed class RangeIntFormatter : IMessagePackFormatter<RangeInt>
    {
        public void Serialize(ref MessagePackWriter writer, RangeInt value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(2);
            writer.WriteInt32(value.start);
            writer.WriteInt32(value.length);
        }

        public RangeInt Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");
            var length = reader.ReadArrayHeader();
            var __start__ = default(int);
            var __length__ = default(int);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        __start__ = reader.ReadInt32();
                        break;
                    case 1:
                        __length__ = reader.ReadInt32();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new RangeInt(__start__, __length__);
            ____result.start = __start__;
            ____result.length = __length__;
            return ____result;
        }
    }

    public sealed class RectIntFormatter : IMessagePackFormatter<RectInt>
    {
        public void Serialize(ref MessagePackWriter writer, RectInt value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(4);
            writer.WriteInt32(value.x);
            writer.WriteInt32(value.y);
            writer.WriteInt32(value.width);
            writer.WriteInt32(value.height);
        }

        public RectInt Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");
            var length = reader.ReadArrayHeader();
            var __x__ = default(int);
            var __y__ = default(int);
            var __width__ = default(int);
            var __height__ = default(int);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        __x__ = reader.ReadInt32();
                        break;
                    case 1:
                        __y__ = reader.ReadInt32();
                        break;
                    case 2:
                        __width__ = reader.ReadInt32();
                        break;
                    case 3:
                        __height__ = reader.ReadInt32();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new RectInt(__x__, __y__, __width__, __height__);
            ____result.x = __x__;
            ____result.y = __y__;
            ____result.width = __width__;
            ____result.height = __height__;
            return ____result;
        }
    }

    public sealed class BoundsIntFormatter : IMessagePackFormatter<BoundsInt>
    {
        public void Serialize(ref MessagePackWriter writer, BoundsInt value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(2);
            options.Resolver.GetFormatterWithVerify<Vector3Int>().Serialize(ref writer, value.position, options);
            options.Resolver.GetFormatterWithVerify<Vector3Int>().Serialize(ref writer, value.size, options);
        }

        public BoundsInt Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil) throw new InvalidOperationException("typecode is null, struct not supported");
            var length = reader.ReadArrayHeader();
            var __position__ = default(Vector3Int);
            var __size__ = default(Vector3Int);
            for (var i = 0; i < length; i++)
            {
                var key = i;
                switch (key)
                {
                    case 0:
                        __position__ = options.Resolver.GetFormatterWithVerify<Vector3Int>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        __size__ = options.Resolver.GetFormatterWithVerify<Vector3Int>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new BoundsInt(__position__, __size__);
            ____result.position = __position__;
            ____result.size = __size__;
            return ____result;
        }
    }
#endif
}