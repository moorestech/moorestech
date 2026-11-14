// ラダー 2 uitk-opening-hud: Web ホスト無しで、開幕スキットを送って飛ばす → 目標 HUD の枠 → 歩いて伐採（ゲージと獲得通知）→ 数字キーで選んで設置を UITK だけで通す
// Ladder 2 uitk-opening-hud: with no web host, advance and skip the opening skit -> outline on the objective HUD -> walk and chop (gauge and earned notification) -> pick with a digit key and place, using UITK alone
// 100_start_game に選択肢は無い。選択肢は uitk-skit-choice-pause-cursor.cs（sample_short）が受け持つ（D-2）
// 100_start_game has no choices; uitk-skit-choice-pause-cursor.cs (sample_short) covers them (D-2)
// 枠は先頭チャレンジの tutorial で、伐採で原木 3 個に届くと消えるので採掘の前に見る（D-3）
// The outline is the first challenge's tutorial and vanishes once chopping reaches 3 logs, so check it before mining (D-3)
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Interact.Selection;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Skit.UI;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Notification;
using Client.Ui.State.Notification.Content;
using Client.WebUiHost.Boot;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.PlayerInventory.Interface;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 300f };
return PlaytestRunner.Run("uitk-opening-hud", options, async p =>
{
    // 系の主張と入力の準備（前例 uitk-skit）
    // Assert the UI system and prepare input (precedent uitk-skit)
    p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    // 裏タブの Game View は描画されず timeScale=0 のままスキットが止まるので表へ出す（vq8w・iefh）
    // A Game View behind a tab never renders and the skit stalls at timeScale=0, so bring it to front (vq8w, iefh)
    Client.Playtest.Operations.Ui.PlaytestGameViewOps.BringToFront();
    await p.Until(() => 0f < Time.timeScale, 10f, "Recorderの初回フレーム取得でtimeScaleが戻る");
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    p.Assert(WebUiHost.Hub == null, "Webホストは起動していない（hostless）");
    var store = SkitPresentationStateStore.Instance;
    var root = GameUiRoot.Instance.GetRootElement();

    // 1. 開幕スキット: 自動送りを切り、Enter と窓のクリックで 1 文ずつ送ってから UITK のスキップで抜ける
    // 1. Opening skit: turn auto off, advance one line each by Enter and a window click, then leave via the UITK skip
    await p.Until(() => store.GetCurrent()?.PresentationState?.Mode == SkitPresentationData.BlockingMode, 60f, "開幕 blocking スキットが始まった");
    await p.Until(() => store.GetCurrent().PresentationState.TextAreaVisible && store.GetCurrent().AllowedIntents.Length > 0, 30f, "会話の文が出て送り待ち");
    await p.UntilUiElement("blocking-skit", 10f);
    if (store.GetCurrent().PresentationState.AutoEnabled)
    {
        await p.ClickUi("skit-tool-auto");
        await p.Until(() => !store.GetCurrent().PresentationState.AutoEnabled, 5f, "UITK の自動ボタンで自動送りが切れる");
    }
    await p.Screenshot("01-opening-skit");
    await AdvanceSkit(true);
    await AdvanceSkit(false);
    await p.ClickUi("skit-tool-skip");
    await p.WaitUiState(UIStateEnum.GameScreen, 60f);

    // 2. 先頭チャレンジの uiHighLight: 目標 HUD（challenge.current-hud）に枠とラベルが重なる
    // 2. First challenge's uiHighLight: an outline and label sit on the objective HUD (challenge.current-hud)
    await p.Until(() => IsShown(root.Q("challenge-hud")), 15f, "目標 HUD（challenge-hud）が出た");
    await p.Until(() => OutlineCueCount() == 1, 15f, "store に challenge.current-hud の枠が 1 つある");
    await p.Until(() => RingGapToHud() < 3f, 10f, "目標 HUD の中心に重なる赤枠が出た");
    p.Assert(root.Query(className: "tutorial-highlight-label").ToList().Any(IsShown), "枠のラベルが出ている");
    p.Note($"枠と HUD の中心のずれ {RingGapToHud():F1}px");
    await p.Screenshot("02-challenge-hud-outline");

    // 3. 初期装備の石の斧で先頭チャレンジのピン先の木へ歩いて寄り、F を押し続けてゲージと獲得通知を出す（前例 uitk-hud）
    // 3. Walk to a tree the first challenge pins with the starting stone axe, hold F and show the gauge and earned notice (precedent uitk-hud)
    var equipment = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(ClientContext.PlayerConnectionSetting.PlayerId).EquipmentInventory;
    var initialToolId = MasterHolder.ItemMaster.GetItemId(MasterHolder.ItemMaster.Items.InitialEquipmentItems[0].ItemGuid);
    p.Assert(equipment.GetSelectedItem().Id == initialToolId, $"開始時に初期装備を選んでいる（期待 {initialToolId} 実測 {equipment.GetSelectedItem().Id}）");
    var category = MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements.Single(c => c.InitialUnlocked);
    var first = MasterHolder.ChallengeMaster.GetCategoryInitialChallenges(category.CategoryGuid).Single();
    var pinTutorial = first.Tutorials.Single(t => t.TutorialType == TutorialsElement.TutorialTypeConst.mapObjectPin);
    var treeGuids = MasterHolder.ChallengeMaster.ResolvePinTargets((MapObjectPinTutorialParam)pinTutorial.TutorialParam);
    var tree = UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SearchNearestMapObject(treeGuids, p.PlayerPosition);
    p.Assert(tree != null, $"ピン先（{treeGuids.Count} 種）の最寄りの木が見つかった");
    // 地図物は少しずつ生成され選ばれる木は進み具合で変わるので、背の高い木でも画面に入るよう根元 +1.2m の幹を狙う
    // Map objects instantiate progressively so the picked tree varies; aim at the trunk 1.2m above the root so tall trees stay on screen
    var treeBounds = tree.GetComponentInChildren<Collider>(true).bounds;
    var treeAimPoint = new Vector3(treeBounds.center.x, Mathf.Clamp(tree.transform.position.y + 1.2f, treeBounds.min.y, treeBounds.max.y), treeBounds.center.z);
    await p.Until(() => Camera.main != null, 10f, "MainCameraの起動");
    var forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
    if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
    // 木の 5m 手前へ飛び、W を押して到達距離（2m）の内まで歩く
    // Warp 5m short of the tree and hold W until inside reach (2m)
    p.WarpPlayer(tree.transform.position - forward * 5f + Vector3.up * 0.5f);
    await p.WaitSeconds(0.5f);
    await p.AimAt(treeAimPoint);
    SemanticInput.KeyDown(Key.W);
    await p.Until(() => Vector3.Distance(Flat(p.PlayerPosition), Flat(tree.transform.position)) < 1.6f, 10f, "歩いて木に寄った");
    SemanticInput.KeyUp(Key.W);
    await p.AimAt(treeAimPoint);
    var selector = new InteractTargetSelector();
    await p.Until(() => ReferenceEquals(selector.Scan().Primary, tree), 10f, "本番の選定器が木を選んだ");
    SemanticInput.KeyDown(Key.F);
    await UntilUiFound("progress-bar", 10f, "採掘ゲージ progress-bar が出た");
    await p.Screenshot("03-mining-gauge");
    await p.Until(() => EarnedRows().Any(), 20f, "獲得通知の行が出た");
    // 行は入りの tween で透明から出るので、不透明になってから撮る
    // Rows fade in through an enter tween, so capture once one is opaque
    await p.Until(() => EarnedRows().Any(row => 0.99f <= row.resolvedStyle.opacity), 5f, "獲得通知の行が入り終わった");
    SemanticInput.KeyUp(Key.F);
    p.Note($"獲得通知: {string.Join(" / ", EarnedRows().Select(row => row.Q<Label>()?.text))}");
    await p.Screenshot("04-earned-notification");

    // 4. 足場へ戻り、枠 0 に木のチェストを割り当て、数字キー 1 で選んで設置する（前例 uitk-build-bp）
    // 4. Back on the scaffold, assign the wooden chest to slot 0, pick it with digit 1 and place it (precedent uitk-build-bp)
    p.WarpPlayer(new Vector3(6f, 33.5f, -5f));
    await p.Hotbar.AssignHotbar(0, "木のチェスト");
    await p.PressKey(Key.Digit1);
    await p.WaitUiState(UIStateEnum.PlaceBlock, 10f);
    var chestOrigin = new Vector3Int(2, 32, 2);
    await p.AimAtPlaceOrigin("木のチェスト", chestOrigin);
    await p.ClickPlace();
    await p.WaitBlockGameObject(chestOrigin);
    await p.Screenshot("05-placed");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    #region Internal

    // Enter（true）か窓のクリック（false）で 1 文送る。打ち出し中は 1 回目が全文表示なので最大 2 回
    // Advance one line by Enter (true) or a window click (false); the first input may only reveal the text, so up to twice
    async UniTask AdvanceSkit(bool byEnter)
    {
        await p.Until(() => store.GetCurrent().AllowedIntents.Length > 0 && store.GetCurrent().PresentationState.TextAreaVisible, 30f, "送り待ち");
        var before = store.GetCurrent().SceneRevision;
        for (var i = 0; i < 2 && store.GetCurrent().SceneRevision == before; i++)
        {
            if (byEnter) await p.PressKey(Key.Enter);
            else await p.ClickUi("blocking-skit");
            await p.WaitSeconds(0.5f);
        }
        p.Assert(store.GetCurrent().SceneRevision > before, byEnter ? "Enter で送られた" : "窓のクリックで送られた");
    }

    // FindUi を await して見つかるまで回す（同期で結果を取り出さない）
    // Await FindUi until found (never block on the result synchronously)
    async UniTask UntilUiFound(string testid, float timeoutSeconds, string label)
    {
        var deadline = Time.realtimeSinceStartup + timeoutSeconds;
        var found = (await PlaytestUiElementOps.FindUi(testid)).IsFound;
        while (!found && Time.realtimeSinceStartup < deadline)
        {
            await UniTask.DelayFrame(1);
            found = (await PlaytestUiElementOps.FindUi(testid)).IsFound;
        }
        p.Assert(found, label);
    }

    int OutlineCueCount()
    {
        return TutorialPresentationStateStore.Instance.GetCurrent().Sessions.SelectMany(session => session.Elements).OfType<TutorialOutlineElementData>().Count(outline => outline.AnchorId == AnchorIds.ChallengeCurrentHud);
    }

    // 見えている赤枠のうち目標 HUD に一番近いものの中心のずれ（枠はアンカーを padding で広げた箱なので中心は一致する）
    // Center gap of the visible ring nearest the objective HUD (the ring is the anchor box grown by padding, so centers match)
    float RingGapToHud()
    {
        var hudCenter = root.Q("challenge-hud").worldBound.center;
        var rings = root.Query(className: "tutorial-outline__ring").ToList().Where(IsShown).ToList();
        return rings.Count == 0 ? float.MaxValue : rings.Min(ring => Vector2.Distance(ring.worldBound.center, hudCenter));
    }

    // 通知は背面の層（BehindStage）にあり、行は同名で複数並ぶので FindUi でなく層から引く
    // Notifications live in the BehindStage layer and rows share one name, so read them from the layer instead of FindUi
    IEnumerable<NotificationRowView> EarnedRows()
    {
        return GameUiRoot.Instance.GetLayer(UiLayer.BehindStage, UiFamily.Viewport).Query<NotificationRowView>().ToList().Where(row => row.Row?.Content is EarnedNotificationContent && IsShown(row));
    }

    bool IsShown(VisualElement element)
    {
        for (var current = element; current != null; current = current.parent)
        {
            if (current.resolvedStyle.display == DisplayStyle.None) return false;
        }
        return element != null;
    }

    Vector3 Flat(Vector3 position) => new(position.x, 0f, position.z);
    #endregion
});
