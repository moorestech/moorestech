// ラダー4 uitk-production-loop: Tab→長押しクラフト→石窯に掴み・Shift・右ドラッグで入れる→開いたまま出来上がりを見る→取り出す→整理（UITKだけ）
// Ladder 4 uitk-production-loop: Tab -> hold-craft -> load a stone kiln by grab, Shift and right-drag -> watch output while open -> take out -> sort (UITK only)
// winunity play <branch> <このファイル> --ui-system GameUi で流す（全体スイッチは winunity が立てて Current を確かめる）
// Run with winunity play <branch> <this file> --ui-system GameUi (winunity sets the whole-UI switch and checks Current)
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Drag;
using Client.Ui.State.Items;
using Client.WebUiHost.Boot;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.UnlockState;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-production-loop", options, async p =>
{
    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    // 裏タブの Game View は描画されず timeScale=0 のままスキットの Skip 送りが止まるので表へ出す（前例 uitk-opening-hud）
    // A Game View behind a tab never renders and skit skip stalls at timeScale=0, so bring it to front (precedent uitk-opening-hud)
    PlaytestGameViewOps.BringToFront();
    await p.Until(() => 0f < Time.timeScale, 10f, "Recorderの初回フレーム取得でtimeScaleが戻る");
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    // 重い機械では1回の Skip で開幕スキットが終わらないことがあるので、Story のままなら飛ばし直し回数を Note に残す
    // On a loaded machine one skip may not end the opening skit, so skip again while in Story and note the rounds
    var skitSkipRounds = 1;
    await p.SkipOpeningSkitIfPlaying();
    while (skitSkipRounds < 4 && !await PlaytestUiOps.PollUiState(UIStateEnum.GameScreen, 5f)) { skitSkipRounds++; await p.SkipOpeningSkitIfPlaying(); }
    p.Note($"opening-skit-skip-rounds={skitSkipRounds}");
    await p.ExitToGameScreen();
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが GameUi（UITK側）");
    p.Assert(WebUiHost.Hub == null, "Webホストは起動していない（UITK側）");
    var log = PlaytestItemOps.ResolveItemId("原木");
    var plank = PlaytestItemOps.ResolveItemId("木の板");
    var bronzeDust = PlaytestItemOps.ResolveItemId("青銅鉱石の粉");
    var ingot = PlaytestItemOps.ResolveItemId("青銅インゴット");
    await p.GiveItem("原木", 12);
    // 粉は1個だけ渡し、出力が1個で止まるようにする（2個目ができると出力枠の「1」の確認が競る）
    // Give a single dust so the output stops at one ingot (a second one would race the "1" check on the output slot)
    await p.GiveItem("青銅鉱石の粉", 1);
    // 掴みの申し送り（C4-e D-36・Warning）の警告を数え、終わりに Note へ出す
    // Count the grab hand-off warnings (C4-e D-36 and Warnings) and note them at the end
    var splitDragWarnings = 0;
    Application.logMessageReceived += CountSplitDragWarnings;
    // Tab で持ち物を開き、木の板を選んで長押しでクラフトする
    // Open the inventory with Tab, select planks and hold to craft
    p.Note("Tab→木の板→長押しクラフト");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    await PlaytestUiElementOps.ScrollDownUntilUiElementIsClickable($"item-list-cell-{plank.AsPrimitive()}", "item-list-scroll", 10f);
    await p.ClickUi($"item-list-cell-{plank.AsPrimitive()}");
    const string craftButton = "craft-button-37623c6d-e1cf-4985-abfb-8239e6e33981";
    await p.UntilUiElement(craftButton, 10f);
    var planksBefore = PlaytestItemOps.CountItemClientSide(plank);
    await PressOn(craftButton, 0);
    await p.Until(() => planksBefore + 2 <= PlaytestItemOps.CountItemClientSide(plank), 10f, "長押しで木の板が2枚以上増える");
    SemanticInput.MouseButtonUp(0);
    await p.Screenshot("01-hold-craft");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    // 青銅インゴットのレシピをサーバー直で解放する（未解放・未選択の機械は入力を受けない。研究の流れはラダー 5 が持つ）
    // Unlock the bronze ingot recipe server-side (a locked or unselected machine takes no input; ladder 5 owns the research flow)
    var bronzeRecipe = Guid.Parse("af78bd9a-39c9-440a-9b43-61dca495f8fb");
    var clientUnlock = ClientDIContext.DIContainer.DIContainerResolver.Resolve<IGameUnlockStateData>();
    await p.RunOnServer("青銅インゴットのレシピを解放する", () => ServerContext.GetService<IGameUnlockStateDataController>().UnlockMachineRecipe(bronzeRecipe));
    await p.Until(() => clientUnlock.MachineRecipeUnlockStateInfos.TryGetValue(bronzeRecipe, out var info) && info.IsUnlocked, 10f, "レシピの解放がクライアントへ届く");
    // 石窯を置いて F で開き、レシピを選ぶ
    // Place a stone kiln, open it with F and select the recipe
    var kilnPos = new Vector3Int(10, 32, 10);
    await p.SetupFlatGround();
    await p.PlaceBlockDirect("石窯", kilnPos, BlockDirection.North);
    var kilnObject = await p.WaitBlockGameObject(kilnPos);
    var kilnBounds = kilnObject.GetComponentsInChildren<Collider>(true)[0].bounds;
    p.WarpPlayer(new Vector3(kilnBounds.center.x, 33.5f, kilnBounds.min.z - 1.5f));
    await p.WaitSeconds(1f);
    await p.AimAt(kilnBounds.center);
    await p.PressInteract();
    await p.WaitUiState(UIStateEnum.SubInventory, 10f);
    await p.Screenshot("02-kiln-recipe-select");
    await p.ClickUi($"machine-recipe-{bronzeRecipe}");
    // 枠の帯は中心が子の枠に当たり押せる判定にならないので、押す先の 1 枠目で待つ
    // The band's center hits a child slot and never counts as clickable, so wait on the first slot we press
    await p.UntilUiElement("machine-input-slot-0", 10f);
    await p.Screenshot("03-kiln-open");
    // Shift＋クリックで粉を入れ、原木は右クリックで半分掴んで素材順の 2 枠目へ置く（入力枠はレシピの素材順に固定。原木が揃うと粉は即消費されるので粉を先に確かめる）
    // Shift-click the dust in, then right-click half the logs into the second slot (inputs follow recipe order; logs start processing and consume the dust, so check the dust first)
    p.Note("Shiftで入れる・半分掴んで置く");
    var kilnInventory = p.GetBlock(kilnPos).GetComponent<IOpenableBlockInventoryComponent>();
    await ShiftClickOn(MainSlotOf(bronzeDust));
    await p.WaitForItemCount(kilnInventory, bronzeDust, 1, null, 10f, "石窯の入力に粉が入る（サーバー）");
    await PressOn(MainSlotOf(log), 1);
    await p.WaitSeconds(0.1f);
    SemanticInput.MouseButtonUp(1);
    await p.ClickUi("machine-input-slot-1");
    await p.WaitForItemCount(kilnInventory, log, 1, null, 10f, "石窯の入力に原木が入る（サーバー）");
    // 残りの原木を掴み右ドラッグで空き 3 枠へ配る
    // Grab the remaining logs and right-drag them across three empty slots
    p.Note("右ドラッグで配る");
    await p.ClickUi(MainSlotOf(log));
    await p.Until(() => Ops().ReadGrab().ItemId == log, 5f, "残りの原木を掴む（クライアントの grab）");
    var grabbed = Ops().ReadGrab().Count;
    var empties = EmptyMainSlots(3);
    await RightDragAcross(empties);
    // 解放後の LateTick で確定するので、配り終えと掴みの残り（掴んだ数−3）を待って見る（前例 uitk-grab-overlay）
    // The split commits on the LateTick after release, so wait for the slots and the remaining grab (grabbed - 3) (precedent uitk-grab-overlay)
    await p.Until(() => empties.All(i => Main()[i].ItemId == log) && Ops().ReadGrab().Count == grabbed - 3, 5f, $"右ドラッグで空き3枠に1個ずつ配り、掴みは {grabbed}-3 個");
    await p.Screenshot("04-right-drag");
    if (0 < Ops().ReadGrab().Count) await p.ClickUi(MainSlotOf(log));
    await p.Until(() => Ops().ReadGrab().IsEmpty, 5f, "掴みの残りを持ち物へ戻す");
    // 開いたまま出来上がりが出力枠に出るのを見る（機械 UI を開いたまま中身が更新される録画）
    // Watch the ingot appear in the output while the UI stays open
    await p.WaitForItemCount(kilnInventory, ingot, 1, null, 20f, "石窯で青銅インゴットができる（サーバー）");
    await PlaytestUiElementOps.WaitUiTextContains("machine-output-slots", "1", 10f);
    await p.Screenshot("05-output-while-open");
    // Shift で取り出し、閉じて整理する
    // Shift-take the output, close and sort
    var ingotsBefore = PlaytestItemOps.CountItemClientSide(ingot);
    await ShiftClickOn("machine-output-slot-0");
    await p.Until(() => ingotsBefore < PlaytestItemOps.CountItemClientSide(ingot), 10f, "Shiftで青銅インゴットを取り出す");
    await p.ClickUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    await p.ClickUi("inventory-sort");
    await p.Until(() => !Main()[0].IsEmpty, 10f, "整理で先頭の枠が埋まる");
    await p.Screenshot("06-sorted");
    Application.logMessageReceived -= CountSplitDragWarnings;
    p.Note($"split-drag-warnings={splitDragWarnings}（[SplitDrag]・stale・DiscardStaleSession を含む Warning/Error）");
    #region Internal
    void CountSplitDragWarnings(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Log) return;
        if (condition.Contains("[SplitDrag]") || condition.Contains("stale") || condition.Contains("DiscardStaleSession")) splitDragWarnings++;
    }
    IReadOnlyList<SlotItem> Main() => Ops().ReadSnapshot().MainSlots;
    string MainSlotOf(ItemId id) => $"main-slot-{Enumerable.Range(0, Main().Count).First(i => Main()[i].ItemId == id)}";
    IPlayerInventoryOperations Ops() => ClientDIContext.DIContainer.DIContainerResolver.Resolve<IPlayerInventoryOperations>();
    int[] EmptyMainSlots(int count) => Enumerable.Range(0, Main().Count).Where(i => Main()[i].IsEmpty).Take(count).ToArray();
    // 移動と押下を同じフレームに積むと押下が元の位置に届くので、移動の後に間を置いて押す（前例 uitk-recipe-craft）
    // Queuing the move and press in one frame lands the press at the old position, so wait after moving (precedent uitk-recipe-craft)
    async UniTask PressOn(string testid, int button)
    {
        SemanticInput.MouseMoveTo((await PlaytestUiElementOps.FindUi(testid)).ScreenRect.center);
        await p.WaitSeconds(0.5f);
        SemanticInput.MouseButtonDown(button);
    }
    // UITK の修飾キーは InputSystem でなく PanelEventHandler.s_Modifiers から読むので Shift もそこへ入れる（前例 uitk-machine-ui）
    // UITK reads modifiers from PanelEventHandler.s_Modifiers, not the Input System, so inject Shift there too (precedent uitk-machine-ui)
    async UniTask ShiftClickOn(string testid)
    {
        var modifiers = typeof(UnityEngine.UIElements.PanelEventHandler).GetField("s_Modifiers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        SemanticInput.KeyDown(Key.LeftShift);
        modifiers.SetValue(null, EventModifiers.Shift);
        await p.ClickUi(testid);
        modifiers.SetValue(null, EventModifiers.None);
        SemanticInput.KeyUp(Key.LeftShift);
    }
    // 注入入力の右押下中は他の枠の RightEnter が出ないので、押下は実入力・進入は命令で通す（前例 uitk-grab-overlay の左の分割ドラッグ）
    // RightEnter never fires on other slots under an injected right press, so press for real and enter by command (precedent uitk-grab-overlay)
    async UniTask RightDragAcross(int[] slots)
    {
        await PressOn($"main-slot-{slots[0]}", 1);
        var drag = ClientDIContext.DIContainer.DIContainerResolver.Resolve<IPlayerSlotDragCommands>();
        var realEnters = 0;
        foreach (var slot in slots.Skip(1))
        {
            await SemanticInput.MouseGlideTo((await PlaytestUiElementOps.FindUi($"main-slot-{slot}")).ScreenRect.center, 0.3f);
            if (!Main()[slot].IsEmpty) { realEnters++; continue; }
            p.Assert(drag.EnterWithRightButton(PlayerSlotRef.Main(slot)) is UiCommandAccepted, $"右ボタンを押したままの進入（命令）が通る: main {slot}");
        }
        p.Note($"right-drag-real-enters={realEnters}/{slots.Length - 1}（注入の右押下中に実の進入で置けた枠の数）");
        SemanticInput.MouseButtonUp(1);
        await p.WaitSeconds(0.5f);
    }

    #endregion
});
