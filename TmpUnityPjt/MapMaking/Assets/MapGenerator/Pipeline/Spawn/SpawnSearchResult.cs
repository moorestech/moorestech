using UnityEngine;

namespace MapGenerator.Pipeline.Spawn
{
    /// <summary>
    /// SpawnRegionFinder.Find() の結果。座標はすべてワールド(ノイズ)座標系(m)。
    /// </summary>
    public readonly struct SpawnSearchResult
    {
        /// <summary>探索成功（valid候補が見つかった）か。falseならフォールバック。</summary>
        public readonly bool Success;
        /// <summary>生成グリッドに加算するグローバルオフセット G = S - gridCenter。</summary>
        public readonly Vector2 WorldOffset;
        /// <summary>確定したスポーン地点 S（草原final CC内 pole of inaccessibility）。</summary>
        public readonly Vector2 SpawnWorldPosition;
        /// <summary>採用候補の final score。</summary>
        public readonly float Score;
        /// <summary>診断ログ（採用経緯・拡大回数・打ち切り有無）。</summary>
        public readonly string Diagnostics;

        public SpawnSearchResult(bool success, Vector2 worldOffset, Vector2 spawn,
            float score, string diagnostics)
        {
            Success = success;
            WorldOffset = worldOffset;
            SpawnWorldPosition = spawn;
            Score = score;
            Diagnostics = diagnostics;
        }

        public static SpawnSearchResult Fallback(string reason) =>
            new SpawnSearchResult(false, Vector2.zero, Vector2.zero, 0f, reason);
    }
}
