using System.Collections.Generic;
using UnityEngine;

namespace Client.Common.PrefabPostProcess
{
    /// <summary>
    /// 元マテリアルの項目とキーワードを写した、別シェーダーの版を1回だけ作って使い回す
    /// Creates, once, a copy of a material on another shader with its properties and keywords, and reuses it
    /// </summary>
    public sealed class MaterialVariantCache
    {
        private readonly Dictionary<(Material source, Shader shader), Material> _variants = new();

        public Material GetOrCreate(Material source, Shader shader)
        {
            var key = (source, shader);
            if (_variants.TryGetValue(key, out var cached)) return cached;

            // 同じ項目名のシェーダー同士を前提に、値・キーワード・描画順を写す
            // Copy values, keywords and render queue, assuming both shaders share property names
            var variant = new Material(shader) { name = source.name + " (" + shader.name + ")" };
            variant.CopyPropertiesFromMaterial(source);
            // LocalKeywordはシェーダーごとの値なので、名前で写す
            // LocalKeyword values belong to one shader, so keywords are copied by name
            variant.shaderKeywords = source.shaderKeywords;
            variant.renderQueue = source.renderQueue;
            variant.enableInstancing = source.enableInstancing;
            _variants.Add(key, variant);
            return variant;
        }
    }
}
