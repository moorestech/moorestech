using System.Collections.Generic;
using UnityEngine;

namespace MapGenerator.Pipeline
{
    /// <summary>
    /// パイプライン全体の出力を保持する値オブジェクト。
    /// TerrainGenerator → TerrainApplier への受け渡しに使う。
    /// Splatmap/Trees/Details は null 許容（レイヤー未設定時はスキップされるため）。
    /// </summary>
    public sealed class TerrainGenerationResult
    {
        public float[] Heights;
        public int Resolution;
        public Vector3 TerrainSize;
        public float[,,] Splatmap;
        public TerrainLayer[] TerrainLayers;
        public TreePrototype[] TreePrototypes;
        public TreeInstance[] TreeInstances;

        // DetailPlacementGenerator が生成する草花密度マップ
        public List<DetailPrototype> DetailPrototypes;
        public List<int[,]> DetailMaps; // [protoIndex][z, x] = density (0-16)

        // ObjectPlacementGenerator が生成するプレハブ配置リスト
        public List<Config.ObjectPlacementResult> ObjectPlacements;

        // OrePlacementGenerator が生成する鉱脈プレハブ配置リスト
        public List<Config.ObjectPlacementResult> OrePlacements;

    }
}
