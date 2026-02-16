using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace ProjectWindowHistory
{
    /// <summary>
    /// 各ProjectWindowHistoryViewを管理するクラス
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectWindowHistoryManager
    {
        private static readonly Dictionary<EditorWindow, ProjectWindowHistoryView> _historyViews = new();
        private static bool _isInitialized;

        static ProjectWindowHistoryManager()
        {
            _isInitialized = false;
            EditorApplication.update += OnUpdate;
        }

        private static void AllProjectWindowUpdate()
        {
            var windows = ProjectWindowReflectionUtility.GetAllProjectWindows();
            foreach (var window in windows)
            {
                UpdateProjectWindow(window);
            }
        }

        private static void OnUpdate()
        {
            // 1フレーム目で初期化
            if (!_isInitialized)
            {
                AllProjectWindowUpdate();
                _isInitialized = true;
            }

            // 既に閉じられたProjectWindowがあればDictionaryから消す
            var closedPairs = _historyViews.Where(pair => pair.Key == null).ToList();
            foreach (var (window, view) in closedPairs)
            {
                view.Destroy();
                _historyViews.Remove(window);
            }

            // 最後に操作したProjectWindowを取得
            var lastProjectWindow = ProjectWindowReflectionUtility.GetLastProjectWindow();
            if (lastProjectWindow == null)
            {
                return;
            }

            UpdateProjectWindow(lastProjectWindow);
        }

        private static void UpdateProjectWindow(EditorWindow projectWindow)
        {
            // 保存している履歴を取得、なければ新規作成
            var history = ProjectWindowHistoryHolder.instance.GetHistory(projectWindow);
            if (history == null)
            {
                history = new ProjectWindowHistory();
                ProjectWindowHistoryHolder.instance.Add(projectWindow, history);
            }

            // 新規ProjectWindowならViewを作成してDictionaryに追加
            if (!_historyViews.ContainsKey(projectWindow))
            {
                var view = new ProjectWindowHistoryView(projectWindow, history);
                _historyViews.Add(projectWindow, view);
            }

            // Viewの更新処理を呼ぶ
            _historyViews[projectWindow].OnUpdate();
        }
    }
}