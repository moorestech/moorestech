using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Playtest;
using Client.Playtest.Operations;
using Client.Playtest.WebUi;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Paths;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.UnlockState;
using MessagePack;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using TrainCarEntityObject = Client.Game.InGame.Train.View.Object.Core.TrainCarEntityObject;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("pause-menu-hierarchy", options, async p =>
{
    // Game ViewがScene Viewの裏タブだと描画されず、Recorderが初回フレーム待ちでtimeScale=0に固定しスキットのscaled Delayが返らないため表タブへ出す
    // A Game View hidden behind the Scene View tab never renders, so the Recorder pins timeScale=0 awaiting its first frame and the skit's scaled delays never return; bring it to front
    var tabFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
    var gameView = Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().First(w => w.GetType().Name == "GameView");
    var dockArea = typeof(UnityEditor.EditorWindow).GetField("m_Parent", tabFlags).GetValue(gameView);
    var panes = (System.Collections.IList)dockArea.GetType().GetField("m_Panes", tabFlags).GetValue(dockArea);
    dockArea.GetType().GetProperty("selected", tabFlags).SetValue(dockArea, panes.IndexOf(gameView));
    await p.Until(() => 0f < Time.timeScale, 10f, "Recorderの初回フレーム取得でtimeScaleが戻る");

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkit();

    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var pauseMenu = resolver.Resolve<PauseMenuStateService>();
    var captureSession = resolver.Resolve<BugReportCaptureSession>();
    var outbox = GameSystemPaths.BugReportOutboxDirectory;
    Directory.CreateDirectory(outbox);
    var existingBugBoxes = ReadyBugBoxes().ToHashSet();
    var forbiddenLogs = new[] { "ポーズメニューの画面名が不正", "invalid_page", "transition_not_allowed", "バグ報告の説明文が空", "プレイ報告の種別が不正", "Exception" };
    var forbiddenLogCount = 0;
    Application.LogCallback countForbidden = (condition, _, type) => { if ((type == LogType.Warning || type == LogType.Error || type == LogType.Exception) && forbiddenLogs.Any(condition.Contains)) forbiddenLogCount++; };
    Application.logMessageReceived += countForbidden;

    // 通常画面でトップ、設定、報告2件、閉じる操作を一続きで確認する
    // Verify top, settings, two reports, and closing as one flow from the normal game screen
    p.Note("通常画面: Escapeでポーズのトップ4ボタンを開く");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.PauseMenu, 15f);
    await AssertTop("01-top-four-buttons");

    p.Note("通常画面: 設定へ進み、Escapeでトップへ1段戻る");
    await p.ClickWebUi("pause-menu-open-settings");
    await p.Until(() => pauseMenu.CurrentPage.Value == PauseMenuPage.Settings, 15f, "設定pageへの遷移");
    await p.UntilWebUiElement("language-select", 15f);
    await p.ClickWebUi("language-select-option-japanese");
    await p.UntilWebUiElement("pause-menu-locale-japanese", 15f);
    p.Assert(p.CurrentUiState == UIStateEnum.PauseMenu, "設定中もPauseMenuが開いている");
    await p.Screenshot("02-settings");
    await p.PressKey(Key.Escape);
    await p.Until(() => pauseMenu.CurrentPage.Value == PauseMenuPage.Top, 15f, "設定からトップへ戻る");
    p.Assert(p.CurrentUiState == UIStateEnum.PauseMenu, "設定から戻ってもPauseMenuが開いている");
    await p.Screenshot("03-settings-escape-top");

    const string firstDescription = "pause hierarchy first report";
    p.Note("通常画面: バグ報告を書き、戻って再度開いても下書きが残ることを確認する");
    await OpenBugReport();
    await EnterDescription(firstDescription);
    await p.Screenshot("04-first-draft");
    await p.ClickWebUi("pause-menu-back");
    await p.Until(() => pauseMenu.CurrentPage.Value == PauseMenuPage.Top, 15f, "報告画面からトップへ戻る");
    await OpenBugReport();
    await p.Screenshot("05-first-draft-restored");
    await SubmitAndAssertBundle(firstDescription, 1, "06-first-submit-toast");

    const string secondDescription = "pause hierarchy second report";
    p.Note("通常画面: 同じポーズ内で再確保された2件目のバグ報告を送る");
    await OpenBugReport();
    await EnterDescription(secondDescription);
    await SubmitAndAssertBundle(secondDescription, 2, "07-second-submit-toast");

    p.Note("通常画面: トップのEscapeでゲームへ戻る");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 15f);
    await p.Screenshot("08-back-to-game");

    // 列車を準備して実際に乗車し、外側TrainHUDを保った入れ子ポーズを確認する
    // Prepare and ride a train, then verify nested pause while the outer TrainHUD remains active
    p.Note("列車をレールへ配置し、実プレイヤー操作で乗車する");
    var car = await PrepareAndRideTrain();
    var trainHud = resolver.Resolve<UIStateDictionary>().GetState(UIStateEnum.TrainHUDScreen) as TrainHUDScreenState;
    await p.Until(() => p.CurrentUiState == UIStateEnum.TrainHUDScreen && trainHud.IsRiding, 15f, "列車HUDで乗車済み");
    p.Assert(car != null, "乗車対象のTrainCarEntityObjectが存在する");

    p.Note("列車HUD: Escapeで入れ子ポーズのトップ4ボタンを開く");
    await p.PressKey(Key.Escape);
    await p.Until(() => trainHud.SubState == NestedPauseSubStateEnum.PauseMenuScreen, 15f, "列車HUDの入れ子ポーズ表示");
    p.Assert(p.CurrentUiState == UIStateEnum.TrainHUDScreen, "入れ子ポーズ中も外側はTrainHUDScreen");
    await AssertTop("09-train-top-four-buttons");

    p.Note("列車HUD: 設定へ進み、Escapeで入れ子ポーズのトップへ戻る");
    await p.ClickWebUi("pause-menu-open-settings");
    await p.Until(() => pauseMenu.CurrentPage.Value == PauseMenuPage.Settings, 15f, "列車HUDで設定pageへ遷移");
    await p.UntilWebUiElement("language-select", 15f);
    await p.Screenshot("10-train-settings");
    await p.PressKey(Key.Escape);
    await p.Until(() => pauseMenu.CurrentPage.Value == PauseMenuPage.Top, 15f, "列車HUDの設定からトップへ戻る");
    p.Assert(trainHud.SubState == NestedPauseSubStateEnum.PauseMenuScreen, "設定から戻っても入れ子ポーズが開いている");
    await p.Screenshot("11-train-settings-escape-top");

    p.Note("列車HUD: トップのEscapeで列車操作へ戻る");
    await p.PressKey(Key.Escape);
    await p.Until(() => trainHud.SubState == NestedPauseSubStateEnum.GameScreen, 15f, "入れ子ポーズを閉じる");
    p.Assert(p.CurrentUiState == UIStateEnum.TrainHUDScreen && trainHud.IsRiding, "列車に乗ったまま列車HUDへ戻る");
    await p.Screenshot("12-train-hud-restored");
    p.Assert(forbiddenLogCount == 0, $"禁止された警告・拒否・例外ログが0件 (count={forbiddenLogCount})");
    Application.logMessageReceived -= countForbidden;

    #region Internal

    async UniTask AssertTop(string screenshot)
    {
        await p.Until(() => pauseMenu.CurrentPage.Value == PauseMenuPage.Top, 15f, "ポーズトップpage");
        foreach (var id in new[] { "pause-menu-save", "pause-menu-save-and-quit", "pause-menu-open-settings", "pause-menu-open-bug-report" })
            await p.UntilWebUiElement(id, 15f);
        p.Assert(pauseMenu.CurrentPage.Value == PauseMenuPage.Top, "トップ4ボタンの所有pageがTop");
        await p.Screenshot(screenshot);
    }

    async UniTask OpenBugReport()
    {
        await p.ClickWebUi("pause-menu-open-bug-report");
        await p.Until(() => pauseMenu.CurrentPage.Value == PauseMenuPage.BugReport, 15f, "バグ報告pageへの遷移");
        await p.UntilWebUiElement("bug-report-description", 15f);
    }

    async UniTask EnterDescription(string description)
    {
        await p.ClickWebUi("bug-report-description");
        p.Assert(CefScreenMapper.TryGetBrowser(out var browser), "CEF browserを解決してtextareaへ入力できる");
        foreach (var character in description)
        {
            browser.SendCharEvent(character, 0);
            await UniTask.Yield();
        }
        await p.UntilWebUiElement("bug-report-send", 15f);
    }

    async UniTask SubmitAndAssertBundle(string description, int expectedNewCount, string screenshot)
    {
        await p.Until(() => captureSession.Status.Value.Kind == BugReportCaptureStatus.Ready, 30f, "バグ報告の確保が送信可能になる");
        // トーストは押した時点の確保状態の欠損で出る
        // The toast reports the gaps held by the capture state at click time
        var clickMissing = captureSession.Status.Value.Missing.ToList();
        await p.ClickWebUi("bug-report-send");
        await p.Until(() => ReadyBugBoxes().Except(existingBugBoxes).Count() == expectedNewCount, 60f, $"bug outboxが{expectedNewCount}箱増える");
        var created = ReadyBugBoxes().Except(existingBugBoxes).Select(ReadManifest).ToList();
        p.Assert(created.Count == expectedNewCount, $"READY済みbug manifestが{expectedNewCount}件");
        p.Assert(created.Any(manifest => (string)manifest["description"] == description && (string)manifest["kind"] == "bug"), $"manifestはkind=bugで説明文を保持: {description}");
        await p.Until(() => pauseMenu.CurrentPage.Value == PauseMenuPage.Top, 15f, "送信後にトップへ戻る");
        p.Assert(p.CurrentUiState == UIStateEnum.PauseMenu, "送信後もPauseMenuが開いている");
        await PlaytestWebUiOps.WaitWebUiTextContains("toast-host", clickMissing.Count == 0 ? "outbox" : clickMissing[0], 15f);
        p.Note($"送信時の欠損: {(clickMissing.Count == 0 ? "なし" : string.Join(",", clickMissing))}");
        await p.Screenshot(screenshot);
    }

    IEnumerable<string> ReadyBugBoxes() => Directory.GetDirectories(outbox).Where(directory => File.Exists(Path.Combine(directory, "READY")) && File.Exists(Path.Combine(directory, BugReportBundleLayout.ManifestFileName))).Where(directory => (string)ReadManifest(directory)["kind"] == "bug");
    JObject ReadManifest(string directory) => JObject.Parse(File.ReadAllText(Path.Combine(directory, BugReportBundleLayout.ManifestFileName)));

    async UniTask<TrainCarEntityObject> PrepareAndRideTrain()
    {
        var a = new Vector3Int(10, 32, 6); var b = new Vector3Int(10, 32, 22);
        PlacePier(a); PlacePier(b);
        await p.WaitBlockGameObject(a); await p.WaitBlockGameObject(b);
        var railA = p.GetBlock(a).GetComponent<RailComponent>(); var railB = p.GetBlock(b).GetComponent<RailComponent>();
        railA.FrontNode.ConnectNode(railB.FrontNode); railB.BackNode.ConnectNode(railA.BackNode);
        var master = MasterHolder.TrainUnitMaster.Train.TrainCars.First(trainCar => trainCar.Length <= 7);
        p.ServerService<IGameUnlockStateDataController>().UnlockTrainCar(master.TrainCarGuid);
        foreach (var required in master.RequiredItems) p.GiveItemDirect(MasterHolder.ItemMaster.GetItemMaster(required.ItemGuid).Name, required.Count);
        var position = new RailPosition(new List<IRailNode> { railA.BackNode, railB.BackNode }, TrainLengthConverter.ToRailUnits(master.Length), 0);
        var response = await ClientContext.VanillaApi.Response.PlaceTrainOnRail(position, master.TrainCarGuid, CancellationToken.None);
        p.Assert(response != null && response.Success, "乗車用の列車を本番プロトコルで配置できる");
        TrainCarEntityObject Spawned() => Object.FindObjectsByType<TrainCarEntityObject>(FindObjectsSortMode.None).FirstOrDefault();
        await p.Until(() => Spawned() != null, 15f, "乗車用列車のクライアント表示");
        var spawned = Spawned(); var collider = spawned.GetComponentInChildren<Collider>();
        p.WarpPlayer(collider.bounds.center + new Vector3(0f, 0f, -2f));
        await p.AimAt(collider.bounds.center); await p.PressRide();
        return spawned;
    }

    void PlacePier(Vector3Int position)
    {
        var blockId = PlaytestBlockOps.ResolveBlockId("レール橋脚");
        var detail = new RailBridgePierComponentStateDetail(RailComponent.ToVector3(BlockDirection.North));
        var parameters = new[] { new BlockCreateParam(RailBridgePierComponentStateDetail.StateDetailKey, MessagePackSerializer.Serialize(detail)) };
        p.Assert(ServerContext.WorldBlockDatastore.TryAddBlock(blockId, position, BlockDirection.North, parameters, out _), $"レール橋脚を配置 {position}");
    }

    #endregion
});
