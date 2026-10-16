using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Playtest.Input;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Client.Playtest.Operations.Ui
{
    // 録画シナリオの削除箱入力と、画面へ届く件数・高さを検証する
    // Exercise recorded delete-box inputs and inspect the displayed count and height
    public static class DeleteBoxScenarioInput
    {
        public static async UniTask Start(PlaytestDriver driver, Vector3Int cell)
        {
            await AimAtGround(driver, cell);
            SemanticInput.KeyDown(Key.LeftShift);
            await UniTask.DelayFrame(3);
            await Click();
            SemanticInput.KeyUp(Key.LeftShift);
            await driver.Until(() => HasTooltipLine(LocalizationKeys.Ui.Tooltip.DeleteBoxTargetsInRange), 5f, "箱の終点待ちへ入る");
        }

        public static async UniTask Click()
        {
            SemanticInput.MouseButtonDown(0);
            await UniTask.DelayFrame(5);
            SemanticInput.MouseButtonUp(0);
            await UniTask.DelayFrame(3);
        }

        public static async UniTask AimAtGround(PlaytestDriver driver, Vector3Int cell)
        {
            await driver.AimAt(new Vector3(cell.x + 0.5f, cell.y - 0.02f, cell.z + 0.5f));
        }

        // 箱確定前の0件拒否・Q/E・ESCを、既存の選択を残したまま検証する
        // Check empty-range refusal, Q/E and ESC without discarding the existing selection
        public static async UniTask CheckEmptyBoxAndHeight(PlaytestDriver driver, Vector3Int emptyCell, int selectedCount)
        {
            await Start(driver, emptyCell);
            await driver.Until(() => HasTooltipLine(LocalizationKeys.Ui.Tooltip.DeleteBoxEmptyRange), 5f, "空範囲の理由が表示される");
            await Click();
            driver.Assert(HasTooltipLine(LocalizationKeys.Ui.Tooltip.DeleteBoxTargetsInRange), "0件クリックは終点待ちに留まる");
            driver.Assert(ReadRequiredTooltipCount(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount) == selectedCount, "0件クリックは既存選択を変えない");

            // 実キーで高さを往復し、マーカー位置にも反映されたことを確かめる
            // Change height through real keys and check the resulting marker positions
            var height = ClientDIContext.DIContainer.DIContainerResolver.Resolve<PlacementHeightOffset>();
            var markerY = GameObject.Find("DeleteBoxEndMarker").transform.position.y;
            await driver.PressKey(Key.E);
            await driver.Until(() => height.Value == 1, 5f, "Eで共用高さが1になる");
            driver.Assert(Mathf.Abs(GameObject.Find("DeleteBoxEndMarker").transform.position.y - markerY - 1f) < 0.01f, "Eで終点マーカーが1セル上がる");
            await driver.PressKey(Key.Q);
            await driver.Until(() => height.Value == 0, 5f, "Qで共用高さが0へ戻る");
            driver.Assert(Mathf.Abs(GameObject.Find("DeleteBoxEndMarker").transform.position.y - markerY) < 0.01f, "Qで終点マーカーが元に戻る");
            await driver.PressKey(Key.E);
            await driver.Screenshot("empty-box-height");

            await driver.PressKey(Key.Escape);
            await driver.WaitSeconds(0.2f);
            driver.Assert(driver.CurrentUiState == UIStateEnum.DeleteBar, "箱待ちESCは削除モードに留まる");
            driver.Assert(!HasTooltipLine(LocalizationKeys.Ui.Tooltip.DeleteBoxTargetsInRange), "箱待ちESCで箱を畳む");
            driver.Assert(ReadRequiredTooltipCount(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount) == selectedCount, "箱待ちESCは既存選択を残す");
            driver.Assert(height.Value == 0, "箱待ちESCで高さを地表へ戻す");
        }

        public static bool HasTooltipLine(LocalizationKey key)
        {
            return Tooltip().GetPresentation().Lines.Any(line => line.Key.Key == key.Key);
        }

        public static string TooltipText()
        {
            return string.Join("|", Tooltip().GetPresentation().Lines.Select(line => line.Key.Key + ":" + string.Join(",", line.TextParams ?? System.Array.Empty<string>())));
        }

        // 必須件数の欠落や破損は録画失敗として表面化させる
        // Fail the recording when a required count is missing or malformed
        public static int ReadRequiredTooltipCount(LocalizationKey key)
        {
            var line = Tooltip().GetPresentation().Lines.FirstOrDefault(item => item.Key.Key == key.Key);
            if (line.TextParams != null && 0 < line.TextParams.Count && int.TryParse(line.TextParams[0], out var count)) return count;
            var actual = line.TextParams == null ? "<missing>" : string.Join(",", line.TextParams);
            throw new System.InvalidOperationException($"Required tooltip count {key.Key} is invalid: {actual}");
        }

        private static MouseCursorTooltipState Tooltip()
        {
            return ClientDIContext.DIContainer.DIContainerResolver.Resolve<MouseCursorTooltipState>();
        }

        // 実レイキャストで対象のブロックを拾えるコライダー中心を照準する
        // Aim at a collider center that the real delete raycast resolves to the intended block
        public static async UniTask AimAtCell(PlaytestDriver driver, Vector3Int pos)
        {
            var block = ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(pos);
            foreach (var point in block.GetComponentsInChildren<Collider>().Select(c => c.bounds.center).OrderBy(c => Vector3.Distance(c, Camera.main.transform.position)).ToList())
            {
                await driver.AimAt(point);
                var target = DeleteTargetRaycaster.AimAt(DeleteAimFilter.Frontmost).Target;
                if (target is BlockGameObjectChild child && ReferenceEquals(child.BlockGameObject, block)) return;
            }
            driver.Assert(false, $"AimAtCell: {pos} を照準できない");
        }
    }
}
