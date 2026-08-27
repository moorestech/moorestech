using System;
using Client.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace Client.Game.InGame.Map.MapVein
{
    /// <summary>
    ///     範囲表示ボックス用の半透明マテリアルをitem/fluidの2枚だけ作って共有する
    ///     Creates exactly two translucent materials, one for item veins and one for fluid veins, and shares them
    /// </summary>
    public class MapVeinRangeBoxMaterials : IDisposable
    {
        // テストがこの接頭辞でマテリアル枚数を数える。3枚を超えていたら作り捨てが復活している
        // Tests count materials by this prefix; more than three means per-box material creation came back
        public const string MaterialNamePrefix = "MapVeinRangeBox_";

        // 種別の色分けはveinTypeから導出する。汎用のプレビュー色とは別物なのでここで持つ
        // Type coloring derives from veinType; these are distinct from the generic preview colors so they live here
        private static readonly Color ItemVeinColor = new(0.95f, 0.72f, 0.25f, 1f);
        private static readonly Color FluidVeinColor = new(0.25f, 0.62f, 0.95f, 1f);

        // チュートリアルが指す1つの鉱脈だけを別色で描き、他の鉱脈と取り違えないようにする
        // The single vein a tutorial points at gets its own color so it cannot be mistaken for another
        private static readonly Color HighlightVeinColor = new(0.3f, 0.95f, 0.35f, 1f);

        // PreviewPlaceBlockのalphaは_PreviewColorではなくこのfloatが持つ。色のalphaを変えても効かない
        // PreviewPlaceBlock takes its alpha from this float, not from _PreviewColor, so tinting the color's alpha does nothing
        private const string AlphaPropertyName = "_Alpha";
        private const float BoxAlpha = 0.5f;

        // 元素材は不透明設定なのでマテリアル側で透過へ切り替える。shadergraphがAllow Material Overrideなので実行時に効く
        // The source material is opaque, so flip it to transparent at the material level; the shadergraph allows material override
        private const string SurfacePropertyName = "_Surface";
        private const string SrcBlendPropertyName = "_SrcBlend";
        private const string DstBlendPropertyName = "_DstBlend";
        private const string DstBlendAlphaPropertyName = "_DstBlendAlpha";
        private const string ZWritePropertyName = "_ZWrite";
        private const string TransparentKeyword = "_SURFACE_TYPE_TRANSPARENT";
        private const string RenderTypeTagName = "RenderType";
        private const string TransparentRenderType = "Transparent";
        private const string ShadowCasterPassName = "ShadowCaster";
        private const float TransparentSurfaceValue = 1f;
        private const float ZWriteOff = 0f;

        public readonly Material FluidMaterial;
        public readonly Material ItemMaterial;
        public readonly Material HighlightMaterial;

        public MapVeinRangeBoxMaterials()
        {
            ItemMaterial = CreateTranslucentMaterial("Item", ItemVeinColor);
            FluidMaterial = CreateTranslucentMaterial("Fluid", FluidVeinColor);
            HighlightMaterial = CreateTranslucentMaterial("Highlight", HighlightVeinColor);

            #region Internal

            Material CreateTranslucentMaterial(string veinTypeName, Color color)
            {
                var material = new Material(MaterialConst.GetPreviewPlaceBlockMaterial()) { name = MaterialNamePrefix + veinTypeName };
                material.SetColor(MaterialConst.PreviewColorPropertyName, color);
                material.SetFloat(AlphaPropertyName, BoxAlpha);

                // URP Litの透過設定一式。ブレンド式とキューまで揃えないと不透明のまま描かれる
                // The full URP Lit transparency set; without the blend equation and the queue it still draws opaque
                material.SetFloat(SurfacePropertyName, TransparentSurfaceValue);
                material.SetFloat(SrcBlendPropertyName, (float)BlendMode.SrcAlpha);
                material.SetFloat(DstBlendPropertyName, (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat(DstBlendAlphaPropertyName, (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat(ZWritePropertyName, ZWriteOff);
                material.EnableKeyword(TransparentKeyword);
                material.SetOverrideTag(RenderTypeTagName, TransparentRenderType);
                material.renderQueue = (int)RenderQueue.Transparent;

                // 半透明の範囲表示が地面に影を落とすと地形が読めなくなる
                // A translucent range box casting shadows would hide the terrain it is meant to annotate
                material.SetShaderPassEnabled(ShadowCasterPassName, false);
                return material;
            }

            #endregion
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(ItemMaterial);
            UnityEngine.Object.Destroy(FluidMaterial);
            UnityEngine.Object.Destroy(HighlightMaterial);
        }
    }
}
