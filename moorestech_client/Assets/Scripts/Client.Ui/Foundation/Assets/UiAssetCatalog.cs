using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Assets
{
    /// <summary>
    /// GameUi の部品・画面の USS を 1 か所で持つカタログ。GameUiRoot が参照し、欠けは起動で止める（D-6）
    /// Single catalog of GameUi component and screen USS; GameUiRoot references it and a missing entry stops startup (D-6)
    /// </summary>
    /// <remarks>
    /// 拡張点: USS を足すときはフィールドと読み出しを 1 組足し、ValidateAllAssigned の検査表へ足し、資産へ uloop で参照を入れる
    /// Extension: to add a USS, add one field and accessor pair, add it to ValidateAllAssigned's table, and assign it on the asset via uloop
    /// </remarks>
    public sealed class UiAssetCatalog : ScriptableObject
    {
        [SerializeField] private StyleSheet gamePanel;
        [SerializeField] private StyleSheet panelActionButton;
        [SerializeField] private StyleSheet modeSwitch;
        [SerializeField] private StyleSheet toast;
        [SerializeField] private StyleSheet pauseMenu;
        [SerializeField] private StyleSheet settings;
        [SerializeField] private StyleSheet bugReport;

        // シリアライズ値の公開に限った素通しの読み出し（D-24）
        // Pass-through accessors limited to exposing serialized values (D-24)
        public StyleSheet GamePanel => gamePanel;
        public StyleSheet PanelActionButton => panelActionButton;
        public StyleSheet ModeSwitch => modeSwitch;
        public StyleSheet Toast => toast;
        public StyleSheet PauseMenu => pauseMenu;
        public StyleSheet Settings => settings;
        public StyleSheet BugReport => bugReport;

        public void ValidateAllAssigned()
        {
            // 欠けた名前を全部並べて 1 回で知らせる
            // Report every missing name at once
            var missing = new List<string>();
            AddIfMissing(gamePanel, nameof(gamePanel));
            AddIfMissing(panelActionButton, nameof(panelActionButton));
            AddIfMissing(modeSwitch, nameof(modeSwitch));
            AddIfMissing(toast, nameof(toast));
            AddIfMissing(pauseMenu, nameof(pauseMenu));
            AddIfMissing(settings, nameof(settings));
            AddIfMissing(bugReport, nameof(bugReport));
            if (0 < missing.Count) throw new InvalidOperationException($"[UiAssetCatalog] unassigned style sheets on {name}: {string.Join(", ", missing)}");

            #region Internal

            void AddIfMissing(StyleSheet sheet, string fieldName)
            {
                if (sheet == null) missing.Add(fieldName);
            }

            #endregion
        }
    }
}
