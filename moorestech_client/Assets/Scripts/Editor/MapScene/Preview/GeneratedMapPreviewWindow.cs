#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Client.MapScene.Editor
{
    public sealed class GeneratedMapPreviewWindow : EditorWindow
    {
        private string _statusText = "生成ボタンで現在の生成マップを表示します。";

        [MenuItem("moorestech/Generated Map Preview")]
        private static void Open() => GetWindow<GeneratedMapPreviewWindow>("Generated Map Preview");

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            var stage = StageUtility.GetCurrentStage() as GeneratedMapPreviewStage;
            var state = stage != null ? stage.State : GeneratedMapPreviewState.Empty;
            EditorGUILayout.LabelField("状態", state.ToString());
            EditorGUILayout.HelpBox(stage != null ? stage.StatusText : _statusText,
                state == GeneratedMapPreviewState.Failed ? MessageType.Error : MessageType.Info);

            // 生成中も閉じる操作を残し、同型Stageを重ねない
            // Keep Close available during generation and reuse an existing preview stage
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || state is GeneratedMapPreviewState.Generating or GeneratedMapPreviewState.Closing))
                if (GUILayout.Button("生成 / 再生成")) Generate();
            using (new EditorGUI.DisabledScope(state != GeneratedMapPreviewState.Ready))
            {
                if (GUILayout.Button("スポーン地点へ移動")) stage.FrameSpawn();
                if (GUILayout.Button("全地形を表示")) stage.FrameAll();
            }
            using (new EditorGUI.DisabledScope(stage == null))
                if (GUILayout.Button("プレビューを閉じる")) StageUtility.GoToMainStage();
        }

        private void Generate()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                _statusText = "Prefab編集を閉じてから生成してください";
                Debug.LogWarning($"[GeneratedMapPreview] {_statusText}");
                return;
            }
            var stage = StageUtility.GetCurrentStage() as GeneratedMapPreviewStage;
            if (stage == null)
            {
                // 新しいプレビューはMainStageから入り、ユーザーのPrefab編集は閉じない
                // Enter a new preview from MainStage without closing the user's prefab editing
                StageUtility.GoToMainStage();
                stage = CreateInstance<GeneratedMapPreviewStage>();
                StageUtility.GoToStage(stage, true);
                if (StageUtility.GetCurrentStage() != stage)
                {
                    _statusText = "プレビューStageを開けませんでした。Consoleを確認してください。";
                    Debug.LogError($"[GeneratedMapPreview] {_statusText}");
                    return;
                }
            }
            stage.Regenerate();
        }
    }
}
#endif
