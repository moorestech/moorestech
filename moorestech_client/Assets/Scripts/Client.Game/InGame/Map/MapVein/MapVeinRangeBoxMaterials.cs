using System;
using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.Map.MapVein
{
    /// <summary>
    ///     範囲表示ボックス用の半透明マテリアルをitem/fluidの2枚だけ作って共有する
    ///     Creates exactly two translucent materials, one for item veins and one for fluid veins, and shares them
    /// </summary>
    public class MapVeinRangeBoxMaterials : IDisposable
    {
        // テストがこの接頭辞でマテリアル枚数を数える。2枚を超えていたら作り捨てが復活している
        // Tests count materials by this prefix; more than two means per-box material creation came back
        public const string MaterialNamePrefix = "MapVeinRangeBox_";

        // 種別の色分けはveinTypeから導出する。汎用のプレビュー色とは別物なのでここで持つ
        // Type coloring derives from veinType; these are distinct from the generic preview colors so they live here
        private static readonly Color ItemVeinColor = new(0.95f, 0.72f, 0.25f, 1f);
        private static readonly Color FluidVeinColor = new(0.25f, 0.62f, 0.95f, 1f);

        // PreviewPlaceBlockのalphaは_PreviewColorではなくこのfloatが持つ。色のalphaを変えても効かない
        // PreviewPlaceBlock takes its alpha from this float, not from _PreviewColor, so tinting the color's alpha does nothing
        private const string AlphaPropertyName = "_Alpha";
        private const float BoxAlpha = 0.5f;

        private const string ShadowCasterPassName = "ShadowCaster";

        public readonly Material FluidMaterial;
        public readonly Material ItemMaterial;

        public MapVeinRangeBoxMaterials()
        {
            ItemMaterial = CreateTranslucentMaterial("Item", ItemVeinColor);
            FluidMaterial = CreateTranslucentMaterial("Fluid", FluidVeinColor);

            #region Internal

            Material CreateTranslucentMaterial(string veinTypeName, Color color)
            {
                // 透過設定済みのアセットを複製する。実行時にキーワードで透過へ切り替えるとビルドにそのバリアントが焼かれず不透明になる
                // Copy the pre-authored translucent asset; enabling the keyword at runtime leaves the build without that variant
                var material = new Material(MaterialConst.GetPreviewPlaceBlockTransparentMaterial()) { name = MaterialNamePrefix + veinTypeName };
                material.SetColor(MaterialConst.PreviewColorPropertyName, color);
                material.SetFloat(AlphaPropertyName, BoxAlpha);

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
        }
    }
}
