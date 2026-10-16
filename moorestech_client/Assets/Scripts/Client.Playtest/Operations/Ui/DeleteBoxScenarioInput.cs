using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Tooltip;
using Client.Playtest.Input;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Client.Playtest.Operations.Ui
{
    /// <summary>
    ///     録画シナリオで削除箱の2点入力と対象件数を検証する
    ///     Enters two delete-box points and checks the target count in recorded scenarios
    /// </summary>
    public static class DeleteBoxScenarioInput
    {
        public static async UniTask Select(PlaytestDriver driver, Vector3Int start, Vector3Int end)
        {
            await AimAtGround(start);
            SemanticInput.KeyDown(Key.LeftShift);
            await UniTask.DelayFrame(3);
            SemanticInput.MouseButtonDown(0);
            await UniTask.DelayFrame(5);
            SemanticInput.MouseButtonUp(0);
            await UniTask.DelayFrame(3);
            SemanticInput.KeyUp(Key.LeftShift);

            // 終点待ちの件数表示が届いてからクリックする
            // Click the end only after the pending box count appears
            await AimAtGround(end);
            await driver.Until(() => TargetCount() >= 1, 5f, "レールを跨ぐ箱に撤去対象がある");
            SemanticInput.MouseButtonDown(0);
            await UniTask.DelayFrame(5);
            SemanticInput.MouseButtonUp(0);

            #region Internal

            async UniTask AimAtGround(Vector3Int cell)
            {
                await driver.AimAt(new Vector3(cell.x + 0.5f, cell.y - 0.02f, cell.z + 0.5f));
            }

            int TargetCount()
            {
                var tooltip = ClientDIContext.DIContainer.DIContainerResolver.Resolve<MouseCursorTooltipState>();
                var key = LocalizationKeys.Ui.Tooltip.DeleteBoxTargetsInRange.Key;
                var line = tooltip.GetPresentation().Lines.FirstOrDefault(item => item.Key.Key == key);
                return line.TextParams != null && line.TextParams.Count > 0 && int.TryParse(line.TextParams[0], out var count) ? count : 0;
            }

            #endregion
        }
    }
}
