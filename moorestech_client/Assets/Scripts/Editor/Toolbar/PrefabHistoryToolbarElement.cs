using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Client.Editor.Toolbar
{
    /// <summary>
    /// Prefab履歴ドロップダウンをツールバーに追加する
    /// Add Prefab history dropdown to the toolbar
    /// </summary>
    public static class PrefabHistoryToolbarElement
    {
        private const string ElementPath = "moorestech/Prefab History";
        private const string EditorPrefsKey = "moorestech_PrefabHistory";
        private const int MaxHistoryCount = 30;
        private const char Separator = ',';

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // Prefabステージが開かれた時に履歴に追加する
            // Add to history when a Prefab stage is opened
            PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
        }

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Right)]
        public static MainToolbarElement CreateElement()
        {
            // アイコンのみのPrefab履歴ドロップダウンを作成
            // Create icon-only Prefab history dropdown
            var icon = ToolbarUtility.GetBuiltInIcon("d_Prefab Icon");
            var content = new MainToolbarContent(icon, "Prefab履歴 / Prefab History");
            return new MainToolbarDropdown(content, ShowDropdownMenu);
        }

        private static void ShowDropdownMenu(Rect dropDownRect)
        {
            var menu = new GenericMenu();
            var history = LoadHistory();
            var pathsToRemove = new List<string>();

            if (history.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("履歴なし / No History"));
            }
            else
            {
                // 存在するPrefabのみメニューに追加、削除済みは除外
                // Add only existing Prefabs to menu, remove deleted ones
                foreach (var assetPath in history)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                    if (asset == null)
                    {
                        pathsToRemove.Add(assetPath);
                        continue;
                    }

                    var fileName = Path.GetFileNameWithoutExtension(assetPath);
                    var path = assetPath;
                    menu.AddItem(new GUIContent(fileName), false, () => PrefabStageUtility.OpenPrefab(path));
                }

                // 削除済みPrefabを履歴から除去
                // Remove deleted Prefabs from history
                if (pathsToRemove.Count > 0)
                {
                    foreach (var p in pathsToRemove) history.Remove(p);
                    SaveHistory(history);
                }

                menu.AddSeparator("");
                menu.AddItem(new GUIContent("履歴をクリア / Clear History"), false, ClearHistory);
            }

            menu.ShowAsContext();
        }

        private static void OnPrefabStageOpened(PrefabStage prefabStage)
        {
            // 開かれたPrefabを履歴に追加
            // Add opened Prefab to history
            var assetPath = prefabStage.assetPath;
            var history = LoadHistory();

            history.Remove(assetPath);
            history.Insert(0, assetPath);

            // 最大件数を超えたら古いものを削除
            // Remove old entries if exceeding max count
            while (history.Count > MaxHistoryCount)
            {
                history.RemoveAt(history.Count - 1);
            }

            SaveHistory(history);
            MainToolbar.Refresh(ElementPath);
        }

        private static List<string> LoadHistory()
        {
            var saved = EditorPrefs.GetString(EditorPrefsKey, "");
            if (string.IsNullOrEmpty(saved)) return new List<string>();
            return saved.Split(Separator).Where(s => !string.IsNullOrEmpty(s)).ToList();
        }

        private static void SaveHistory(List<string> history)
        {
            EditorPrefs.SetString(EditorPrefsKey, string.Join(Separator.ToString(), history));
        }

        private static void ClearHistory()
        {
            EditorPrefs.DeleteKey(EditorPrefsKey);
            MainToolbar.Refresh(ElementPath);
        }
    }
}
