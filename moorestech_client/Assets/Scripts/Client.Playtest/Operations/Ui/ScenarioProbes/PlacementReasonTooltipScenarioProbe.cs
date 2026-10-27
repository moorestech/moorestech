using System.Linq;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Tooltip;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

#if UNITY_EDITOR
namespace Client.Playtest.Operations.Ui.ScenarioProbes
{
    // 設置理由シナリオ固有の地形と表示観測をまとめる
    // Keep the placement reason scenario's terrain and presentation probes together
    public sealed class PlacementReasonTooltipScenarioProbe
    {
        private readonly PlaytestDriver _playtest;

        public PlacementReasonTooltipScenarioProbe(PlaytestDriver playtest)
        {
            _playtest = playtest;
        }

        public void PrepareTerrainFixtures()
        {
            // 段差と遠方地面を原本と同じ順序・座標で作る
            // Create the bump and far ground in the original order and positions
            var bump = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bump.name = "PlaytestTerrainBump";
            bump.transform.position = new Vector3(9.5f, 30.9f, 9.5f);
            bump.transform.localScale = new Vector3(3f, 4f, 3f);
            bump.AddComponent<GroundGameObject>();

            var farGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
            farGround.name = "PlaytestFarGround";
            farGround.transform.position = new Vector3(0f, 30f, 145f);
            farGround.transform.localScale = new Vector3(60f, 4f, 60f);
            farGround.AddComponent<GroundGameObject>();
        }

        public string Snapshot()
        {
            // DI登録された表示正本を毎回読む
            // Read the DI presentation authority on every observation
            var presentation = Tooltip().GetPresentation();
            if (!presentation.Visible) return "(hidden)";
            return string.Join(" / ", presentation.Lines.Select(line =>
                line.TextParams.Count == 0 ? line.Key.Key : line.Key.Key + "[" + string.Join(",", line.TextParams.Select(param => param.Name + "=" + param.Value)) + "]"));
        }

        public bool HasKey(string key)
        {
            var presentation = Tooltip().GetPresentation();
            return presentation.Visible && presentation.Lines.Any(line => line.Key.Key == key);
        }

        public bool HasTextParam(string key, string paramName, string paramValue)
        {
            // 名前付き差し込みを行キーと名前で照合する
            // Match a named placeholder by line key and name
            var presentation = Tooltip().GetPresentation();
            return presentation.Visible && presentation.Lines.Any(line => line.Key.Key == key &&
                line.TextParams.Any(param => param.Name == paramName && param.Value == paramValue));
        }

        public bool IsOnScreen(Vector3 worldPosition)
        {
            var screenPoint = Camera.main.WorldToScreenPoint(worldPosition);
            return 0 < screenPoint.z && 0 < screenPoint.x && screenPoint.x < Screen.width && 0 < screenPoint.y && screenPoint.y < Screen.height;
        }

        public async UniTask<string> AimAndSnapshot(Vector3 worldPosition, string label)
        {
            // プレビューが再計算される原本の10フレームを保つ
            // Preserve the original ten frames for preview recomputation
            await _playtest.AimAt(worldPosition);
            await UniTask.DelayFrame(10);
            var snapshot = Snapshot();
            _playtest.Note($"[{label}] tooltip = {snapshot}");
            return snapshot;
        }

        private static IMouseCursorTooltip Tooltip()
        {
            return ClientDIContext.DIContainer.DIContainerResolver.Resolve<IMouseCursorTooltip>();
        }
    }
}
#endif
