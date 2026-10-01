// メニューを開いたまま歩け、ポーズ中だけ止まり、機械UIは手の届く範囲を出ると閉じることを通しで確かめる
// End-to-end check: walking works with menus open, only pause stops it, and the machine UI closes once out of reach
using Client.Game.InGame.Interact.Selection;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.InputSystem;

var ovenBlockName = "石窯";
var flatGroundObjectName = "PlaytestFlatGround";
var testFieldTopY = 200f;
var ovenModelCenterOffset = new Vector3(1.5f, 1f, 1.5f);

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("walk-while-menus-open", options, async p =>
{
    await p.SkipOpeningSkit();
    await p.SetupFlatGround();
    GameObject.Find(flatGroundObjectName).transform.position = new Vector3(0f, testFieldTopY - 2f, 0f);
    p.WarpPlayer(new Vector3(0f, testFieldTopY + 1f, 0f));
    await p.WaitSeconds(1.5f);

    // Wを1.5秒押した移動量を返す
    // Returns how far the player moved while W was held for 1.5 seconds
    async UniTask<float> WalkForward()
    {
        var before = p.PlayerPosition;
        SemanticInput.KeyDown(Key.W);
        await p.WaitSeconds(1.5f);
        SemanticInput.KeyUp(Key.W);
        await p.WaitSeconds(0.5f);
        var delta = p.PlayerPosition - before;
        return new Vector2(delta.x, delta.z).magnitude;
    }

    // 開いて歩いて閉じる。閉じるのはEscape（各画面のCloseUI）
    // Open, walk, and close; closing uses Escape (each screen's CloseUI)
    async UniTask CheckWalkWhileOpen(Key openKey, UIStateEnum state)
    {
        p.Note($"{state} を開いたままWで歩く");
        await p.PressKey(openKey);
        await p.WaitUiState(state, 5f);
        var walked = await WalkForward();
        p.Assert(walked > 1f, $"{state} を開いたまま歩けた ({walked:F2}m)");
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 5f);
    }

    // 開くキーは GameScreenState.GetNextUpdate の割当（B/T/R）とインベントリの OpenInventory
    // Open keys follow GameScreenState.GetNextUpdate (B/T/R) and the inventory's OpenInventory binding
    await CheckWalkWhileOpen(Key.Tab, UIStateEnum.PlayerInventory);
    await CheckWalkWhileOpen(Key.B, UIStateEnum.BuildMenu);
    await CheckWalkWhileOpen(Key.T, UIStateEnum.ChallengeList);
    await CheckWalkWhileOpen(Key.R, UIStateEnum.ResearchTree);

    // ビルドメニューの検索欄に文字入力フォーカスがある間は、wasd・Spaceを押しても動かない
    // While the build menu search box owns text focus, pressing wasd or Space never moves the player
    p.Note("ビルドメニューの検索欄にフォーカス中はwasd・Spaceを押しても動かない");
    await p.PressKey(Key.B);
    await p.WaitUiState(UIStateEnum.BuildMenu, 5f);
    await p.ClickWebUi("build-menu-search");
    await p.Until(() => WebUiInputExclusivity.IsTextInputFocused, 5f, "検索欄に文字入力フォーカスが入る");
    var beforeTyping = p.PlayerPosition;
    foreach (var typedKey in new[] { Key.W, Key.A, Key.S, Key.D, Key.Space }) await p.PressKey(typedKey);
    var typingWalk = await WalkForward();
    var typingMoved = Vector3.Distance(beforeTyping, p.PlayerPosition);
    p.Assert(typingWalk < 0.05f && typingMoved < 0.05f, $"検索欄にフォーカス中は動かない ({typingMoved:F2}m)");
    await p.CloseWebUiPanel();
    await p.WaitUiState(UIStateEnum.GameScreen, 5f);
    await p.Until(() => !WebUiInputExclusivity.IsTextInputFocused, 5f, "閉じると文字入力フォーカスが外れる");
    var resumedWalk = await WalkForward();
    p.Assert(resumedWalk > 1f, $"検索欄を離れると再び歩ける ({resumedWalk:F2}m)");

    // ポーズメニュー中は歩けない
    // Walking is stopped while the pause menu is open
    p.Note("ポーズメニュー中はWを押しても歩かない");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.PauseMenu, 5f);
    var pauseWalk = await WalkForward();
    p.Assert(pauseWalk < 0.05f, $"ポーズ中は歩けない ({pauseWalk:F2}m)");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 5f);
    await p.Screenshot("01-menus-walk");

    // 石窯を開き、開いた直後は閉じず、歩いて離れると閉じる
    // Open the oven; it stays open right after opening and closes once the player walks away
    p.Note("石窯を設置して開き、Sで離れて自動で閉じるか確かめる");
    p.WarpPlayer(new Vector3(0f, testFieldTopY + 1f, 0f));
    await p.WaitSeconds(1.5f);
    await p.PrepareBlockForUiPlacement(ovenBlockName, 1);
    var ovenOrigin = new Vector3Int(-1, Mathf.RoundToInt(testFieldTopY), 1);
    await p.PlaceBlockViaUi(ovenBlockName, ovenOrigin, BlockDirection.North);
    await p.ExitToGameScreen();
    var oven = await p.WaitBlockGameObject(ovenOrigin);
    p.WarpPlayer(new Vector3(0f, testFieldTopY + 1f, 0f));
    await p.WaitSeconds(1.5f);
    await p.AimAt(oven.transform.position + ovenModelCenterOffset);
    await p.WaitSeconds(0.5f);
    await p.PressInteract();
    await p.WaitUiState(UIStateEnum.SubInventory, 10f);
    await p.WaitSeconds(1f);
    p.Assert(p.CurrentUiState == UIStateEnum.SubInventory, "開いた直後は閉じない");

    // 石窯の当たり判定表面までの距離。到達判定と同じく表面で測る（モデルは3x3より小さい）
    // Distance to the oven's collider surface, measured like the reach check (the model is smaller than 3x3)
    float DistanceToOvenSurface()
    {
        var nearest = float.MaxValue;
        foreach (var collider in oven.GetComponentsInChildren<Collider>())
        {
            // 無効なClickColliderはClosestPointが入力点を返し0mになるため、物理に居る実体だけで測る
            // A disabled ClickCollider returns the query point from ClosestPoint (0m), so measure only live physical colliders
            if (!collider.enabled || collider.isTrigger) continue;
            nearest = Mathf.Min(nearest, Vector3.Distance(collider.ClosestPoint(p.PlayerPosition), p.PlayerPosition));
        }
        return nearest;
    }

    // 閉じた理由が距離であることを残すため、開いた時と閉じた時の表面距離を記録する
    // Record the surface distance at open and at close so the close is attributable to distance
    var openedDistance = DistanceToOvenSurface();
    p.Assert(openedDistance < InteractOverlap.InteractDistance, $"開いた位置は手の届く範囲 ({openedDistance:F2}m)");
    SemanticInput.KeyDown(Key.S);
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 5f, "離れると機械UIが閉じる");
    var closedDistance = DistanceToOvenSurface();
    SemanticInput.KeyUp(Key.S);
    p.Assert(InteractOverlap.InteractDistance - 0.1f < closedDistance, $"閉じたのは手の届く範囲を出た時 ({closedDistance:F2}m)");
    await p.Screenshot("02-closed-out-of-reach");
});
