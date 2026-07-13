using MapGenerator.Pipeline;
using MapGenerator.Pipeline.Config;
using UnityEditor;
using UnityEngine;

namespace MapGenerator.Editor
{
    /// <summary>
    /// MapGeneratorFacadeのカスタムインスペクタ。
    /// config SOとバイオームSOの内容をインライン展開し、単一Inspectorで全パラメータを編集可能にする。
    /// パラメータ変更時のデバウンス付き自動生成にも対応。
    /// </summary>
    [CustomEditor(typeof(MapGeneratorFacade))]
    public class MapGeneratorEditor : UnityEditor.Editor
    {
        // デバウンス制御：パラメータ変更から0.3秒後に生成を発火する
        private double _lastChangeTime;
        private bool _pendingGenerate;
        private bool _isGenerating;

        // バイオームSOのフォールド状態をキャッシュ
        private static readonly string[] BiomeFieldNames =
        {
            "grassland", "forest", "savanna", "desert",
            "mesa", "alpine", "jungle", "woods"
        };

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        /// <summary>
        /// Undo/Redo時にパラメータが巻き戻るので、autoGenerate ONならデバウンス経由で再生成する
        /// </summary>
        private void OnUndoRedo()
        {
            var facade = target as MapGeneratorFacade;
            if (facade == null || !facade.autoGenerate) return;

            _lastChangeTime = EditorApplication.timeSinceStartup;
            _pendingGenerate = true;
        }

        public override void OnInspectorGUI()
        {
            var facade = (MapGeneratorFacade)target;

            // 生成ボタンとautoGenerateチェックボックスを横並びで配置
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate Map", GUILayout.Height(35)))
            {
                ExecuteGenerate(facade);
            }

            // autoGenerateチェックボックスをボタン右に配置
            var autoGenProp = serializedObject.FindProperty("autoGenerate");
            autoGenProp.boolValue = EditorGUILayout.ToggleLeft("Auto", autoGenProp.boolValue, GUILayout.Width(60));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // config SO参照スロットを描画
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            var configProp = serializedObject.FindProperty("config");
            EditorGUILayout.PropertyField(configProp);

            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();

            // config SOが割り当て済みなら内容をインライン展開
            if (facade.config != null)
                DrawConfigInline(facade.config, facade.autoGenerate);
        }

        /// <summary>
        /// config SOの全プロパティをインライン描画。バイオームSOは更にネスト展開する
        /// </summary>
        private void DrawConfigInline(TerrainGenerationConfig config, bool autoGenerate)
        {
            var configSO = new SerializedObject(config);
            configSO.Update();
            EditorGUI.BeginChangeCheck();

            var prop = configSO.GetIterator();
            prop.NextVisible(true); // m_Script をスキップ
            while (prop.NextVisible(false))
            {
                // バイオームSOフィールドはカスタム描画
                if (IsBiomeField(prop.name))
                {
                    DrawBiomeSoField(configSO, prop);
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

        /// <summary>
        /// バイオームSOの参照スロット＋内容をFoldoutで展開描画する
        /// </summary>
        private void DrawBiomeSoField(SerializedObject configSO, SerializedProperty biomeProp)
        {
            EditorGUILayout.PropertyField(biomeProp, false);

            var biomeObj = biomeProp.objectReferenceValue;
            if (biomeObj == null) return;

            // Foldoutで展開（EditorPrefsでセッション間の開閉状態を保持）
            string foldKey = $"MapGen_Biome_{biomeProp.name}";
            bool expanded = EditorPrefs.GetBool(foldKey, false);
            expanded = EditorGUILayout.Foldout(expanded, $"  {biomeProp.displayName} 詳細", true);
            EditorPrefs.SetBool(foldKey, expanded);

            if (!expanded) return;

            EditorGUI.indentLevel++;
            var biomeSO = new SerializedObject(biomeObj);
            biomeSO.Update();
            EditorGUI.BeginChangeCheck();

            var bp = biomeSO.GetIterator();
            bp.NextVisible(true); // m_Script をスキップ
            while (bp.NextVisible(false))
                EditorGUILayout.PropertyField(bp, true);

            if (EditorGUI.EndChangeCheck())
            {
                biomeSO.ApplyModifiedProperties();
                // バイオームSOの変更もautoGenerate対象
                var facade = target as MapGeneratorFacade;
                if (facade != null)
                    ScheduleAutoGenerate(facade.autoGenerate);
            }

            EditorGUI.indentLevel--;
        }

        private static bool IsBiomeField(string name)
        {
            foreach (var n in BiomeFieldNames)
                if (n == name) return true;
            return false;
        }

        private void ScheduleAutoGenerate(bool autoGenerate)
        {
            if (!autoGenerate) return;
            _lastChangeTime = EditorApplication.timeSinceStartup;
            _pendingGenerate = true;
        }

        /// <summary>
        /// デバウンスタイマー監視。0.3秒間パラメータ変更がなければ自動生成を実行する
        /// </summary>
        private void OnEditorUpdate()
        {
            if (!_pendingGenerate || _isGenerating) return;
            if (EditorApplication.timeSinceStartup - _lastChangeTime < 0.3) return;

            _pendingGenerate = false;

            var facade = target as MapGeneratorFacade;
            if (facade == null || !facade.autoGenerate) return;

            ExecuteGenerate(facade);
            Repaint();
        }

        /// <summary>
        /// Undo対応付きでマップ生成を実行する。手動ボタンと自動生成の共通処理
        /// </summary>
        private void ExecuteGenerate(MapGeneratorFacade facade)
        {
            var terrains = facade.CollectTerrains();
            if (terrains.Length == 0) return;

            _isGenerating = true;
            try
            {
                facade.Generate();

                foreach (var t in terrains)
                    EditorUtility.SetDirty(t.terrainData);
            }
            finally
            {
                _isGenerating = false;
            }
        }

    }
}
