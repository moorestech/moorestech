using System.IO;
using Game.Paths;
using Server.Boot;
using UnityEditor;
using UnityEngine;

// map.jsonをエディタモードのシーンへ展開し、編集後に再度map.jsonへ書き出すオーサリングツール
// Authoring tool that expands map.json into an edit-mode scene and writes it back after editing
public class MapAuthoringWindow : EditorWindow
{
    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "map.jsonをシーンへ展開(Import)し、編集後にmap.jsonへ書き出す(Export)ツール。\n" +
            "ExportはMapAuthoringRoot配下のみを走査し、instanceIdを決定論的順序で振り直します。\n" +
            "新規オブジェクトはMapAuthoringRoot配下に配置してください。",
            MessageType.Info);

        // PlayMode中はランタイム生成物と競合するため操作を禁止する
        // Block all operations during play mode to avoid fighting runtime-instantiated objects
        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("PlayMode中は使用できません。", MessageType.Warning);
            return;
        }

        var defaultMapJsonPath = WorldDataDirectory.ServerDataMapJsonPath(ServerDirectory.GetDirectory());
        var defaultDirectory = Path.GetDirectoryName(defaultMapJsonPath);

        // Import: map.jsonを選択してシーンへ展開する
        // Import: pick a map.json and expand it into the scene
        if (GUILayout.Button("Import map.json → Scene"))
        {
            var path = EditorUtility.OpenFilePanel("Import map.json", defaultDirectory, "json");
            if (path.Length != 0) MapAuthoringImporter.Import(path);
        }

        // Export: シーンを走査してmap.jsonへ書き出す
        // Export: scan the scene and write it out as map.json
        if (GUILayout.Button("Export Scene → map.json"))
        {
            var path = EditorUtility.SaveFilePanel("Export map.json", defaultDirectory, "map", "json");
            if (path.Length != 0) MapAuthoringExporter.Export(path);
        }
    }

    [MenuItem("moorestech/MapAuthoring")]
    private static void ShowWindow()
    {
        var window = GetWindow<MapAuthoringWindow>();
        window.titleContent = new GUIContent("MapAuthoring");
        window.Show();
    }
}
