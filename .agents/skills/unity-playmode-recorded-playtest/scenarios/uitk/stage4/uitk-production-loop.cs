// ラダー4 uitk-production-loop: Tab→長押しクラフト→石窯に掴み・Shift・右ドラッグで入れる→開いたまま出来上がりを見る→取り出す→整理（UITKだけ）
// Ladder 4 uitk-production-loop: Tab -> hold-craft -> load a stone kiln by grab, Shift and right-drag -> watch output while open -> take out -> sort (UITK only)
// winunity play <branch> <このファイル> --ui-system GameUi で流す（全体スイッチは winunity が立てて Current を確かめる）
// Run with winunity play <branch> <this file> --ui-system GameUi (winunity sets the whole-UI switch and checks Current)
using System;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
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
    await HoldOn(craftButton, 2.5f);
    await p.Until(() => planksBefore + 2 <= PlaytestItemOps.CountItemClientSide(plank), 10f, "長押しで木の板が2枚以上増える");
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
    await p.UntilUiElement("machine-input-slots", 10f);
    await p.Screenshot("03-kiln-open");

    // Shift＋クリックで粉を入れ、原木は右クリックで半分掴んで素材順の 2 枠目へ置く（入力枠はレシピの素材順に固定）
    // Shift-click the dust in, then right-click to grab half the logs and drop them in the second slot (inputs follow recipe order)
    p.Note("Shiftで入れる・半分掴んで置く");
    SemanticInput.KeyDown(Key.LeftShift);
    await p.ClickUi(MainSlotOf(bronzeDust));
    SemanticInput.KeyUp(Key.LeftShift);
    await RightClickOn(MainSlotOf(log));
    await p.ClickUi("machine-input-slot-1");
    var kilnInventory = p.GetBlock(kilnPos).GetComponent<IOpenableBlockInventoryComponent>();
    await p.WaitForItemCount(kilnInventory, log, 1, null, 10f, "石窯の入力に原木が入る（サーバー）");
    await p.WaitForItemCount(kilnInventory, bronzeDust, 1, null, 10f, "石窯の入力に粉が入る（サーバー）");

    // 残りの原木を掴み右ドラッグで空き 3 枠へ配る
    // Grab the remaining logs and right-drag them across three empty slots
    p.Note("右ドラッグで配る");
    p.Assert(Enumerable.Range(0, Local().MainSlotCount).Any(i => Local()[i].Id == log), "半分掴みの後も手持ちに原木が残る");
    await p.ClickUi(MainSlotOf(log));
    var empties = EmptyMainSlots(3);
    await DragWithButton(1, empties);
    p.Assert(empties.All(i => Local()[i].Id == log), "右ドラッグで空き3枠に原木が配られる");
    await p.Screenshot("04-right-drag");
    await p.ClickUi(MainSlotOf(log));

    // 開いたまま出来上がりが出力枠に出るのを見る（機械 UI を開いたまま中身が更新される録画）
    // Watch the ingot appear in the output while the UI stays open
    await p.WaitForItemCount(kilnInventory, ingot, 1, null, 20f, "石窯で青銅インゴットができる（サーバー）");
    await PlaytestUiElementOps.WaitUiTextContains("machine-output-slots", "1", 10f);
    await p.Screenshot("05-output-while-open");

    // Shift で取り出し、閉じて整理する
    // Shift-take the output, close and sort
    var ingotsBefore = PlaytestItemOps.CountItemClientSide(ingot);
    SemanticInput.KeyDown(Key.LeftShift);
    await p.ClickUi("machine-output-slot-0");
    SemanticInput.KeyUp(Key.LeftShift);
    await p.Until(() => ingotsBefore < PlaytestItemOps.CountItemClientSide(ingot), 10f, "Shiftで青銅インゴットを取り出す");
    await p.ClickUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    await p.ClickUi("inventory-sort");
    await p.Until(() => Local()[0].Id != ItemMaster.EmptyItemId, 10f, "整理で先頭の枠が埋まる");
    await p.Screenshot("06-sorted");
    Application.logMessageReceived -= CountSplitDragWarnings;
    p.Note($"split-drag-warnings={splitDragWarnings}（[SplitDrag]・stale・DiscardStaleSession を含む Warning/Error）");

    #region Internal

    void CountSplitDragWarnings(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Log) return;
        if (condition.Contains("[SplitDrag]") || condition.Contains("stale") || condition.Contains("DiscardStaleSession")) splitDragWarnings++;
    }

    ILocalPlayerInventory Local() => ClientDIContext.DIContainer.DIContainerResolver.Resolve<ILocalPlayerInventory>();
    string MainSlotOf(ItemId id) => $"main-slot-{Enumerable.Range(0, Local().MainSlotCount).First(i => Local()[i].Id == id)}";
    int[] EmptyMainSlots(int count) => Enumerable.Range(0, Local().MainSlotCount).Where(i => Local()[i].Id == ItemMaster.EmptyItemId).Take(count).ToArray();

    async UniTask HoldOn(string testid, float seconds)
    {
        var center = (await PlaytestUiElementOps.FindUi(testid)).ScreenRect.center;
        SemanticInput.MouseMoveTo(center);
        SemanticInput.MouseButtonDown(0);
        await p.WaitSeconds(seconds);
        SemanticInput.MouseButtonUp(0);
    }

    async UniTask RightClickOn(string testid)
    {
        SemanticInput.MouseMoveTo((await PlaytestUiElementOps.FindUi(testid)).ScreenRect.center);
        SemanticInput.MouseButtonDown(1);
        await p.WaitSeconds(0.1f);
        SemanticInput.MouseButtonUp(1);
        await p.WaitSeconds(0.3f);
    }

    async UniTask DragWithButton(int button, int[] slots)
    {
        var first = (await PlaytestUiElementOps.FindUi($"main-slot-{slots[0]}")).ScreenRect.center;
        SemanticInput.MouseMoveTo(first);
        SemanticInput.MouseButtonDown(button);
        foreach (var slot in slots.Skip(1)) await SemanticInput.MouseGlideTo((await PlaytestUiElementOps.FindUi($"main-slot-{slot}")).ScreenRect.center, 0.3f);
        SemanticInput.MouseButtonUp(button);
        await p.WaitSeconds(0.5f);
    }

    #endregion
});
