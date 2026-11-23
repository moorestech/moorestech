// 出荷マスタのフィルター分岐器を東向きに置いて開き、出口の見出しが左・正面・右の順に出てモード名が言い換わっていることを確かめる
// Place the shipped filter splitter facing east, open it, and confirm the exit headings read left, front, right and the mode names are reworded
using System.Reflection;
using Client.Game.InGame.Control;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Text;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

return PlaytestRunner.Run("filter-splitter-side-labels", new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 180f }, async p =>
{
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    foreach (var device in new InputDevice[] { Keyboard.current, Mouse.current }) InputSystem.EnableDevice(device);

    // 平地に分岐器を東向きで置く（向きを北以外にして、見出しが置く向きで変わらないことも見る）
    // Place the splitter facing east on flat ground (a non-north facing also shows the headings do not depend on facing)
    p.Note("出荷マスタのフィルター分岐器を東向きに置く");
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen");
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    var splitterPos = new Vector3Int(-22, 32, -20);
    var aimPoint = (Vector3)splitterPos + new Vector3(0.5f, 0.4f, 0.5f);
    await p.PlaceBlockDirect("フィルター分岐器", splitterPos, BlockDirection.East);
    await p.WaitSeconds(0.5f);
    var blockObject = await p.WaitBlockGameObject(splitterPos);
    await p.Until(() => !blockObject.IsPlayingPlaceAnimation, 30f, "フィルター分岐器の設置演出が終わった");

    // 南面の手前から Alt のカーソル照準で狙って開く（前例 uitk-block-special-panels の Open）
    // Open it by aiming from just south with the Alt cursor (precedent: Open in uitk-block-special-panels)
    p.WarpPlayer(new Vector3(splitterPos.x + 0.5f, 32.5f, splitterPos.z - 0.7f));
    await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "分岐器の前に着地");
    SemanticInput.KeyDown(Key.LeftAlt);
    await p.AimAt(aimPoint);
    await p.PressInteract();
    await p.WaitUiState(UIStateEnum.SubInventory, 10f);
    SemanticInput.KeyUp(Key.LeftAlt);
    await p.Until(() => Heading(0)?.worldBound.height > 0 && Heading(1)?.worldBound.height > 0 &&
                       Heading(2)?.worldBound.height > 0 && Text("filter-direction-label-2") != "", 15f, "出口の見出しが3つ出る");

    // 出荷マスタの出口の並びは 左(-X)・正面(+Z)・右(+X)。見出しはその順で、モード名は 2026-11-20 の言い換え
    // The shipped master lists exits left (-X), front (+Z), right (+X); headings follow that order and mode names use the 2026-11-20 wording
    p.Assert(Text("filter-direction-label-0") == Localize.Get(LocalizationKeys.Ui.BlockInventory.FilterOutputLeft), $"方向0の見出しが左: 「{Text("filter-direction-label-0")}」");
    p.Assert(Text("filter-direction-label-1") == Localize.Get(LocalizationKeys.Ui.BlockInventory.FilterOutputFront), $"方向1の見出しが正面: 「{Text("filter-direction-label-1")}」");
    p.Assert(Text("filter-direction-label-2") == Localize.Get(LocalizationKeys.Ui.BlockInventory.FilterOutputRight), $"方向2の見出しが右: 「{Text("filter-direction-label-2")}」");
    p.Assert(Heading(0).worldBound.yMin < Heading(1).worldBound.yMin &&
             Heading(1).worldBound.yMin < Heading(2).worldBound.yMin, "見出しが上から左・正面・右の順に並ぶ");
    p.Assert(ModeText("filter-mode-0-Default") == Localize.Get(LocalizationKeys.Ui.BlockInventory.FilterDefault), "モード名がその他すべての文");
    p.Assert(ModeText("filter-mode-0-Whitelist") == Localize.Get(LocalizationKeys.Ui.BlockInventory.FilterWhitelist), "モード名が指定した物だけの文");
    p.Assert(ModeText("filter-mode-0-Blacklist") == Localize.Get(LocalizationKeys.Ui.BlockInventory.FilterBlacklist), "モード名が指定した物以外の文");
    p.Note($"入口から見た順に {Heading(0).SourceText} / {Heading(1).SourceText} / {Heading(2).SourceText}");
    await p.Screenshot("filter-splitter-side-labels");
    await p.WaitSeconds(0.5f);

    await p.ClickUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 北西から入口と左出口を同じ画面に収める（既存の右ドラッグ操作で視点を回す）
    // Look from the northwest to capture the inlet and left exit together (turn using the existing right-drag action)
    p.Note("東向きの分岐器を北西から見る：西面が入口、北面が左出口");
    p.WarpPlayer(new Vector3(splitterPos.x - 2.1f, 32.5f, splitterPos.z + 1.3f));
    await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "分岐器の北西側に着地");
    var cameraController = UnityEngine.Object.FindFirstObjectByType<InGameCameraController>();
    var sensitivity = (Vector2)typeof(InGameCameraController).GetField("sensitivity", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cameraController);
    // Look のマウスデルタは入力アセットで半分になるため、度数をピクセルへ逆換算する
    // The input asset halves mouse delta for Look, so invert that scale when converting degrees to pixels
    const float lookMouseDeltaScale = 0.5f;
    var angle = Camera.main.transform.eulerAngles;
    await p.RightDrag(new Vector2(Mathf.DeltaAngle(angle.y, 135f) / sensitivity.x, -Mathf.DeltaAngle(angle.x, 30f) / sensitivity.y) / lookMouseDeltaScale);
    await p.Until(() => Vector3.Dot(Camera.main.transform.forward, Quaternion.Euler(30f, 135f, 0f) * Vector3.forward) > 0.999f,
        5f, "カメラが北西から分岐器を向く");
    // 設置済みモデルの回転でマスタの接続方向を確かめ、F3 で同じ接続口を画面に出す
    // Verify master connector directions against the placed model rotation, then show those ports using F3
    var connectors = ((IInventoryConnectors)blockObject.BlockMasterElement.BlockParam).InventoryConnectors;
    var inlet = Vector3Int.RoundToInt(blockObject.transform.TransformDirection(connectors.InputConnects[0].Directions[0]));
    var firstOutlet = Vector3Int.RoundToInt(blockObject.transform.TransformDirection(connectors.OutputConnects[0].Directions[0]));
    p.Assert(inlet == Vector3Int.left && firstOutlet == new Vector3Int(0, 0, 1),
        $"実物の入口が西、見出し0の出口が北: 入口 {inlet} / 出口 {firstOutlet}");
    await p.PressKey(Key.F3);
    await p.WaitUiState(UIStateEnum.Debug, 10f);
    await p.AimAt(aimPoint);
    await p.Screenshot("filter-splitter-east-facing");
    await p.WaitSeconds(0.5f);
    await p.PressKey(Key.F3);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    #region Internal

    // LocalizedLabel の text は行の高さの印を頭に付けた表示文なので、辞書の文と比べるときは原文（SourceText）を読む（前例 uitk-challenge-gate の PlainText）
    // LocalizedLabel.text is the display text prefixed with a line-height tag, so compare the source (SourceText) with the dictionary (precedent: PlainText in uitk-challenge-gate)
    LocalizedLabel Heading(int direction) => GameUiRoot.Instance.GetRootElement().Q<LocalizedLabel>($"filter-direction-label-{direction}");
    string Text(string testid) => (GameUiRoot.Instance.GetRootElement().Q<VisualElement>(testid) as LocalizedLabel)?.SourceText ?? "";
    string ModeText(string optionTestid) => GameUiRoot.Instance.GetRootElement().Q<VisualElement>(optionTestid)?.Q<LocalizedLabel>(className: "mode-switch__label")?.SourceText ?? "";

    #endregion
});
