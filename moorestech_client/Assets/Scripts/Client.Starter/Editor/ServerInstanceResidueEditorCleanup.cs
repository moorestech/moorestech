#if UNITY_EDITOR
using Server.Boot;
using UnityEditor;
using UnityEngine;

namespace Client.Starter.Editor
{
    /// <summary>
    /// Editor 専用の保険: Play 終了後に再開した初期化継続が編集中シーンへ残した ServerStarter を、次の Play が始まる前に除去する
    /// 本線は exitCancellationToken と生成直前の fail-closed ガード。ここは両方をすり抜けた残留が次の Play で引数なしサーバーとして起動するのを止める最後の網
    /// Editor-only safety net: removes ServerStarter objects that a resumed initialization left in the edited scene before the next play session starts
    /// The primary defenses are exitCancellationToken and the fail-closed guard before creation; this last net stops a leaked object from booting an argument-less server on the next play
    /// </summary>
    public static class ServerInstanceResidueEditorCleanup
    {
        //   - EnteredEditMode: Play 終了直後にすでに再開していた継続の残留を拾う
        //   - ExitingEditMode: 遅れて再開した継続の残留を、シーンが Play 用に退避される前に拾う
        //   - EnteredEditMode: catches residue from a continuation that already resumed right after play-mode exit
        //   - ExitingEditMode: catches residue from a late continuation before the scene is backed up for play
        [InitializeOnLoadMethod]
        private static void RegisterPlayModeHook()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode && state != PlayModeStateChange.ExitingEditMode) return;
            RemoveResidueFromLoadedScenes();
        }

        private static void RemoveResidueFromLoadedScenes()
        {
            // EditMode で見つかる ServerStarter は正規経路では存在し得ない。Play 中の生成物は DontDestroyOnLoad ごと Play 終了で消えるため
            // No ServerStarter can legitimately exist in EditMode: play-time instances vanish with DontDestroyOnLoad on play-mode exit
            var residues = Object.FindObjectsByType<ServerStarter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var residue in residues)
            {
                Debug.LogWarning($"[ServerInstanceResidueEditorCleanup] 編集中シーン '{residue.gameObject.scene.name}' に残留した ServerInstance を除去します");
                Object.DestroyImmediate(residue.gameObject);
            }
        }
    }
}
#endif
