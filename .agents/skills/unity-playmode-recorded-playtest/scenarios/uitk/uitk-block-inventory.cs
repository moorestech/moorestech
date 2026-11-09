// ブロックインベントリ（S4-alpha）を UITK（GameUi）で開き、左掴み・右分割・ダブル集め・Shift 移動が実在庫に効くことを通す
// Opens the block inventory (S4-alpha) on UITK (GameUi) and checks left pick, right split, double-click collect and Shift move against real stock
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Ui.Foundation.Root;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.SlotCommands.Block;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

var chestPosition = new Vector3Int(0, 32, 0);
return PlaytestRunner.Run("uitk-block-inventory", new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 240f }, async p =>
{
    // 実行中の Error ログを数えて最後に 0 件を主張する
    // Count error logs during the run and assert zero at the end
    var errorLogs = new List<string>();
    void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errorLogs.Add(condition);
    }
    Application.logMessageReceived += OnLog;
    try
    {
        p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
        p.Note($"UiInputExclusivity.ActiveUiSystem={UiInputExclusivity.ActiveUiSystem}");

        // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
        // Ignore focus so injected input reaches a background Editor
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        SemanticInput.EnsureDevices();
        InputSystem.EnableDevice(Keyboard.current);
        InputSystem.EnableDevice(Mouse.current);

        // 1. 木のチェストを置き、サーバーで枠 0 に原木 10 を入れる
        // 1. Place a wooden chest and put 10 wood into slot 0 on the server
        await p.SetupFlatGround();
        await p.SkipOpeningSkitIfPlaying();
        await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen");
        var chest = await p.PlaceBlockDirect("木のチェスト", chestPosition, BlockDirection.North);
        await p.WaitBlockGameObject(chestPosition);
        var wood = PlaytestItemOps.ResolveItemId("原木");
        var chestInventory = chest.GetComponent<IOpenableBlockInventoryComponent>();
        await p.RunOnServer("原木 10 をチェストへ入れる", () => chestInventory.SetItem(0, wood, 10));

        // 2. 向いて操作キーで開く → block-inventory と題
        // 2. Aim and interact to open -> block-inventory and its title
        p.WarpPlayer(new Vector3(chestPosition.x + 0.5f, 32.5f, chestPosition.z - 1.5f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "着地");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(chestPosition.x + 0.5f, 32.5f, chestPosition.z));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.UntilUiElement("chest-grid-slot-0", 10f);
        var root = GameUiRoot.Instance.GetRootElement();
        var shell = root.Q<VisualElement>("block-inventory");
        var title = shell.Q<Label>(className: "game-panel__title")?.text;
        p.Note($"題='{title}'");
        p.Assert(shell.resolvedStyle.display == DisplayStyle.Flex, "block-inventory が出る");
        p.Assert(title == "木のチェスト", "題がチェストの名前（木のチェスト）");
        var resolver = ClientDIContext.DIContainer.DIContainerResolver;
        var blockOperationsName = resolver.Resolve<IBlockInventoryOperations>().GetType().Name;
        p.Note($"IBlockInventoryOperations={blockOperationsName}");
        p.Assert(blockOperationsName == "LocalBlockInventoryOperations", "本番経路の口は LocalBlockInventoryOperations");
        await p.Until(() => Slot(0) == 10, 5f, "枠 0 に原木 10（クライアント）");
        await p.Screenshot("01-chest-open");

        // 3. 左押下で全量を掴み、掴み絵が出る → 同じ枠へ左押下で戻す
        // 3. Left press picks all and shows the grab picture -> left press puts it back
        p.Note("左押下で全量を掴む");
        await p.ClickUi("chest-grid-slot-0");
        await Expect(10, 0, -1, "左押下: 掴み 10・枠 0 が空");
        var overlay = root.Q<VisualElement>("grab-overlay");
        p.Assert(overlay != null && overlay.ClassListContains("grab-overlay--shown") && overlay.resolvedStyle.display == DisplayStyle.Flex, "grab-overlay が grab-overlay--shown を持ち display Flex");
        await p.Screenshot("02-grabbed");
        await p.ClickUi("chest-grid-slot-0");
        await Expect(0, 10, -1, "左押下で戻す: 掴み 0・枠 0 が 10");

        // 4. 右押下で半分 → 右押下で 1 個置く → 左押下で残りを置く
        // 4. Right press takes half -> right press drops one -> left press drops the rest
        p.Note("右押下で分割・1 個置き・残りを置く");
        await RightClick("chest-grid-slot-0");
        await Expect(5, 5, -1, "右押下: 掴み 5・枠 0 が 5");
        await RightClick("chest-grid-slot-0");
        await Expect(4, 6, -1, "右押下で 1 個置く: 掴み 4・枠 0 が 6");
        await p.ClickUi("chest-grid-slot-0");
        await Expect(0, 10, -1, "左押下で残りを置く: 掴み 0・枠 0 が 10");

        // 5. 半分を枠 1 へ置き、枠 0 のダブルクリックで集める
        // 5. Put half into slot 1, then double-click slot 0 to collect
        p.Note("右押下で半分を枠 1 へ置き、枠 0 をダブルクリックで集める");
        await RightClick("chest-grid-slot-0");
        await p.ClickUi("chest-grid-slot-1");
        await Expect(0, 5, 5, "枠 0 が 5・枠 1 が 5・掴み 0");
        await p.Screenshot("03-split-two-slots");
        await p.HoverUi("chest-grid-slot-0");
        await SemanticInput.Click();
        await SemanticInput.Click();
        await Expect(0, 10, 0, "ダブルクリック: 掴み 0・枠 0 が 10・枠 1 が空");

        // 6. Shift＋左押下で main へ配る
        // 6. Shift + left press distributes into main
        var mainBefore = MainWood();
        p.Note($"Shift＋左押下で main へ（前の main の原木={mainBefore}）");
        // UITK の shiftKey は PanelEventHandler が IMGUI の Event 列から読み InputSystem の注入では立たないので、押下の間だけ同じ静的値へ Shift を置く
        // UITK's shiftKey comes from PanelEventHandler's IMGUI event queue, not injected InputSystem keys, so hold Shift in that static only during the press
        var uitkModifiers = typeof(PanelEventHandler).GetField("s_Modifiers", BindingFlags.NonPublic | BindingFlags.Static);
        if (uitkModifiers == null) throw new System.InvalidOperationException("PanelEventHandler.s_Modifiers が見つからない（Unity の版で内部名が変わった）ので Shift を注入できない");
        SemanticInput.KeyDown(Key.LeftShift);
        uitkModifiers.SetValue(null, EventModifiers.Shift);
        await p.ClickUi("chest-grid-slot-0");
        uitkModifiers.SetValue(null, EventModifiers.None);
        SemanticInput.KeyUp(Key.LeftShift);
        await Expect(0, 0, 0, "Shift: チェストの枠が空");
        p.Assert(MainWood() == mainBefore + 10, $"Shift: main の原木が 10 増える（{mainBefore} → {MainWood()}）");
        await ExpectServer(new[] { 0, 0 }, "サーバーのチェストも枠 0・枠 1 が空");
        await p.Screenshot("04-shift-moved");

        // 7. 閉じるで GameScreen へ戻り隠れる
        // 7. Close returns to GameScreen and hides the panel
        await p.ClickUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
        await UniTask.DelayFrame(2);
        p.Assert(shell.resolvedStyle.display == DisplayStyle.None, "閉じると block-inventory が隠れる");
    }
    finally
    {
        Application.logMessageReceived -= OnLog;
    }

    // 8. 途中の Error ログが 0 件
    // 8. No error logs during the run
    p.Note($"Error ログ {errorLogs.Count} 件: {string.Join(" | ", errorLogs.Take(5))}");
    p.Assert(errorLogs.Count == 0, "実行中の Error ログが 0 件");

    #region Internal

    int Slot(int index) => ClientDIContext.DIContainer.DIContainerResolver.Resolve<IBlockInventoryOperations>().TryReadBlockSlots(out var slots) && index < slots.Count ? slots[index].Count : -1;

    int Grab() => ClientDIContext.DIContainer.DIContainerResolver.Resolve<IPlayerInventoryOperations>().ReadGrab().Count;

    int MainWood() => ClientDIContext.DIContainer.DIContainerResolver.Resolve<IPlayerInventoryOperations>().ReadSnapshot().MainSlots.Where(s => s.ItemId == PlaytestItemOps.ResolveItemId("原木")).Sum(s => s.Count);

    // 掴み・枠 0・枠 1（-1 は見ない）がクライアントで揃い、サーバーの実在庫も同じになるまで待つ
    // Wait until grab, slot 0 and slot 1 (-1 = ignored) match on the client and the server stock agrees
    async UniTask Expect(int grab, int slot0, int slot1, string label)
    {
        await p.Until(() => Grab() == grab && Slot(0) == slot0 && (slot1 < 0 || Slot(1) == slot1), 5f, label);
        p.Note($"{label}: 掴み={Grab()} 枠0={Slot(0)} 枠1={Slot(1)}");
        await ExpectServer(slot1 < 0 ? new[] { slot0 } : new[] { slot0, slot1 }, $"{label}（サーバー）");
    }

    async UniTask ExpectServer(int[] counts, string label)
    {
        var inventory = p.GetBlock(chestPosition).GetComponent<IOpenableBlockInventoryComponent>();
        var actual = new int[counts.Length];
        await p.RunOnServer($"サーバーの枠を読む: {label}", () =>
        {
            for (var i = 0; i < counts.Length; i++) actual[i] = inventory.GetItem(i).Count;
        });
        p.Assert(actual.SequenceEqual(counts), $"{label}: [{string.Join(",", actual)}]");
    }

    // 枠の中心へ動かし、右ボタンの押下→解放を別フレームで注入する
    // Move to the slot center and inject a right press then release on separate frames
    async UniTask RightClick(string testid)
    {
        await p.HoverUi(testid);
        p.Note($"右クリック: {testid}");
        SemanticInput.MouseButtonDown(1);
        await UniTask.DelayFrame(2);
        SemanticInput.MouseButtonUp(1);
        await UniTask.DelayFrame(2);
        await p.WaitSeconds(0.5f);
    }

    #endregion
});
