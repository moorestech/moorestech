using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MapGenerator.Editor
{
    /// <summary>
    /// シーン内にインライン保存（TerrainDataがアセット化されていない）されているTerrainを
    /// .assetファイルとして書き出すエディタ拡張。
    /// InfiniteTerrainManagerがエディットモードで生成する new TerrainData() はディスク未保存のため、
    /// このツールでアセット化して永続化する。
    ///
    /// AssetDatabase.CreateAssetはライブのTerrainDataインスタンスをそのままアセット化するため、
    /// Terrain/TerrainColliderの参照は自動的に保存済みアセットへ張り替わる（別途relink不要）。
    /// </summary>
    public class SceneOnlyTerrainExporter : EditorWindow
    {
        const string DefaultOutputFolder = "Assets/MapGenerator/TerrainData/SceneOnly";

        [SerializeField] GameObject _root;
        [SerializeField] string _outputFolder = DefaultOutputFolder;
        [SerializeField] bool _overwriteExisting = true;

        Vector2 _scroll;

        [MenuItem("Tools/MapGenerator/Export Scene-Only Terrain")]
        public static void ShowWindow()
        {
            GetWindow<SceneOnlyTerrainExporter>("Scene-Only Terrain Exporter");
        }

        void OnGUI()
        {
            GUILayout.Label("Scene-Only Terrain Exporter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "シーンにインライン保存されているTerrain（TerrainDataがアセット化されていないもの）を" +
                ".assetとして書き出します。書き出し後、Terrainの参照は保存済みアセットへ自動で張り替わります。\n" +
                "シーン保存を忘れずに行ってください。",
                MessageType.Info);

            EditorGUILayout.Space();

            _root = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Root (任意)", "指定した場合はその子のTerrainのみ対象。未指定ならアクティブシーン全体を走査。"),
                _root, typeof(GameObject), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                    BrowseOutputFolder();
            }

            _overwriteExisting = EditorGUILayout.Toggle(
                new GUIContent("Overwrite Existing", "同名アセットが存在する場合に上書きする。OFFなら連番で別名保存。"),
                _overwriteExisting);

            EditorGUILayout.Space();

            var sceneOnly = CollectSceneOnlyTerrains();
            EditorGUILayout.LabelField($"Scene-only Terrains: {sceneOnly.Count}", EditorStyles.boldLabel);

            if (sceneOnly.Count > 0)
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(160));
                foreach (var t in sceneOnly)
                    EditorGUILayout.LabelField("• " + GetHierarchyPath(t.transform));
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "対象のTerrainが見つかりません。すべてのTerrainが既にアセット化済み、" +
                    "またはシーンにTerrainがありません。",
                    MessageType.None);
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(sceneOnly.Count == 0 || string.IsNullOrEmpty(_outputFolder)))
            {
                if (GUILayout.Button($"Export {sceneOnly.Count} Terrain(s)", GUILayout.Height(30)))
                    Export(sceneOnly);
            }
        }

        void BrowseOutputFolder()
        {
            string abs = EditorUtility.SaveFolderPanel("Select Output Folder", DefaultOutputFolder, "");
            if (string.IsNullOrEmpty(abs)) return;

            string dataPath = Application.dataPath;
            if (abs == dataPath)
            {
                _outputFolder = "Assets";
            }
            else if (abs.StartsWith(dataPath + "/"))
            {
                _outputFolder = "Assets" + abs.Substring(dataPath.Length);
            }
            else
            {
                EditorUtility.DisplayDialog("Error",
                    "出力先はプロジェクトのAssetsフォルダ内を指定してください。", "OK");
            }
            GUI.changed = true;
        }

        /// <summary>
        /// 対象スコープ内で、TerrainDataがディスク未保存（=シーンにインライン保存）のTerrainを収集する。
        /// </summary>
        List<Terrain> CollectSceneOnlyTerrains()
        {
            IEnumerable<Terrain> terrains;
            if (_root != null)
            {
                terrains = _root.GetComponentsInChildren<Terrain>(true);
            }
            else
            {
                var scene = SceneManager_GetActiveScene();
                terrains = scene.GetRootGameObjects()
                    .SelectMany(go => go.GetComponentsInChildren<Terrain>(true));
            }

            return terrains
                .Where(t => t.terrainData != null
                            && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(t.terrainData)))
                .ToList();
        }

        void Export(List<Terrain> terrains)
        {
            int exported = RunExport(terrains, _outputFolder, _overwriteExisting, silent: false);
            EditorUtility.DisplayDialog("Export Complete",
                $"{exported}個のTerrainDataを書き出しました。\n{_outputFolder}\n\n" +
                "参照は保存済みアセットへ張り替わりました。シーンを保存してください。", "OK");
        }

        /// <summary>
        /// アクティブシーン全体のscene-only Terrainを既定フォルダへ一括書き出しするワンクリックメニュー。
        /// （ウィンドウを開かずに実行できるショートカット）
        /// </summary>
        [MenuItem("Tools/MapGenerator/Export Scene-Only Terrain (Run All)")]
        static void ExportAllInActiveScene()
        {
            var scene = SceneManager_GetActiveScene();
            var terrains = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Terrain>(true))
                .Where(t => t.terrainData != null
                            && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(t.terrainData)))
                .ToList();

            if (terrains.Count == 0)
            {
                Debug.Log("[SceneOnlyTerrainExporter] (Run All) アクティブシーンにscene-only Terrainがありません。");
                return;
            }

            int exported = RunExport(terrains, DefaultOutputFolder, overwrite: true, silent: true);
            Debug.Log($"[SceneOnlyTerrainExporter] (Run All) {exported}個のTerrainDataを書き出しました。");
        }

        /// <summary>
        /// scene-only TerrainDataを.asset化し、参照張り替え＋描画再構築まで行う共通処理。
        /// silent=trueならProgressBar等のUIを出さない（自動実行・検証用）。
        /// </summary>
        static int RunExport(List<Terrain> terrains, string outputFolder, bool overwrite, bool silent)
        {
            EnsureFolderExists(outputFolder);

            var exportedTerrains = new List<Terrain>();
            var dirtyScenes = new HashSet<UnityEngine.SceneManagement.Scene>();
            try
            {
                for (int i = 0; i < terrains.Count; i++)
                {
                    var terrain = terrains[i];
                    if (!silent)
                        EditorUtility.DisplayProgressBar("Exporting Scene-Only Terrain",
                            $"{terrain.name} ({i + 1}/{terrains.Count})", (float)i / terrains.Count);

                    string path = $"{outputFolder}/{BuildAssetName(terrain)}.asset";
                    string createPath = path;
                    bool replaceExisting = false;

                    if (AssetDatabase.LoadAssetAtPath<TerrainData>(path) != null)
                    {
                        if (overwrite)
                        {
                            replaceExisting = true;
                            createPath = AssetDatabase.GenerateUniqueAssetPath(
                                $"{outputFolder}/__tmp_{BuildAssetName(terrain)}.asset");
                        }
                        else
                        {
                            createPath = AssetDatabase.GenerateUniqueAssetPath(path);
                            path = createPath;
                        }
                    }

                    var terrainData = terrain.terrainData;
                    var textureSnapshot = TerrainTextureSnapshot.Capture(terrainData);

                    // ライブインスタンスをそのままアセット化 → 同じインスタンスがアセットの実体になる
                    AssetDatabase.CreateAsset(terrainData, createPath);
                    textureSnapshot.RestoreTo(terrainData);
                    EditorUtility.SetDirty(terrainData);

                    if (replaceExisting)
                        terrainData = ReplaceExistingAsset(createPath, path);

                    // 参照を保存済みアセットへ明示的に張り替え（Terrain/Collider両方）
                    terrain.terrainData = terrainData;
                    var collider = terrain.GetComponent<TerrainCollider>();
                    if (collider != null)
                        collider.terrainData = terrainData;

                    exportedTerrains.Add(terrain);
                    dirtyScenes.Add(terrain.gameObject.scene);
                }
            }
            finally
            {
                if (!silent)
                    EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();

            // SetAlphamaps後のTerrain描画を更新する。terrainDataをnullにするとUnity内部の
            // alphamap参照が壊れる場合があるため、参照は触らずFlushだけ行う。
            foreach (var terrain in exportedTerrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;
                terrain.Flush();
            }

            // 参照張り替えを永続化するためシーンをdirtyにする（保存はユーザー判断）
            foreach (var scene in dirtyScenes)
                EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[SceneOnlyTerrainExporter] Exported {exportedTerrains.Count} TerrainData asset(s) to {outputFolder}");
            return exportedTerrains.Count;
        }

        static TerrainData ReplaceExistingAsset(string tempPath, string destinationPath)
        {
            // 既存TerrainDataの同一パスへ直接CreateAssetすると、Refresh後にSplatAlphaが初期化される場合がある。
            // 一度別パスで保存を確定してからMoveAssetすることで、サブアセットのテクスチャを保持する。
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (AssetDatabase.LoadAssetAtPath<TerrainData>(destinationPath) != null
                && !AssetDatabase.DeleteAsset(destinationPath))
            {
                throw new IOException($"Failed to delete existing TerrainData asset: {destinationPath}");
            }

            var moveError = AssetDatabase.MoveAsset(tempPath, destinationPath);
            if (!string.IsNullOrEmpty(moveError))
                throw new IOException($"Failed to move TerrainData asset: {moveError}");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<TerrainData>(destinationPath);
        }

        /// <summary>
        /// 既存の SceneOnly/ アセットと同じ命名規則（"{親名}_{オブジェクト名}_TerrainData"）で名前を生成する。
        /// </summary>
        static string BuildAssetName(Terrain terrain)
        {
            var parent = terrain.transform.parent;
            string prefix = parent != null ? parent.name + "_" : "";
            string name = $"{prefix}{terrain.gameObject.name}_TerrainData";
            return SanitizeFileName(name);
        }

        static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        static void EnsureFolderExists(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;

            string[] parts = assetFolder.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static string GetHierarchyPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        static UnityEngine.SceneManagement.Scene SceneManager_GetActiveScene()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        }

        sealed class TerrainTextureSnapshot
        {
            readonly int _alphamapResolution;
            readonly TerrainLayer[] _terrainLayers;
            readonly float[,,] _alphamaps;

            TerrainTextureSnapshot(TerrainData terrainData)
            {
                _alphamapResolution = terrainData.alphamapResolution;
                _terrainLayers = terrainData.terrainLayers != null
                    ? terrainData.terrainLayers.ToArray()
                    : new TerrainLayer[0];

                // CreateAsset時にSplatAlphaが初期化される場合があるため、保存前のCPU側重みを明示的に退避する。
                _alphamaps = terrainData.alphamapLayers > 0
                    ? terrainData.GetAlphamaps(0, 0, _alphamapResolution, _alphamapResolution)
                    : null;
            }

            public static TerrainTextureSnapshot Capture(TerrainData terrainData)
            {
                return new TerrainTextureSnapshot(terrainData);
            }

            public void RestoreTo(TerrainData terrainData)
            {
                terrainData.terrainLayers = _terrainLayers;
                if (_alphamaps == null)
                    return;

                if (terrainData.alphamapResolution != _alphamapResolution)
                    terrainData.alphamapResolution = _alphamapResolution;

                terrainData.SetAlphamaps(0, 0, _alphamaps);
            }
        }
    }
}
