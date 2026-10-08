// BPコピー範囲の実測プローブ（不具合原因の測定専用）
// ・地面→地面 / ブロック天面→地面 / 地面→背の高いブロック天面 / ブロック側面 の4種で選択ボックスと作成BPの中身を観測
// ・貼り付け中のE押下でゴースト位置と高さオフセットが動くかを観測
// ・2回クリックで連続貼り付けできるか、ビルドメニューのBPエントリ表示を観測
// Measurement probe for the BP copy box: observes the visualizer box and created BP contents for four aim patterns,
// whether E moves the paste ghosts, whether two clicks paste twice, and how the BP entry renders in the build menu
using System.Linq;
using System.Text;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Blueprint;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("blueprint-copy-box-probe", options, async p =>
{
    // 背景EditorではInputSystemがキーボード・マウスを無効化し注入が届かないため、焦点無視へ切り替え再有効化する（beads moorestech-xsd18.4 (b)）
    // A background Editor disables keyboard/mouse so injection never lands; switch to ignore-focus and re-enable (beads moorestech-xsd18.4 (b))
    var inputSettings = InputSystem.settings;
    inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkit();
    p.Hotbar.UnlockBlueprint();

    p.PlaceBlockDirect("木のチェスト", new Vector3Int(2, 32, 2), BlockDirection.North);
    p.PlaceBlockDirect("石窯", new Vector3Int(10, 32, 2), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(2, 32, 2));
    await p.WaitBlockGameObject(new Vector3Int(10, 32, 2));

    p.WarpPlayer(new Vector3(6f, 33.5f, -5f));
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var nameState = resolver.Resolve<BlueprintNameInputState>();
    var heightOffset = resolver.Resolve<PlacementHeightOffset>();
    var datastore = p.ServerService<IBlueprintDatastore>();

    const string CopyToolCategory = "build-menu-category-d1000000-0000-4000-8000-000000000009";
    const string CopyToolEntry = "build-menu-entry-blueprintCopy-88dd687d-aceb-4aeb-94e8-44be1e1f5a0d";
    const string BlueprintCategory = "build-menu-category-d1000000-0000-4000-8000-000000000010";

    p.Note("BPコピーツールを選択");
    await SelectEntry(CopyToolCategory, CopyToolEntry);
    await p.Screenshot("00-copy-tool-selected");

    p.Note("プローブA: 地面(0.5,32,0.5)→地面(4.5,32,4.5)");
    await DragAndCreate(new Vector3(0.5f, 32f, 0.5f), new Vector3(4.5f, 32f, 4.5f), "probeA");

    p.Note("プローブB: チェスト天面(2.5,33,2.5)→地面(4.5,32,4.5)");
    await DragAndCreate(new Vector3(2.5f, 33f, 2.5f), new Vector3(4.5f, 32f, 4.5f), "probeB");

    p.Note("プローブC: 地面(8.5,32,0.5)→石窯天面(11.5,34,3.5)");
    await DragAndCreate(new Vector3(8.5f, 32f, 0.5f), new Vector3(11.5f, 34f, 3.5f), "probeC");

    p.Note("プローブD: チェスト東面(3.0,32.5,2.5)→地面(4.5,32,4.5)");
    await DragAndCreate(new Vector3(3.0f, 32.5f, 2.5f), new Vector3(4.5f, 32f, 4.5f), "probeD");

    p.Note("プローブD2: チェスト南面(2.5,32.5,2.0)→地面(4.5,32,4.5)");
    await DragAndCreate(new Vector3(2.5f, 32.5f, 2.0f), new Vector3(4.5f, 32f, 4.5f), "probeD2");

    // 貼り付け側: プローブAのBPを選択
    // Paste side: select probe A's blueprint
    var pasteBp = datastore.Blueprints.FirstOrDefault(b => b.Name == "probeA");
    p.Assert(pasteBp != null, "プローブAのBPが存在する");
    if (pasteBp != null)
    {
        p.Note("プローブE: 貼り付け中のE押下");
        await SelectEntry(BlueprintCategory, $"build-menu-entry-blueprint-{pasteBp.BlueprintGuid:D}");
        await p.AimAt(new Vector3(6.5f, 32f, 6.5f));
        await UniTask.DelayFrame(3);
        var ghostsBefore = DumpGhosts();
        var heightBefore = heightOffset.Value;
        await p.Screenshot("probeE-before-E");
        await p.PressKey(Key.E);
        await UniTask.DelayFrame(3);
        var ghostsAfter = DumpGhosts();
        p.Note($"probeE: height {heightBefore}->{heightOffset.Value} ghosts before=[{ghostsBefore}] after=[{ghostsAfter}]");
        await p.Screenshot("probeE-after-E");
        await p.PressKey(Key.Q);

        p.Note("プローブF: 2回クリックで連続貼り付け");
        await p.AimAt(new Vector3(6.5f, 32f, 6.5f));
        await p.ClickPlace();
        await p.AimAt(new Vector3(14.5f, 32f, 10.5f));
        await p.ClickPlace();
        var deadlineF = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadlineF && (p.GetBlock(new Vector3Int(6, 32, 6)) == null || p.GetBlock(new Vector3Int(14, 32, 10)) == null)) await UniTask.DelayFrame(5);
        p.Note($"probeF: state={p.CurrentUiState} block(6,32,6)={(p.GetBlock(new Vector3Int(6, 32, 6)) != null)} block(14,32,10)={(p.GetBlock(new Vector3Int(14, 32, 10)) != null)}");
        await p.Screenshot("probeF-pasted");

        p.Note("プローブG: ビルドメニューのBPエントリ表示");
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.BuildMenu, 10f);
        await p.ClickWebUi(BlueprintCategory);
        await p.HoverWebUi($"build-menu-entry-blueprint-{pasteBp.BlueprintGuid:D}");
        await p.Screenshot("probeG-build-menu-bp-entry");
        await p.CloseWebUiPanel();
    }

    await p.ExitToGameScreen();

    #region Internal

    async UniTask SelectEntry(string categoryTestid, string entryTestid)
    {
        // キー1回のタップ取りこぼしに備え、開くまでタップを繰り返す（PlaytestBuildMenuOpsと同じ）
        // Retry the open key in case a single tap is dropped (same as PlaytestBuildMenuOps)
        for (var attempt = 0; attempt < 3 && p.CurrentUiState != UIStateEnum.BuildMenu; attempt++)
        {
            await p.PressKey(p.CurrentUiState == UIStateEnum.PlaceBlock ? Key.Tab : Key.B);
            if (await PlaytestUiOps.PollUiState(UIStateEnum.BuildMenu, 4f)) break;
        }
        p.Assert(p.CurrentUiState == UIStateEnum.BuildMenu, $"ビルドメニューが開く ({entryTestid})");
        await p.UntilWebUiElement("build-menu-panel", 15f);
        await p.ClickWebUi(categoryTestid);

        // BPライブラリ更新の非同期再構築がクリックを破棄するレースに備え、遷移するまで繰り返す
        // Retry until the transition happens in case an async BP-library rebuild wipes a click
        var deadline = Time.realtimeSinceStartup + 20f;
        while (p.CurrentUiState != UIStateEnum.PlaceBlock && Time.realtimeSinceStartup < deadline)
        {
            await p.ClickWebUi(entryTestid);
            await UniTask.DelayFrame(10);
        }
        p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, $"エントリ選択でPlaceBlockへ遷移: {entryTestid}");
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.6f));
    }

    string DescribeHit()
    {
        if (!PlaceSystemUtil.TryGetRayHitPosition(Camera.main, out var hit, out var surface)) return "hit=none";
        return $"hit={hit:F4} snapped={PlaceSystemUtil.SnapHitPointToCell(hit)} surface={(surface == null ? "ground" : surface.PreviewSurfaceType.ToString())}";
    }

    async UniTask DragAndCreate(Vector3 startAim, Vector3 endAim, string label)
    {
        await p.AimAt(startAim);
        var startHit = DescribeHit();
        SemanticInput.MouseButtonDown(0);
        await UniTask.DelayFrame(3);
        await p.AimAt(endAim);
        await UniTask.DelayFrame(3);
        var endHit = DescribeHit();

        var vis = GameObject.Find("BlueprintAreaVisualizer");
        var boxText = "visualizer=none";
        if (vis != null && vis.activeSelf)
        {
            var scale = vis.transform.localScale;
            var min = Vector3Int.RoundToInt(vis.transform.position - scale * 0.5f);
            var max = min + Vector3Int.RoundToInt(scale) - Vector3Int.one;
            boxText = $"box min={min} max={max} size={scale}";
        }
        p.Note($"{label}: start[{startHit}] end[{endHit}] {boxText}");
        await p.Screenshot($"{label}-box");

        SemanticInput.MouseButtonUp(0);
        await UniTask.DelayFrame(3);

        // 名前確定は状態へ直接書く（Webモーダルの文字入力は本検証の対象外）
        // Confirm the name directly on the state (typing into the web modal is outside this probe's scope)
        nameState.Confirm(label);
        var deadline = Time.realtimeSinceStartup + 6f;
        while (Time.realtimeSinceStartup < deadline && !datastore.Blueprints.Any(b => b.Name == label)) await UniTask.DelayFrame(5);
        var bp = datastore.Blueprints.FirstOrDefault(b => b.Name == label);
        var contents = bp == null ? "BP未登録(EmptyArea等)" : string.Join(" / ", bp.Blocks.Select(b => $"{b.BlockGuidStr.Substring(0, 8)} off={b.Offset}"));
        p.Note($"{label}: created => {contents}");
        await UniTask.DelayFrame(3);
    }

    string DumpGhosts()
    {
        var root = GameObject.Find("BlueprintPastePreview");
        if (root == null) return "root=none";
        var sb = new StringBuilder();
        foreach (Transform child in root.transform)
        {
            if (!child.gameObject.activeSelf) continue;
            sb.Append(child.position.ToString("F2")).Append(';');
        }
        return sb.ToString();
    }

    #endregion
});
