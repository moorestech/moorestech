using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Playtest.Operations.Ui
{
    /// <summary>
    ///     BP録画シナリオのメニュー操作と可視状態の検査をまとめる
    ///     Groups menu actions and visual checks for the blueprint recording scenario
    /// </summary>
    public class BlueprintCopyPasteScenarioChecks
    {
        private readonly PlaytestDriver _playtest;
        private readonly MouseCursorTooltipState _tooltip;

        public BlueprintCopyPasteScenarioChecks(PlaytestDriver playtest, MouseCursorTooltipState tooltip)
        {
            _playtest = playtest;
            _tooltip = tooltip;
        }

        public async UniTask SelectEntry(string categoryTestid, string entryTestid)
        {
            for (var attempt = 0; attempt < 3 && _playtest.CurrentUiState != UIStateEnum.BuildMenu; attempt++)
            {
                await _playtest.PressKey(_playtest.CurrentUiState == UIStateEnum.PlaceBlock ? Key.Tab : Key.B);
                if (await PlaytestUiOps.PollUiState(UIStateEnum.BuildMenu, 4f)) break;
            }
            _playtest.Assert(_playtest.CurrentUiState == UIStateEnum.BuildMenu, $"ビルドメニューが開く ({entryTestid})");
            await _playtest.UntilWebUiElement("build-menu-panel", 15f);
            await _playtest.ClickWebUi(categoryTestid);
            var deadline = Time.realtimeSinceStartup + 20f;
            while (_playtest.CurrentUiState != UIStateEnum.PlaceBlock && Time.realtimeSinceStartup < deadline)
            {
                await _playtest.ClickWebUi(entryTestid);
                await UniTask.DelayFrame(10);
            }
            _playtest.Assert(_playtest.CurrentUiState == UIStateEnum.PlaceBlock, $"エントリ選択でPlaceBlockへ遷移: {entryTestid}");
            await UniTask.Delay(TimeSpan.FromSeconds(0.6f));
        }

        public void AssertMarker(string name, Vector3Int cell, string label)
        {
            var marker = GameObject.Find(name);
            var expected = cell + new Vector3(0.5f, 0.5f, 0.5f);
            var ok = marker != null && marker.activeSelf && (marker.transform.position - expected).sqrMagnitude < 0.01f;
            _playtest.Assert(ok, $"{label} 実際:{(marker == null ? "null" : marker.activeSelf ? marker.transform.position.ToString() : "inactive")}");
        }

        public void AssertRangeBox(Vector3Int min, Vector3Int max, string label)
        {
            var box = GameObject.Find("BlueprintCopyRangeBox");
            var size = new Vector3(max.x - min.x + 1, max.y - min.y + 1, max.z - min.z + 1);
            var ok = box != null && box.activeSelf && box.transform.localScale == size;
            _playtest.Assert(ok, $"{label} size={size} 実際:{(box == null ? "null" : box.transform.localScale.ToString())}");
        }

        public bool TooltipHasParam(string value)
        {
            return _tooltip.GetPresentation().Lines.Any(line => 0 < line.TextParams.Count && line.TextParams[0] == value);
        }

        public List<Vector3> ActiveGhostPositions()
        {
            var root = GameObject.Find("BlueprintPastePreview");
            var result = new List<Vector3>();
            if (root == null) return result;
            foreach (Transform child in root.transform)
            {
                if (child.gameObject.activeSelf) result.Add(child.position);
            }
            return result;
        }
    }
}
