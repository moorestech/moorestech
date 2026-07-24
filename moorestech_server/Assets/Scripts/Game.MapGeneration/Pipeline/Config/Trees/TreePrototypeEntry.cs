using UnityEngine;

namespace Game.MapGeneration.Pipeline.Config
{
    // 独立配置パイプラインを持つ樹種グループ。prefabs GameObject[] は mapObjectGuid 配列へ置換した。
    // Tree-species group with its own placement pipeline; prefabs replaced by a mapObjectGuid array.
    public class TreePrototypeEntry
    {
        // 等確率で選択される mapObjectGuid 群。
        // mapObjectGuids chosen with equal probability.
        public string[] mapObjectGuids;
        public Vector2 scaleHeightRange = new Vector2(0.8f, 1.2f);
        public Vector2 scaleWidthRange = new Vector2(0.8f, 1.2f);
        public bool lockWidthHeight = true;
        public float sink;
        public float bendFactor;
        public bool randomRotation = true;
        public bool disabled;

        public TreeDensityConfig densityConfig = new TreeDensityConfig();
        public UnderstoryConfig understoryConfig = new UnderstoryConfig();
        public RockProximityTreeConfig rockProximityConfig = new RockProximityTreeConfig();

        public float borderMargin = 0f;
        public float sharedGridMinDistance = 2f;

        public PlacementFilter slopeFilter;
        public PlacementFilter curvatureFilter;

        public PlacementNoise clusterNoise;
        public float clusterNoiseThreshold = 0.3f;
        public PlacementNoise clusterNoise2;
        public NoiseOp noise2Op = NoiseOp.Multiply;

        // 配置木の周辺ハイトマップ変更量（適用は 5b の TreePlacementStage）。
        // Height modification around placed trees (applied by 5b's TreePlacementStage).
        public float heightModAmount;
        public float heightModWidth = 2f;

        public float boundaryScaleMultiplier = 1f;
        public float oldGrowthScale = 1f;
        public float oldGrowthRatio;
    }
}
