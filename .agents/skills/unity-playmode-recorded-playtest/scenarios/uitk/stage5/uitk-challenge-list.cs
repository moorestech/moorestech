// チャレンジ一覧を UITK（GameUi）で通す: T で開いて進行中のカードへ中央寄せ・2560×1080 でも中央・アンカー・中身なりの高さのカード・HUD が一覧より上・パンして T で閉じて開き直しても視点が残る・一覧の上の右短押しでは閉じない・Tab でインベントリ
// Runs the challenge list on UITK (GameUi): open with T centered on the current card, still centered at 2560x1080, anchors, content-height cards, HUD above the list, pan then close and reopen with T keeps the view, a right short press over the list does not close, Tab goes to the inventory
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Root;
using Client.Ui.State.Challenge;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

return PlaytestRunner.Run("uitk-challenge-list", new PlaytestRunOptions { Record = true }, async p =>
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
    var root = GameUiRoot.Instance.GetRootElement();

    // 解放済みの先頭カテゴリと、マスタの順で最初の進行中を源から引く（Web の categories[0] と findChallengeInitialFocusNode）
    // Take the first unlocked category and its first current challenge in master order from the source (Web categories[0] and findChallengeInitialFocusNode)
    var progress = ClientDIContext.DIContainer.DIContainerResolver.Resolve<ChallengeProgressState>();
    var category = progress.Categories.First(c => c.IsUnlocked);
    var current = category.Category.Challenges.First(c => category.CurrentChallenges.Contains(c));
    var cardId = $"challenge-node-{current.ChallengeGuid}";
    p.Note($"カテゴリ {category.Category.CategoryName}（解放 {progress.Categories.Count(c => c.IsUnlocked)} 個）・進行中 {current.Title}");

    // T で開き、進行中のカードがビューポートの中央にある
    // Open with T; the current card sits at the viewport center
    await p.PressKey(Key.T);
    await p.WaitUiState(UIStateEnum.ChallengeList, 10f);
    await p.UntilUiElement("challenge-panel", 5f);
    var offset = await CenterOffset();
    p.Note($"進行中のカードの中心とビューポートの中心の差 {offset}");
    p.Assert(offset.magnitude <= 2f, "進行中のカードがビューポートの中心から 2px 以内");
    await p.Screenshot("01-open-centered-1280x720");

    // 2560×1080 でも寸法の追従で中央のまま。並べ比べの UITK 側を撮って 1280×720 へ戻す
    // At 2560x1080 resize-follow keeps it centered; take the UITK comparison shot and return to 1280x720
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 2560, 1080);
    await UniTask.DelayFrame(3);
    var wideOffset = await CenterOffset();
    p.Note($"2560×1080 の差 {wideOffset}");
    p.Assert(wideOffset.magnitude <= 3f, "2560×1080 でも進行中のカードが中央（3 物理 px 以内）");
    await p.Screenshot("01b-open-centered-2560x1080");
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
    await UniTask.DelayFrame(3);

    // 枠・カテゴリ列・先頭カテゴリの全ノードのアンカーが一意に引ける
    // The frame, category row and every node of the first category resolve uniquely
    var anchorIds = new[] { AnchorIds.ChallengePanel, AnchorIds.ChallengeCategories }.Concat(category.Category.Challenges.Select(c => AnchorIds.ChallengeNode(c.ChallengeGuid.ToString()))).ToList();
    var unique = anchorIds.Count(id => TutorialAnchorRegistry.Resolve(root, id) is TutorialAnchorResolution.Unique);
    p.Note($"アンカー 一意 {unique}/{anchorIds.Count}");
    p.Assert(unique == anchorIds.Count, "枠・カテゴリ列・全ノードのアンカーが Unique");

    // カードは幅 240・中身なりの高さ（包みの高さ 0 で潰れない）
    // Cards are 240 wide with content height (not crushed by a zero-height wrapper)
    var card = root.Q(cardId);
    p.Note($"進行中のカードの寸法 {card.layout.size}");
    p.Assert(Mathf.Abs(card.layout.width - 240f) < 0.5f && card.layout.height > 60f, "カードが幅 240・中身なりの高さ");

    // 進行中 HUD は一覧とカテゴリのボタンより上、中心の当たりが HUD 自身（判定の間だけ根を拾わせる）
    // The current HUD is above the list and category buttons, and its center hit is the HUD (the root picks only during the check)
    var hud = root.Q("challenge-hud");
    p.Assert(hud.worldBound.yMax <= root.Q("challenge-panel").worldBound.yMin + 0.5f, "HUD が一覧の上端より上");
    p.Assert(hud.worldBound.yMax <= root.Q($"challenge-category-{category.Category.CategoryGuid}").worldBound.yMin + 0.5f, "HUD がカテゴリのボタンより上");
    var savedPicking = hud.pickingMode;
    hud.pickingMode = PickingMode.Position;
    var picked = root.panel.Pick(hud.worldBound.center);
    hud.pickingMode = savedPicking;
    p.Assert(picked != null && (picked == hud || hud.Contains(picked)), $"HUD の中心の当たりが HUD 自身（当たり={picked?.name}）");

    // 空の背景をパンし、T で閉じて T で開き直しても視点が残る
    // Pan on empty background; closing and reopening with T keeps the view
    var viewport = await PlaytestUiElementOps.FindUi("challenge-viewport");
    // ScreenRect は左下原点なので右下の隅はホットバーに当たる。進行中のカードの左上、行と行の間の空きを押す
    // ScreenRect is bottom-left origin so the lower-right corner hits the hotbar; press the gap left of and above the current card between rows
    var empty = viewport.ScreenRect.center + new Vector2(-200f, 120f);
    p.Note($"空の背景として押す点 {empty}（ビューポート {viewport.ScreenRect}）");
    var beforePan = await CardCenter();
    SemanticInput.MouseMoveTo(empty);
    await p.WaitSeconds(0.5f);
    // 押下・移動・解放は 0.2 秒ずつ空ける（前例 uitk-research-tree の実測）
    // Space press, move and release by 0.2 s (measured in uitk-research-tree)
    SemanticInput.MouseButtonDown(0);
    await p.WaitSeconds(0.2f);
    await SemanticInput.MouseGlideTo(empty + new Vector2(-60f, 30f), 0.4f);
    await p.WaitSeconds(0.2f);
    SemanticInput.MouseButtonUp(0);
    var panned = await SettledCardCenter();
    p.Note($"パンでカードが {panned - beforePan} 動いた");
    p.Assert((panned - beforePan).magnitude > 30f, "背景のドラッグでパンした");
    await p.PressKey(Key.T);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.PressKey(Key.T);
    await p.WaitUiState(UIStateEnum.ChallengeList, 10f);
    await p.UntilUiElement("challenge-panel", 5f);
    var reopened = await CardCenter();
    p.Note($"開き直したカードの中心 {reopened}（閉じる前 {panned}）");
    p.Assert((reopened - panned).magnitude <= 1f, "開き直しても視点が残る（1px 以内）");
    await p.Screenshot("02-reopen-kept");

    // 一覧の上の右短押しでは閉じず、Tab でインベントリへ移ると一覧が消える
    // A right short press over the list does not close; Tab moves to the inventory and the list goes away
    SemanticInput.MouseMoveTo(empty);
    await p.WaitSeconds(0.5f);
    SemanticInput.MouseButtonDown(1);
    await p.WaitSeconds(0.1f);
    SemanticInput.MouseButtonUp(1);
    await p.WaitSeconds(0.5f);
    p.Assert(p.CurrentUiState == UIStateEnum.ChallengeList, "一覧の上の右短押しでは閉じない");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    p.Assert(!(await PlaytestUiElementOps.FindUi("challenge-panel")).IsFound, "インベントリへ移ると一覧が消える");

    #region Internal

    async UniTask<Vector2> CardCenter() => (await PlaytestUiElementOps.FindUi(cardId)).ScreenRect.center;

    async UniTask<Vector2> CenterOffset() => (await CardCenter()) - (await PlaytestUiElementOps.FindUi("challenge-viewport")).ScreenRect.center;

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

    #endregion
});
