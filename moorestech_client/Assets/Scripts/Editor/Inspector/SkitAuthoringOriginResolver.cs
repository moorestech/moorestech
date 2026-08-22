using Client.Game.InGame.Context;
using Client.Skit.Context;
using UnityEditor;
using UnityEngine;
using VContainer;

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
        if (Application.isPlaying && ClientDIContext.DIContainer != null)
        {
            // MainGameStarterがコンテナ構築時に必ずSkitOriginを登録するため直接Resolveできる
            // MainGameStarter always registers SkitOrigin when building the container, so resolve directly
            origin = ClientDIContext.DIContainer.DIContainerResolver.Resolve<SkitOrigin>();
        }
        
        if (origin == null)
        {
            EditorUtility.DisplayDialog(
                "スキット座標コピー",
                "スポーン原点を実行時DIから解決できませんでした。PlayModeでゲーム開始後にコピーしてください（ADR 0029: JSONは相対座標）",
                "OK");
            return false;
        }
        
        return true;
    }
}
