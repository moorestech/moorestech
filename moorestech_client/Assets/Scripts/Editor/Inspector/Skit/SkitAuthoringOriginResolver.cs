using Client.Game.Skit;
using Client.Skit.Context;
using UnityEditor;
using UnityEngine;

/// <summary>
///     スキット執筆ツールが実行時と同一のSkitOriginを引く窓口（ADR 0029）
///     Single gateway for skit authoring tools to fetch the same SkitOrigin used at runtime (ADR 0029)
/// </summary>
public static class SkitAuthoringOriginResolver
{
    // 取れない状況ではコピーを拒否し、絶対座標がJSONへ混入する経路を塞ぐ
    // Refuse the copy when unavailable, closing the path for absolute coordinates to leak into JSON
    public static bool TryResolve(out SkitOrigin origin)
    {
        origin = null;
        // 本編もSkitTestもシーン上のSkitManagerが原点を持つため、DIコンテナではなく実体から引く
        // Both the main game and SkitTest keep the origin on the scene's SkitManager, so read it from the instance instead of the DI container
        if (Application.isPlaying)
        {
            var skitManager = Object.FindFirstObjectByType<SkitManager>();
            if (skitManager != null) origin = skitManager.GetSkitOrigin();
        }
        
        if (origin == null)
        {
            EditorUtility.DisplayDialog(
                "スキット座標コピー",
                "スポーン原点を解決できませんでした。PlayModeでスキットシーンを再生してからコピーしてください（ADR 0029: JSONは相対座標）",
                "OK");
            return false;
        }
        
        return true;
    }
}
