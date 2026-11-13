// マスタのアンカー: ピン先マスタの uiHighLight 9 件を、その画面を開いて実ゲームの木で解決し枠を出す（UITK だけ）
// Master anchors: resolve the 9 uiHighLight anchors of the pinned master in the live tree of each screen and show the frame (UITK only)
// winunity play <branch> <このファイル> --ui-system GameUi で流す（全体スイッチは winunity が立てて Current を確かめる）
// Run with winunity play <branch> <this file> --ui-system GameUi (winunity sets the whole-UI switch and checks Current)
using System.Linq;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Root;
using Client.Ui.Screens.Tutorial.Anchor;
using Cysharp.Threading.Tasks;
using Client.WebUiHost.Boot;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-master-anchors", options, async p =>
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
    // 重い機械では1回の Skip で開幕スキットが終わらないことがあるので、Story のままなら飛ばし直す（前例 uitk-production-loop）
    // On a loaded machine one skip may not end the opening skit, so skip again while in Story (precedent uitk-production-loop)
    var skitSkipRounds = 1;
    await p.SkipOpeningSkitIfPlaying();
    while (skitSkipRounds < 4 && !await PlaytestUiOps.PollUiState(UIStateEnum.GameScreen, 5f)) { skitSkipRounds++; await p.SkipOpeningSkitIfPlaying(); }
    p.Note($"opening-skit-skip-rounds={skitSkipRounds}");
    await p.ExitToGameScreen();
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが GameUi（UITK側）");
    p.Assert(WebUiHost.Hub == null, "Webホストは起動していない（UITK側）");

    // マスタから集めた件数と中身を記録する
    // Record the count and contents collected from the master
    var anchors = PlaytestAnchorChecks.CollectUiHighlightAnchors();
    foreach (var a in anchors) p.Note($"{a.ChallengeGuid} {a.ChallengeTitle} → {a.AnchorId}");
    p.Assert(anchors.Count == 9, $"ピン先マスタの uiHighLight は 9 件（{anchors.Count}）");

    // ゲーム画面: チャレンジ HUD
    // Game screen: challenge HUD
    p.Note("ゲーム画面: チャレンジ HUD");
    foreach (var a in anchors.Where(a => a.AnchorId == AnchorIds.ChallengeCurrentHud)) await PlaytestAnchorChecks.CheckOutlineOnAnchor(p, a);

    // 持ち物: 木の板を選んでクラフトボタンとアイテムのセル
    // Inventory: select planks, then the craft button and the item cell
    p.Note("持ち物: 木の板→クラフトボタン・アイテムのセル");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    var plank = PlaytestItemOps.ResolveItemId("木の板");
    var plankCell = $"item-list-cell-{plank.AsPrimitive()}";
    await PlaytestUiElementOps.ScrollDownUntilUiElementIsClickable(plankCell, "item-list-scroll", 10f);
    await p.ClickUi(plankCell);
    // 木の板の先頭クラフトレシピのボタンが出てからアンカーを見る（前例 uitk-production-loop）
    // Check anchors once the plank's head craft recipe button is up (precedent uitk-production-loop)
    await p.UntilUiElement("craft-button-37623c6d-e1cf-4985-abfb-8239e6e33981", 10f);
    foreach (var a in anchors.Where(a => a.AnchorId == AnchorIds.RecipeCraftButton)) await PlaytestAnchorChecks.CheckOutlineOnAnchor(p, a);
    var craftAnchor = anchors.First(a => a.AnchorId == AnchorIds.RecipeCraftButton);
    await PlaytestAnchorChecks.CheckOutlineOnAnchor(p, new PlaytestMasterAnchor(AnchorIds.RecipeItem(plank.AsPrimitive()), craftAnchor.TutorialGuid, craftAnchor.ChallengeGuid, "アイテムのセル（マスタ外の確認）"));
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 研究ツリー: 研究ノード 7 件
    // Research tree: the seven research nodes
    p.Note("研究ツリー: 研究ノード 7 件");
    await p.PressKey(Key.R);
    await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
    await p.WaitSeconds(1f);
    // 初期の視口の外にあるノードもあるので、本番のパン（左ドラッグ）で視口の中央へ寄せてから測る
    // Some nodes start outside the viewport, so pan them to its center with the production left-drag before measuring
    foreach (var a in anchors.Where(a => a.AnchorId.StartsWith(AnchorIds.ResearchNodePrefix)))
    {
        await PanResearchNodeToViewCenter(a.AnchorId);
        await PlaytestAnchorChecks.CheckOutlineOnAnchor(p, a);
    }
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 9 件のどれも画面の割り振りから漏れていない
    // None of the 9 anchors is left out of the screen routing
    var routed = anchors.Count(a => a.AnchorId == AnchorIds.ChallengeCurrentHud || a.AnchorId == AnchorIds.RecipeCraftButton || a.AnchorId.StartsWith(AnchorIds.ResearchNodePrefix));
    p.Assert(routed == anchors.Count, $"全アンカーを画面に割り振った（{routed}/{anchors.Count}）");

    #region Internal

    async UniTask PanResearchNodeToViewCenter(string anchorId)
    {
        var root = GameUiRoot.Instance.GetRootElement();
        for (var round = 1; round <= 6; round++)
        {
            // silent-ok: 解決しないときは寄せずに進む / 直後の CheckOutlineOnAnchor の Assert が落とす
            var unique = TutorialAnchorRegistry.Resolve(root, anchorId) as TutorialAnchorResolution.Unique;
            if (unique == null) return;

            // 本番の枠と同じ見える範囲の中央とノードの中央の差を、根の座標で測る
            // Measure the gap between the visible-region center (same as the production frame) and the node center in root space
            var visible = TutorialAncestorClipWalker.ClipOf(unique.Element, root);
            var viewCenter = new Vector2((visible.Left + visible.Right) / 2f, (visible.Top + visible.Bottom) / 2f);
            var offset = viewCenter - root.WorldToLocal(unique.Element.worldBound).center;
            if (offset.magnitude <= 20f) { p.Note($"{anchorId} を視口の中央へ寄せた（ドラッグ {round - 1} 回・残り {offset.magnitude:F1}px）"); return; }

            // 1 回の移動は見える範囲の 4 割までにし、中央を挟んで押して離すので指が視口の外へ出ない
            // Cap one move at 40% of the visible region and straddle the center so the pointer never leaves the viewport
            var limit = new Vector2(visible.Right - visible.Left, visible.Bottom - visible.Top) * 0.4f;
            var step = new Vector2(Mathf.Clamp(offset.x, -limit.x, limit.x), Mathf.Clamp(offset.y, -limit.y, limit.y));
            await DragInRoot(root, viewCenter - step / 2f, viewCenter + step / 2f);
        }
        p.Note($"{anchorId} は 6 回のドラッグで視口の中央へ寄り切らなかった");
    }

    async UniTask DragInRoot(VisualElement root, Vector2 fromRoot, Vector2 toRoot)
    {
        // 根の座標を左下原点のスクリーン座標へ直す（GameUiScreenMapper と同じ換算）
        // Convert root coordinates to bottom-left screen coordinates (same mapping as GameUiScreenMapper)
        var scale = new Vector2(Screen.width / root.worldBound.width, Screen.height / root.worldBound.height);
        Vector2 ToScreen(Vector2 v) => new Vector2(v.x * scale.x, Screen.height - v.y * scale.y);

        // 押す前に間を置き、離す前に止まって滑走（慣性）を起こさない
        // Pause before pressing and stop before releasing so no inertial glide starts
        SemanticInput.MouseMoveTo(ToScreen(fromRoot));
        await p.WaitSeconds(0.3f);
        SemanticInput.MouseButtonDown(0);
        await UniTask.DelayFrame(2);
        await SemanticInput.MouseGlideTo(ToScreen(toRoot), 0.4f);
        await p.WaitSeconds(0.3f);
        SemanticInput.MouseButtonUp(0);
        await p.WaitSeconds(0.3f);
    }

    #endregion
});
