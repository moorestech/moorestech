// ベルトの地形乗り越え(ADR 0081)の通し検証。Groundレイヤーの箱で丘と崖を作り、UI経路のドラッグで
// (1) 丘の上のセルが持ち上がること (2) 崖で解けないとき地形に埋めて置き、案内行がツールチップに出ることを確かめる
// End-to-end check of belt terrain climbing (ADR 0081): Ground-layer boxes form a hill and a cliff, and UI drags verify
// (1) hill cells are lifted and (2) on an unsolvable cliff the belt is placed buried with the notice line in the tooltip
using Client.Common;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Context;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Server.Protocol.PacketResponse;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.InputSystem;
using Client.Game.InGame.UI.Tooltip;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using System.Linq;
using UnityEngine;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("belt-terrain-climb-via-ui", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { SpawnPosition = new Vector3(5f, 33.5f, -4f) });
    await p.SkipOpeningSkit();

    // 丘: x=4..5・z=2 の2セルを覆い上面y=33.4（地形最高点を含むセルは33）。足場上面は32
    // Hill: covers cells x=4..5, z=2 with top y=33.4 (cell containing the max is 33); the scaffold top is 32
    CreateGroundBox("PlaytestBeltHill", new Vector3(5f, 31.7f, 2.5f), new Vector3(2f, 3.4f, 1f));
    // 崖: x=12・z=6 の1セルで上面y=35.4（下限35）。端点32の3セル列では端点へ戻れない
    // Cliff: one cell x=12, z=6 with top y=35.4 (floor 35); a 3-cell run with endpoints at 32 cannot come back down
    CreateGroundBox("PlaytestBeltCliff", new Vector3(12.5f, 32.7f, 6.5f), new Vector3(1f, 5.4f, 1f));
    await UniTask.DelayFrame(5);

    // ---------------------------------------------------------------- 1. 丘を乗り越える
    p.Note("フェーズ1: 丘を横切る東向きドラッグ → 丘の上のセルが y=33 に持ち上がる");
    await p.DragPlaceViaUi("ベルトコンベア", new Vector3Int(2, 32, 2), new Vector3Int(8, 32, 2));
    await p.Until(() => p.GetBlock(new Vector3Int(4, 33, 2)) != null && p.GetBlock(new Vector3Int(5, 33, 2)) != null, 10f, "丘の上のセルが y=33 に置かれる");
    p.Assert(p.GetBlock(new Vector3Int(8, 32, 2)) != null, "終端は地面の高さ y=32 のまま");
    await p.Screenshot("01-belt-over-hill");
    await p.ExitToGameScreen();

    // ---------------------------------------------------------------- 2. 崖で諦めて埋めて置く
    p.Note("フェーズ2: 崖を横切る3セルドラッグ → 埋めて置く案内が出て、y=32 に設置される");
    p.WarpPlayer(new Vector3(12.5f, 33.5f, -3f));
    await p.WaitSeconds(0.5f);
    await p.OpenBuildMenuAndSelectBlock("ベルトコンベア");

    await p.AimAt(PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(11, 32, 6), BlockDirection.East));
    await UniTask.DelayFrame(10);
    SemanticInput.MouseButtonDown(0);
    await UniTask.DelayFrame(5);
    await p.AimAt(PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(13, 32, 6), BlockDirection.East));
    await UniTask.DelayFrame(10);
    p.Note($"[崖ドラッグ中] tooltip = {Snapshot()}");
    p.Assert(HasKey("ui.tooltip.placeBeltTerrainClimbAbandoned"), "崖を越えられない案内がツールチップに出る");
    var cliffPreview = PreviewCells();
    p.Assert(cliffPreview.Count == 3 && cliffPreview.All(cell => cell.Placeable && cell.Position.y == 32), "崖フォールバックは3セルとも y=32・Placeable（赤にしない）");
    await p.Screenshot("02-cliff-notice");
    SemanticInput.MouseButtonUp(0);

    await p.Until(() => p.GetBlock(new Vector3Int(12, 32, 6)) != null, 10f, "崖のセルは地形に埋めて y=32 に置かれる");
    await p.Screenshot("03-belt-buried-in-cliff");
    await p.ExitToGameScreen();

    // Qの実入力で地表32から31へ下げ、中間セルだけ地表へ戻る不具合を検出する
    // Lower ground level 32 to 31 through real Q input and catch intermediate cells snapping back to ground
    p.Note("フェーズ3: 平地でQを1回押してドラッグ → 全7セルが y=31 の平らな設置可能列になる");
    p.WarpPlayer(new Vector3(-7f, 33.5f, -4f));
    await p.WaitSeconds(0.5f);
    await p.OpenBuildMenuAndSelectBlock("ベルトコンベア");
    await p.AimAt(PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(-10, 32, 3), BlockDirection.East));
    await p.PressKey(Key.Q);
    var heightOffset = ClientDIContext.DIContainer.DIContainerResolver.Resolve<PlacementHeightOffset>();
    p.Assert(heightOffset.Value == -1, "Qキー入力で現在の高さ補正が-1になる");
    SemanticInput.MouseButtonDown(0);
    await p.WaitSeconds(0.5f);
    await p.AimAt(PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(-4, 32, 3), BlockDirection.East));
    await UniTask.DelayFrame(5);
    var loweredPreview = PreviewCells();
    p.Note("[Qドラッグ中] " + string.Join(" / ", loweredPreview.Select(cell => $"{cell.Position}:placeable={cell.Placeable}")));
    p.Assert(loweredPreview.Count == 7 && loweredPreview.All(cell => cell.Position.y == 31 && cell.Placeable), "Qで下げた平地列は全7セルが y=31・Placeable");
    p.Assert(loweredPreview.Select(cell => cell.Position).OrderBy(cell => cell.x).SequenceEqual(Enumerable.Range(-10, 7).Select(x => new Vector3Int(x, 31, 3))), "Qで下げた列のXZは指定した平地の7セルと一致する");
    p.Assert(!HasKey("ui.tooltip.placeBeltTerrainClimbAbandoned"), "Qで下げた平地列には地形断念案内が出ない");
    await p.Screenshot("04-q-lowered-flat-preview");
    SemanticInput.MouseButtonUp(0);
    await p.Until(() => Enumerable.Range(-10, 7).All(x => p.GetBlock(new Vector3Int(x, 31, 3)) != null), 10f, "Qで下げた全7セルが y=31 に送信・設置される");
    p.Assert(Enumerable.Range(-10, 7).All(x => p.GetBlock(new Vector3Int(x, 32, 3)) == null), "Qで下げた列に y=32 へ戻るセルがない");
    await p.Screenshot("05-q-lowered-flat-placed");
    await p.ExitToGameScreen();

    #region Internal

    // 本番の最終Placeable列を読取専用で観測し、検証用の公開APIを増やさない
    // Observe the production final Placeable list read-only without adding a public testing API
    List<PlaceInfo> PreviewCells()
    {
        var system = ClientDIContext.DIContainer.DIContainerResolver.Resolve<BeltConveyorPlaceSystem>();
        var field = typeof(BeltConveyorPlaceSystem).GetField("_currentPlaceInfos", BindingFlags.Instance | BindingFlags.NonPublic);
        return ((List<PlaceInfo>)field.GetValue(system)).ToList();
    }

    void CreateGroundBox(string name, Vector3 center, Vector3 size)
    {
        // 地表探査はGroundレイヤーを見るため、足場と同じレイヤーとマーカーを付ける
        // Ground probing looks at the Ground layer, so give it the same layer and marker as the scaffold
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.position = center;
        box.transform.localScale = size;
        box.AddComponent<GroundGameObject>();
        box.layer = LayerConst.GroundLayer;
    }

    IMouseCursorTooltip Tooltip()
    {
        return ClientDIContext.DIContainer.DIContainerResolver.Resolve<IMouseCursorTooltip>();
    }

    string Snapshot()
    {
        var presentation = Tooltip().GetPresentation();
        if (!presentation.Visible) return "(hidden)";
        return string.Join(" / ", presentation.Lines.Select(line => line.Key.Key));
    }

    bool HasKey(string key)
    {
        var presentation = Tooltip().GetPresentation();
        return presentation.Visible && presentation.Lines.Any(line => line.Key.Key == key);
    }

    #endregion
});
