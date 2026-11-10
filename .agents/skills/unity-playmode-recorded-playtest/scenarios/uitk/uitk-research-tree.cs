// 研究ツリーを UITK（GameUi）で通す: 開いて注目点へ中央寄せ・全ノードのアンカー・HUD が遮られない・タップで詳細・ホバーのツールチップ・研究・視野の保持と選択の解除・右短押しと Tab
// Runs the research tree on UITK (GameUi): open centered on focus, every node anchor, unblocked HUDs, tap for details, hover tooltips, research, kept viewport with cleared selection, right short press and Tab
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System;
using System.Linq;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Core.Master;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// チュートリアル（challenges.json の research.node-*）が指す 7 件
// The seven nodes the tutorial (research.node-* in challenges.json) points at
var tutorialNodes = new[] { "0d76f2e5-be1c-4ad4-b460-97a8aad0495f", "3bca3b97-14d7-4cc1-a661-2266670bb6cb", "48f75a7e-36f3-4845-a0bc-f8de8b3d7baf", "50372888-cd15-47c6-9986-adf50548d156", "858bcb10-b8ba-478e-9bc5-473ca61281a2", "b47c5e3c-1b58-42c5-a477-d485d2eae747", "bc5e7786-6759-4271-8095-836703b54490" };
return PlaytestRunner.Run("uitk-research-tree", new PlaytestRunOptions { Record = true }, async p =>
{
    p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();

    // 並びの先頭の根ノード（前提なし）を選び、消費アイテムを持たせる。先頭なので注目点（1 つ目の研究可能）になる
    // Pick the first root node (no prerequisites) and hand over its cost; being first it becomes the focus (first researchable)
    var node = MasterHolder.ResearchMaster.GetAllResearches().First(r => r.PrevResearchNodeGuids.Length == 0 && r.ConsumeItems.Length > 0);
    var guid = node.ResearchNodeGuid;
    p.Note($"研究するノード {node.ResearchNodeName}（{guid}）");
    foreach (var consume in node.ConsumeItems) await GiveAll(MasterHolder.ItemMaster.GetItemMaster(consume.ItemGuid).Name, consume.ItemCount);

    // R で開き、注目点のカードがビューポートの中央にある
    // Open with R; the focus card sits at the viewport center
    await p.PressKey(Key.R);
    await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
    await p.UntilUiElement("research-tree", 5f);
    var viewport = await PlaytestUiElementOps.FindUi("research-viewport");
    p.Assert(viewport.IsFound, "ビューポート（research-viewport）が見える");
    var centerOffset = (await CardCenter()) - viewport.ScreenRect.center;
    p.Note($"注目のカードの中心とビューポートの中心の差 {centerOffset}");
    p.Assert(centerOffset.magnitude <= 2f, "注目点のカードがビューポートの中心から 2px 以内");
    await p.Screenshot("01-open-centered");

    // マスタの全ノードのアンカーが一意に引ける（チュートリアルの 7 件を含む）
    // Every master node anchor resolves uniquely (including the tutorial's seven)
    var root = GameUiRoot.Instance.GetRootElement();
    var allGuids = MasterHolder.ResearchMaster.GetAllResearches().Select(r => r.ResearchNodeGuid.ToString()).ToList();
    var unique = allGuids.Count(g => TutorialAnchorRegistry.Resolve(root, AnchorIds.ResearchNode(g)) is TutorialAnchorResolution.Unique);
    p.Note($"研究ノードのアンカー 一意 {unique}/{allGuids.Count}（チュートリアルの {tutorialNodes.Length} 件はマスタに {tutorialNodes.Count(allGuids.Contains)} 件）");
    p.Assert(unique == allGuids.Count && tutorialNodes.All(allGuids.Contains), "全研究ノードのアンカーが Unique（チュートリアルの 7 件を含む）");

    // Chrome 層の HUD が出ていて、中心の当たりが HUD 自身に来る（上の層に覆われない）
    // The Chrome HUDs show and a hit at their center lands on the HUD itself (not covered by a higher layer)
    p.Note($"研究エリア {root.Q("research-tree").worldBound}");
    AssertHudOnTop("challenge-hud");
    AssertHudOnTop("key-hints");

    // カードのタップで詳細が出て、研究ボタンが押せる
    // Tapping the card shows details with a pressable research button
    await p.ClickUi($"research-node-{guid}");
    await p.UntilUiElement("research-detail-pane", 5f);
    var button = await PlaytestUiElementOps.FindUi($"research-button-{guid}");
    p.Assert(button.IsFound && button.Clickable, "研究ボタンが押せる");
    await p.Screenshot("02-detail");

    // カードのアイコンと消費アイテムの枠の上でホバーのツールチップが出る
    // Hover tooltips show over the card icon and the cost slot
    p.Assert(await HoverShowsTooltip($"research-node-icon-{guid}"), "カードのアイコンでツールチップが出る");
    p.Assert(await HoverShowsTooltip("research-consume-slot-0"), "消費アイテムの枠でツールチップが出る");
    await p.Screenshot("02b-tooltip");

    // ボタンで研究し、カードが完了の状態・ボタンが完了済みの文言になる
    // Research via the button; the card turns completed and the button says completed
    await p.ClickUi($"research-button-{guid}");
    await p.WaitForResearch(new[] { guid }, Array.Empty<Guid>(), 10f, "UI から研究した");
    var completedText = Localize.Get(LocalizationKeys.Ui.Research.Completed);
    var card = root.Q($"research-node-{guid}");
    await p.Until(() => card.ClassListContains("research-tree__card--completed"), 5f, "カードが完了の状態");
    p.Note($"カードの状態 [{string.Join(",", (await PlaytestUiElementOps.FindUi($"research-node-{guid}")).StateFlags)}]");
    await p.Until(() => TextOf(root.Q($"research-button-{guid}")).Contains(completedText), 5f, $"ボタンの文言が「{completedText}」");
    await p.Screenshot("03-completed");

    // 空の背景をパンし、閉じて開き直しても視野は残り、選択は消える
    // Pan on empty background; closing and reopening keeps the viewport and clears the selection
    var empty = new Vector2(viewport.ScreenRect.xMax - 40f, viewport.ScreenRect.yMin + 40f);
    var beforePan = await CardCenter();
    SemanticInput.MouseMoveTo(empty);
    await p.WaitSeconds(0.5f);
    // 押下・移動・解放は 0.2 秒ずつ空ける（押下の直後に滑らせるとパンが 0px だった。実測）
    // Space press, move and release by 0.2 s (gliding right after the press panned 0 px; measured)
    SemanticInput.MouseButtonDown(0);
    await p.WaitSeconds(0.2f);
    await SemanticInput.MouseGlideTo(empty + new Vector2(-60f, 30f), 0.4f);
    await p.WaitSeconds(0.2f);
    SemanticInput.MouseButtonUp(0);
    var panned = await SettledCardCenter();
    p.Note($"パンでカードが {panned - beforePan} 動いた");
    p.Assert((panned - beforePan).magnitude > 30f, "背景のドラッグでパンした");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.PressKey(Key.R);
    await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
    await p.UntilUiElement("research-tree", 5f);
    var reopened = await CardCenter();
    p.Note($"開き直したカードの中心 {reopened}（閉じる前 {panned}）");
    p.Assert((reopened - panned).magnitude <= 1f, "開き直しても視野が残る（1px 以内）");
    p.Assert(!(await PlaytestUiElementOps.FindUi("research-detail-pane")).IsFound, "開き直すと選択が消え詳細が出ない");
    await p.Screenshot("04-reopen-kept");

    // 研究画面の中の右短押しでは閉じず、Tab でインベントリへ移る
    // A right short press inside the research screen does not close; Tab moves to the inventory
    SemanticInput.MouseMoveTo(empty);
    await p.WaitSeconds(0.5f);
    SemanticInput.MouseButtonDown(1);
    await p.WaitSeconds(0.1f);
    SemanticInput.MouseButtonUp(1);
    await p.WaitSeconds(0.5f);
    p.Assert(p.CurrentUiState == UIStateEnum.ResearchTree, "研究画面の中の右短押しでは閉じない");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    p.Assert(!(await PlaytestUiElementOps.FindUi("research-tree")).IsFound, "インベントリへ移ると研究エリアが消える");

    #region Internal

    string TextOf(VisualElement element) => element == null ? string.Empty : string.Concat(element.Query<TextElement>().ToList().Select(t => t.text));

    async UniTask<Vector2> CardCenter() => (await PlaytestUiElementOps.FindUi($"research-node-{guid}")).ScreenRect.center;

    // パンの滑走が止まるまで待ってからカードの中心を読む
    // Wait for the pan glide to stop before reading the card center
    async UniTask<Vector2> SettledCardCenter()
    {
        var last = await CardCenter();
        for (var i = 0; i < 25; i++)
        {
            await p.WaitSeconds(0.2f);
            var now = await CardCenter();
            if (now == last) return now;
            last = now;
        }
        return last;
    }

    // give コマンドは 1 スタックの上限を超える個数を受け付けないので、上限ずつ分けて渡す
    // The give command rejects counts above one stack's limit, so hand over one stack at a time
    async UniTask GiveAll(string itemName, int count)
    {
        var maxStack = Core.Item.ItemStackLevelDataStore.Instance.GetMaxStack(PlaytestItemOps.ResolveItemId(itemName));
        for (var given = 0; given < count; given += maxStack) await p.GiveItem(itemName, Math.Min(maxStack, count - given));
        await p.Until(() => p.CountItem(itemName) >= count, 10f, $"{itemName} x{count} を持った");
    }

    // HUD は部分木ごと拾わないので、判定の間だけ根を拾わせて中心の当たりを取り、すぐ戻す（Web expectHitTestWithin）
    // HUDs ignore picking subtree-wide, so let the root pick only during the check, hit its center and restore (Web expectHitTestWithin)
    void AssertHudOnTop(string hudName)
    {
        var hud = root.Q(hudName);
        p.Assert(hud != null && hud.resolvedStyle.display == DisplayStyle.Flex && hud.worldBound.width > 0f && hud.worldBound.height > 0f, $"{hudName} が出ている");
        if (hud == null) return;
        hud.pickingMode = PickingMode.Position;
        var picked = root.panel.Pick(hud.worldBound.center);
        hud.pickingMode = PickingMode.Ignore;
        p.Note($"{hudName} {hud.worldBound} 中心の当たり={picked?.name}");
        p.Assert(picked != null && (picked == hud || hud.Contains(picked)), $"{hudName} の中心の当たりが HUD 自身（上の層に覆われない）");
    }

    // 対象の中心へ動かして 1 秒待ち、Tooltip 層のホバーの宿主が出ているかを見る
    // Move to the target center, wait a second, and check the Tooltip layer's hover host is shown
    async UniTask<bool> HoverShowsTooltip(string testid)
    {
        var target = await PlaytestUiElementOps.FindUi(testid);
        SemanticInput.MouseMoveTo(target.ScreenRect.center);
        await p.WaitSeconds(1f);
        var host = GameUiRoot.Instance.GetLayer(UiLayer.Tooltip, UiFamily.Viewport).Q("hover-tooltip-host");
        var shown = host != null && host.ClassListContains("hover-tooltip-host--shown") && host.Children().Any(c => c.resolvedStyle.display == DisplayStyle.Flex && c.worldBound.width > 0f);
        p.Note($"{testid}（{target.Status}）のホバー: 宿主の表示={shown} 文字='{host?.Query<TextElement>().ToList().Select(t => t.text).FirstOrDefault()}'");
        return shown;
    }

    #endregion
});
