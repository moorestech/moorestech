using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using SearchViewState = ProjectWindowHistory.ProjectWindowReflectionUtility.SearchViewState;

namespace ProjectWindowHistory
{
    /// <summary>
    /// ProjectWindowにUndo/Redoボタンを追加し、押下時に履歴を辿る処理を呼び出す
    /// 選択フォルダと検索結果の変化を検知し、履歴を追加する
    /// </summary>
    public class ProjectWindowHistoryView
    {
        private readonly EditorWindow _projectWindow;
        private readonly ProjectWindowHistory _history;
        private Button _undoButton;
        private Button _redoButton;

        private bool _isOneColumnViewMode; // 1カラムビューか
        private float _timeAddToHistoryForSearchedText; // 現在の検索文字列が入力完了してからの経過時間
        private string _lastSearchedText; // 前フレームの検索文字列
        private const float DurationForInputCompleted = 2f; // 入力完了と判断する秒数

        public ProjectWindowHistoryView(EditorWindow projectWindow, ProjectWindowHistory history)
        {
            _projectWindow = projectWindow;
            _history = history;

            CreateButton();
            RefreshButtons();
        }

        /// <summary>
        /// Undo/Redoボタンを作成する
        /// </summary>
        private void CreateButton()
        {
            const float buttonWidth = 20f;

#if UNITY_2022_2_OR_NEWER
            // Unity2022.2以降ではSearchByImportLogTypeボタンが増えたため、その分ボタンの位置を左にずらす
            const float buttonMarginRight = 470f;
#else
            const float buttonMarginRight = 440f;
#endif

            _undoButton = new Button(Undo)
            {
                text = "<",
                focusable = false,
                style =
                {
                    width = buttonWidth,
                    position = new StyleEnum<Position>(Position.Absolute),
                    right = buttonMarginRight + buttonWidth
                }
            };
            _redoButton = new Button(Redo)
            {
                text = ">",
                focusable = false,
                style =
                {
                    width = buttonWidth,
                    position = new StyleEnum<Position>(Position.Absolute),
                    right = buttonMarginRight
                }
            };

            // 右クリックで履歴一覧を表示
            _undoButton.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 1)
                {
                    ShowHistoryRecordListMenu(true);
                }
            });
            _redoButton.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 1)
                {
                    ShowHistoryRecordListMenu(false);
                }
            });

            _projectWindow.rootVisualElement.Add(_undoButton);
            _projectWindow.rootVisualElement.Add(_redoButton);
        }

        private void Undo()
        {
            _isOneColumnViewMode = ProjectWindowReflectionUtility.IsOneColumnViewMode(_projectWindow);
            if (_isOneColumnViewMode)
            {
                return;
            }

            var record = _history.Undo();
            if (record != null)
            {
                ApplyHistoryRecord(record);
            }
        }

        private void Redo()
        {
            _isOneColumnViewMode = ProjectWindowReflectionUtility.IsOneColumnViewMode(_projectWindow);
            if (_isOneColumnViewMode)
            {
                return;
            }

            var record = _history.Redo();
            if (record != null)
            {
                ApplyHistoryRecord(record);
            }
        }

        /// <summary>
        /// 履歴情報をProjectWindowに反映する
        /// </summary>
        /// <param name="record"></param>
        private void ApplyHistoryRecord(ProjectWindowHistoryRecord record)
        {
            ProjectWindowReflectionUtility.SetFolderSelection(_projectWindow, record.SelectedFolderInstanceIDs);
            ProjectWindowReflectionUtility.SetSearch(_projectWindow, record.SearchedText, record.SelectedFolderInstanceIDs);
            if (record.SearchViewState > 0)
            {
                ProjectWindowReflectionUtility.SetSearchViewState(_projectWindow, record.SearchViewState);
            }

            RefreshButtons();
        }

        /// <summary>
        /// Undo/Redoボタンの状態を更新する
        /// </summary>
        private void RefreshButtons()
        {
            _undoButton.SetEnabled(!_isOneColumnViewMode && _history.CanUndo);
            _redoButton.SetEnabled(!_isOneColumnViewMode && _history.CanRedo);
        }

        public void OnUpdate()
        {
            // 1カラムビューかを取得
            _isOneColumnViewMode = ProjectWindowReflectionUtility.IsOneColumnViewMode(_projectWindow);
            if (_isOneColumnViewMode)
            {
                // 現状1カラムビューは未対応なので、ボタン状態の更新だけして終了
                RefreshButtons();
                return;
            }

            CheckSearchedText();
            CheckSelectedFolder();
        }

        /// <summary>
        /// 検索文字列を履歴に追加するかチェック
        /// </summary>
        private void CheckSearchedText()
        {
            var searchedText = ProjectWindowReflectionUtility.GetSearchedText(_projectWindow); // 現在の検索文字列
            var realtimeSinceStartup = Time.realtimeSinceStartup;

            // 検索文字列の入力完了からの経過時間を更新
            if (searchedText != _lastSearchedText)
            {
                _timeAddToHistoryForSearchedText = realtimeSinceStartup + DurationForInputCompleted;
                _lastSearchedText = searchedText;
            }

            // 入力完了からの経過時間が閾値を超えていなければ、入力中と判断して何もしない
            if (realtimeSinceStartup < _timeAddToHistoryForSearchedText)
            {
                return;
            }

            // 検索文字列が空なら何もしない
            if (string.IsNullOrEmpty(searchedText))
            {
                return;
            }

            // 検索範囲が指定されていない（SearchViewState.NotSearching）なら何もしない
            var searchViewState = ProjectWindowReflectionUtility.GetSearchViewState(_projectWindow);
            if (searchViewState == SearchViewState.NotSearching)
            {
                return;
            }

            var currentRecord = _history.CurrentRecord;

            // 検索文字列が最新履歴と変わったら履歴に追加
            var isSearchedTextChanged = searchedText != currentRecord?.SearchedText;
            if (isSearchedTextChanged)
            {
                var record = new ProjectWindowHistoryRecord(currentRecord?.SelectedFolderInstanceIDs, searchedText, searchViewState);
                _history.SetCurrentRecord(record);
                RefreshButtons();
            }

            // 検索範囲が変わっただけなら、最新履歴の検索範囲を更新
            var isSearchViewStateChanged = searchViewState != currentRecord?.SearchViewState;
            if (isSearchViewStateChanged)
            {
                currentRecord?.ChangeSearchViewState(searchViewState);
            }
        }

        /// <summary>
        /// 選択フォルダを履歴に追加するかチェック
        /// </summary>
        private void CheckSelectedFolder()
        {
            var selectedFolderInstanceIds = ProjectWindowReflectionUtility.GetLastFolderInstanceIds(_projectWindow);
            var isFolderSelected = selectedFolderInstanceIds != null && selectedFolderInstanceIds.Any();

            // ツリービューでフォルダが選択されていなければ何もしない
            if (!isFolderSelected)
            {
                return;
            }

            selectedFolderInstanceIds = selectedFolderInstanceIds
                .Where(instanceId => AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(instanceId)))
                .ToArray();
            var lastRecord = _history.CurrentRecord;
            var lastSelectedFolderInstanceIds = lastRecord?.SelectedFolderInstanceIDs;
            var isFirstFolderSelected = lastSelectedFolderInstanceIds == null;

            // 初めてフォルダを選択した、もしくは選択フォルダが最新履歴と変わったら履歴に追加
            if (isFirstFolderSelected || !selectedFolderInstanceIds.SequenceEqual(lastSelectedFolderInstanceIds))
            {
                // 検索範囲が選択フォルダ内なら検索を維持しつつフォルダ選択され、それ以外の検索範囲なら検索はリセットされる
                var isSearchedSubFolders = lastRecord?.SearchViewState == SearchViewState.SubFolders;
                var searchedText = isSearchedSubFolders ? lastRecord.SearchedText : null;
                var searchViewState = isSearchedSubFolders ? SearchViewState.SubFolders : SearchViewState.NotSearching;

                // 履歴に追加する
                var record = new ProjectWindowHistoryRecord(selectedFolderInstanceIds, searchedText, searchViewState);
                _history.SetCurrentRecord(record);
                RefreshButtons();
            }
        }

        /// <summary>
        /// 履歴一覧を表示
        /// </summary>
        /// <param name="isUndo"></param>
        private void ShowHistoryRecordListMenu(bool isUndo)
        {
            var menu = new GenericMenu();
            var recordList = isUndo ? _history.GetUndoHistoryRecordList().Reverse().ToList() : _history.GetRedoHistoryRecordList().ToList();
            for (var i = 0; i < recordList.Count; i++)
            {
                var labelText = recordList[i].ToLabelText();

                var operationCount = i + 1;
                menu.AddItem(new GUIContent(labelText), false, () =>
                {
                    var record = isUndo ? _history.UndoMultiple(operationCount) : _history.RedoMultiple(operationCount);
                    ApplyHistoryRecord(record);
                });
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("UndoRedo履歴を全削除"), false, () =>
            {
                _history.Clear();
                RefreshButtons();
            });

            menu.ShowAsContext();
        }

        public void Destroy()
        {
            _undoButton.RemoveFromHierarchy();
            _redoButton.RemoveFromHierarchy();
        }
    }
}