using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectWindowHistory
{
    /// <summary>
    /// ProjectWindowの履歴を保持するクラス
    /// </summary>
    [Serializable]
    public class ProjectWindowHistory
    {
        [SerializeField] private List<ProjectWindowHistoryRecord> _records;
        [SerializeField] private int _currentRecordIndex;

        public ProjectWindowHistoryRecord CurrentRecord =>
            _currentRecordIndex >= 0 && _currentRecordIndex < _records.Count ? _records[_currentRecordIndex] : null;

        public bool CanUndo => _currentRecordIndex > 0;
        public bool CanRedo => _currentRecordIndex < _records.Count - 1;

        // 履歴レコードの最大数
        private const int MaxRecordCount = 50;

        public ProjectWindowHistory()
        {
            _records = new List<ProjectWindowHistoryRecord>(MaxRecordCount);
            _currentRecordIndex = -1;
        }

        /// <summary>
        /// 現在の状態をセット
        /// </summary>
        /// <param name="record"></param>
        public void SetCurrentRecord(ProjectWindowHistoryRecord record)
        {
            // Redo側のレコードを削除
            if (_currentRecordIndex < _records.Count - 1)
            {
                _records.RemoveRange(_currentRecordIndex + 1, _records.Count - _currentRecordIndex - 1);
            }

            // レコードが最大数を超えていたら古いものから削除
            if (_records.Count >= MaxRecordCount)
            {
                var overCount = _records.Count - MaxRecordCount + 1;
                _records.RemoveRange(0, overCount);
                _currentRecordIndex -= overCount;
            }

            _records.Add(record);
            _currentRecordIndex++;
        }

        /// <summary>
        /// Undo操作
        /// </summary>
        /// <returns></returns>
        public ProjectWindowHistoryRecord Undo()
        {
            RemoveInvalidRecords();

            if (!CanUndo)
            {
                return null;
            }

            _currentRecordIndex--;
            return CurrentRecord;
        }

        /// <summary>
        /// Redo操作
        /// </summary>
        /// <returns></returns>
        public ProjectWindowHistoryRecord Redo()
        {
            RemoveInvalidRecords();

            if (!CanRedo)
            {
                return null;
            }

            _currentRecordIndex++;
            return CurrentRecord;
        }

        /// <summary>
        /// 複数回Undoをまとめて行う
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public ProjectWindowHistoryRecord UndoMultiple(int count)
        {
            RemoveInvalidRecords();

            for (var i = 0; i < count; i++)
            {
                if (CanUndo)
                {
                    Undo();
                }
            }

            return CurrentRecord;
        }

        /// <summary>
        /// 複数回Redoをまとめて行う
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public ProjectWindowHistoryRecord RedoMultiple(int count)
        {
            RemoveInvalidRecords();

            for (var i = 0; i < count; i++)
            {
                if (CanRedo)
                {
                    Redo();
                }
            }

            return CurrentRecord;
        }

        /// <summary>
        /// Undoレコード一覧を返す
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ProjectWindowHistoryRecord> GetUndoHistoryRecordList()
        {
            RemoveInvalidRecords();
            return CanUndo ? _records.Take(_currentRecordIndex) : new List<ProjectWindowHistoryRecord>();
        }

        /// <summary>
        /// Redoレコード一覧を返す
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ProjectWindowHistoryRecord> GetRedoHistoryRecordList()
        {
            RemoveInvalidRecords();
            return CanRedo ? _records.Skip(_currentRecordIndex + 1) : new List<ProjectWindowHistoryRecord>();
        }

        /// <summary>
        /// Invalidなレコードを削除する
        /// </summary>
        private void RemoveInvalidRecords()
        {
            for (var i = _records.Count - 1; i >= 0; i--)
            {
                var record = _records[i];
                if (record.IsValid())
                {
                    continue;
                }

                _records.RemoveAt(i);
                if (i <= _currentRecordIndex)
                {
                    _currentRecordIndex--;
                }
            }
        }

        /// <summary>
        /// 履歴をクリアする
        /// </summary>
        public void Clear()
        {
            _records.Clear();
            _currentRecordIndex = -1;
        }
    }
}