using MapGenerator.Pipeline;
using MapGenerator.Pipeline.Config;
using UnityEditor;
using UnityEngine;

namespace MapGenerator.Editor
{
    /// <summary>
    /// InfiniteTerrainManagerのカスタムインスペクタ。
    /// MapGeneratorEditorと同じSO展開描画・デバウンス付き自動再生成を提供する。
    /// </summary>
    [CustomEditor(typeof(InfiniteTerrainManager))]
    public class InfiniteTerrainManagerEditor : UnityEditor.Editor
    {
        // デバウンス制御：パラメータ変更から0.5秒後に再生成を発火する
        double _lastChangeTime;
        bool _pendingGenerate;
        bool _isGenerating;

        static readonly string[] BiomeFieldNames =
        {
            "grassland", "forest", "savanna", "desert",
            "mesa", "alpine", "jungle", "woods"
        };

        void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void OnUndoRedo()
        {
            var mgr = target as InfiniteTerrainManager;
            if (mgr == null || !mgr.autoGenerate) return;

            _lastChangeTime = EditorApplication.timeSinceStartup;
            _pendingGenerate = true;
        }

        public override void OnInspectorGUI()
        {
            var mgr = (InfiniteTerrainManager)target;

            // Generate / Clear / Auto ボタン行
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate All", GUILayout.Height(35)))
                ExecuteRegenerate(mgr);

            if (GUILayout.Button("Clear", GUILayout.Height(35), GUILayout.Width(60)))
            {
                mgr.ClearAllChunks();
                SceneView.RepaintAll();
            }

            var autoGenProp = serializedObject.FindProperty("autoGenerate");
            autoGenProp.boolValue = EditorGUILayout.ToggleLeft(
                "Auto", autoGenProp.boolValue, GUILayout.Width(60));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // baseConfig SO参照と他のプロパティを描画
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            var prop = serializedObject.GetIterator();
            prop.NextVisible(true); // m_Script をスキップ
            while (prop.NextVisible(false))
            {
                if (prop.name == "autoGenerate") continue;
                EditorGUILayout.PropertyField(prop, true);
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                ScheduleAutoGenerate(mgr.autoGenerate);
            }

            // baseConfig SOが割り当て済みなら内容をインライン展開
            if (mgr.baseConfig != null)
                DrawConfigInline(mgr.baseConfig, mgr.autoGenerate);
        }

        /// <summary>
        /// config SOの全プロパティをインライン描画
        /// </summary>
        void DrawConfigInline(TerrainGenerationConfig config, bool autoGenerate)
        {
            var configSO = new SerializedObject(config);
            configSO.Update();
            EditorGUI.BeginChangeCheck();

            var prop = configSO.GetIterator();
            prop.NextVisible(true);
            while (prop.NextVisible(false))
            {
                if (IsBiomeField(prop.name))
                {
                    DrawBiomeSoField(configSO, prop, autoGenerate);
                    continue;
                }
                EditorGUILayout.PropertyField(prop, true);
            }

            if (EditorGUI.EndChangeCheck())
            {
                configSO.ApplyModifiedProperties();
                ScheduleAutoGenerate(autoGenerate);
            }
        }

        void DrawBiomeSoField(SerializedObject configSO, SerializedProperty biomeProp, bool autoGenerate)
        {
            EditorGUILayout.PropertyField(biomeProp, false);

            var biomeObj = biomeProp.objectReferenceValue;
            if (biomeObj == null) return;

            string foldKey = $"InfTerrain_Biome_{biomeProp.name}";
            bool expanded = EditorPrefs.GetBool(foldKey, false);
            expanded = EditorGUILayout.Foldout(expanded, $"  {biomeProp.displayName} 詳細", true);
            EditorPrefs.SetBool(foldKey, expanded);

            if (!expanded) return;

            EditorGUI.indentLevel++;
            var biomeSO = new SerializedObject(biomeObj);
            biomeSO.Update();
            EditorGUI.BeginChangeCheck();

            var bp = biomeSO.GetIterator();
            bp.NextVisible(true);
            while (bp.NextVisible(false))
                EditorGUILayout.PropertyField(bp, true);

            if (EditorGUI.EndChangeCheck())
            {
                biomeSO.ApplyModifiedProperties();
                ScheduleAutoGenerate(autoGenerate);
            }

            EditorGUI.indentLevel--;
        }

        static bool IsBiomeField(string name)
        {
            foreach (var n in BiomeFieldNames)
                if (n == name) return true;
            return false;
        }

        void ScheduleAutoGenerate(bool autoGenerate)
        {
            if (!autoGenerate) return;
            _lastChangeTime = EditorApplication.timeSinceStartup;
            _pendingGenerate = true;
        }

        void OnEditorUpdate()
        {
            if (!_pendingGenerate || _isGenerating) return;
            if (EditorApplication.timeSinceStartup - _lastChangeTime < 0.5) return;

            _pendingGenerate = false;

            var mgr = target as InfiniteTerrainManager;
            if (mgr == null || !mgr.autoGenerate) return;

            ExecuteRegenerate(mgr);
            Repaint();
        }

        void ExecuteRegenerate(InfiniteTerrainManager mgr)
        {
            _isGenerating = true;
            try
            {
                mgr.RegenerateAllChunks();
                // スポーン探索が走った場合、再計算された spawnWorldPosition をアセット保存対象に登録
                // （runtime側はSetDirtyできないため、ドメイン/シーン再読込でも契約が失われないよう永続化）
                if (mgr.baseConfig != null && mgr.baseConfig.useSpawnOffsetSearch)
                    EditorUtility.SetDirty(mgr.baseConfig);
                foreach (var t in mgr.GetComponentsInChildren<Terrain>())
                    EditorUtility.SetDirty(t.terrainData);
            }
            finally
            {
                _isGenerating = false;
            }
        }

    }
}
