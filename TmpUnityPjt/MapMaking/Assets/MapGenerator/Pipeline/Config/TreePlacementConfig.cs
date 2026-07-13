using System;
using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// バイオームごとの樹木配置設定。各プロトタイプが独立した配置パイプラインを持つ。
    /// </summary>
    [Serializable]
    public class TreePlacementConfig
    {
        [Label("樹木設定")]
        public TreePrototypeEntry[] prototypes;
    }
}
