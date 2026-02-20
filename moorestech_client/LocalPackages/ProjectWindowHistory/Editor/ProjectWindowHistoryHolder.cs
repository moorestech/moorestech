using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectWindowHistory
{
    /// <summary>
    /// ProjectWindowとHistoryのペアを保持しておくScriptableSingleton
    /// アセットとして保存はしてないので、Unityエディタ再起動時には履歴情報は消える
    /// </summary>
    public class ProjectWindowHistoryHolder : ScriptableSingleton<ProjectWindowHistoryHolder>
    {
        [SerializeField] private List<ProjectWindowHistorySaveData> _saveDataList = new();

        public ProjectWindowHistory GetHistory(EditorWindow targetWindow)
        {
            return _saveDataList.FirstOrDefault(data => data.WindowInstanceId == targetWindow.GetInstanceID())?.History;
        }

        public void Add(EditorWindow targetWindow, ProjectWindowHistory history)
        {
            var saveData = new ProjectWindowHistorySaveData(targetWindow, history);
            _saveDataList.Add(saveData);
        }
    }

    [Serializable]
    public class ProjectWindowHistorySaveData
    {
        [SerializeField] private int _windowInstanceId;
        [SerializeField] private ProjectWindowHistory _history;

        public int WindowInstanceId => _windowInstanceId;
        public ProjectWindowHistory History => _history;

        public ProjectWindowHistorySaveData(EditorWindow window, ProjectWindowHistory history)
        {
            _windowInstanceId = window.GetInstanceID();
            _history = history;
        }
    }
}