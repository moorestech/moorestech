// 通知の並べ比べ撮影: GameUi（UITK）の本番の通知の層に参照 2-notifications と同じ 4 行（獲得・研究完了・アイテム解放・拒否）を日本語で出し、Game View の実画面を 1280×720 と 2560×1080 で撮る
// 参照は mock-host の画面なので、背面の層に参照と同じ橙の地を敷き、行のアイコンを mock-host の仮アイコン（demoAssets.ts の placeholderIcon）・文を参照と同じ文（Item 2 +5・最初の研究）に差し替える。行の段組・色・寸法は本番のまま
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Notification comparison shots: show the same four rows as reference 2-notifications (earned, research completed, item unlocked, denied) in Japanese on the production notification layer and shoot the real Game View at 1280x720 and 2560x1080
// The reference is a mock-host screen, so lay the same orange ground behind and swap row icons for the mock-host placeholder icons (demoAssets.ts placeholderIcon) and texts for the reference texts (Item 2 +5, 最初の研究); row layout, colors and sizes stay production
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Operations.Ui;
using Client.Playtest.Overlay;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Icons;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.Notification;
using Client.Ui.State.Notification.Content;
using Client.Ui.State.Text;
using Client.WebUiHost.Boot;
using Core.Master;
using Cysharp.Threading.Tasks;
using Server.Event.Notification;
using UnityEngine;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = false };
return PlaytestRunner.Run("uitk-notification-compare", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    p.Assert(WebUiHost.Hub == null, "Webホストは起動していない（GameUi）");

    // 参照の元データ（W/e2e/mock-host/topics/topicControls.ts の通知の 4 つ）と同じ順・同じ中身。研究完了はアイテム 1 のアイコン付き
    // Same order and contents as the reference source data (the four notification controls in topicControls.ts); research completed carries the item 1 icon
    Localize.TrySetChosenLanguage("japanese");
    var research = MasterHolder.ResearchMaster.GetAllResearches().First();
    var earned = NotificationMessagePack.CreateItemEarned(new ItemId(2), 5);
    var researchCompleted = NotificationMessagePack.CreateAchievementWithItem("achievement.researchCompleted", new Dictionary<string, string> { ["research"] = research.ResearchNodeGuid.ToString() }, new ItemId(1));
    var unlocked = NotificationMessagePack.CreateAchievementWithItem("achievement.unlockedItem", new Dictionary<string, string>(), new ItemId(2));
    var denied = NotificationMessagePack.CreateOperationDenied("denied.researchNotCompletable", new Dictionary<string, string>());
    var placeholderIcons = new Dictionary<int, Texture2D> { [1] = PlaceholderIcon(1), [2] = PlaceholderIcon(2) };

    // 世界の代わりに参照と同じ橙の地（W/public/mock-orange-gradient.png を center/cover）を背面の層の一番下に敷く
    // Lay the reference's orange ground (W/public/mock-orange-gradient.png, center/cover) at the bottom of the behind-stage layer instead of the world
    var layer = GameUiRoot.Instance.GetLayer(UiLayer.BehindStage, UiFamily.Viewport);
    var ground = new VisualElement { name = "compare-mock-ground", pickingMode = PickingMode.Ignore };
    var groundTexture = new Texture2D(2, 2);
    groundTexture.LoadImage(File.ReadAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "moorestech_web", "webui", "public", "mock-orange-gradient.png"))));
    ground.style.position = Position.Absolute;
    ground.style.left = 0f;
    ground.style.top = 0f;
    ground.style.right = 0f;
    ground.style.bottom = 0f;
    ground.style.backgroundImage = groundTexture;
    ground.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
    layer.Insert(0, ground);

    // 行に重なる操作の案内（参照の状態より行が多い）と、DSL の操作ログ（OnGUI）を撮る間だけ隠す
    // Hide the key hints overlapping the rows (more lines than the reference state) and the DSL action log (OnGUI) while shooting
    var keyHints = GameUiRoot.Instance.GetPanelDocument().rootVisualElement.Q("key-hints");
    keyHints.style.display = DisplayStyle.None;
    var actionLog = Object.FindFirstObjectByType<PlaytestOverlayView>();
    actionLog.enabled = false;

    // 途中の Assert で落ちても、同じ PlayMode で続けて動かすシナリオのために隠した物・言語・作ったテクスチャを戻す
    // Even when an Assert fails midway, restore what was hidden, the language and the created textures for scenarios run next in the same PlayMode
    try
    {
        await ShootRealScreen(1280, 720);
        await ShootRealScreen(2560, 1080);
    }
    finally
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
        ground.RemoveFromHierarchy();
        keyHints.style.display = StyleKeyword.Null;
        actionLog.enabled = true;
        Localize.TrySetChosenLanguage("english");
        Object.Destroy(groundTexture);
        foreach (var icon in placeholderIcons.Values) Object.Destroy(icon);
    }

    // Game View を指定の解像度にし、前の行が消えるのを待ってから 4 件を通知し、参照に合わせて差し替えて入場の後に撮る
    // Set the Game View resolution, wait for earlier rows to leave, notify the four rows, swap them to match the reference and shoot after the enter
    async UniTask ShootRealScreen(int width, int height)
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, width, height);
        await p.WaitSeconds(6f);
        p.Assert(Screen.width == width && Screen.height == height, $"Screen が {width}x{height} (実際 {Screen.width}x{Screen.height})");
        foreach (var message in new[] { earned, researchCompleted, unlocked, denied }) ClientDIContext.ClientLocalNotificationSource.Notify(message);
        await UniTask.DelayFrame(2);
        MatchReferenceRows();
        await p.WaitSeconds(0.6f);

        // 拒否の文は詰めた表示文（改行入り）に替わっている
        // The denied text has swapped to the trimmed display text (with a newline)
        var deniedText = layer.Query<NotificationRowView>().ToList().First(row => row.Row.Content is OperationDeniedNotificationContent).Q<LocalizedLabel>();
        p.Assert(deniedText.text.Contains("\n"), $"拒否の文が表示文: {deniedText.text.Replace("\n", "|")} 高さ {deniedText.layout.height}");
        p.Note($"{width}x{height} の拒否の文: {deniedText.text.Replace("\n", "|")} 高さ {deniedText.layout.height}");
        await p.Screenshot($"uitk-2-notifications-{width}x{height}");
    }

    // 行のアイコンを mock-host の仮アイコンへ、獲得と研究完了の文を参照の文へ替える（参照は mock-host のアイテム名・研究名）
    // Swap row icons for the mock-host placeholders and the earned / research texts for the reference texts (the reference uses mock-host names)
    void MatchReferenceRows()
    {
        foreach (var row in layer.Query<NotificationRowView>().ToList())
        {
            if (row.Row.Content.Icon is ItemNotificationIcon itemIcon)
            {
                var id = itemIcon.ItemId.AsPrimitive();
                row.Q<GameIcon>().SetIcon(new UiIcon(placeholderIcons[id], id.ToString(), UiText.FromVerbatim($"Item {id}")), GameIconFallback.IdText);
            }
            if (row.Row.Content is EarnedNotificationContent) row.SetText(UiText.FromVerbatim("Item 2 +5"));
            if (row.Row.Content.MessageId == "achievement.researchCompleted") row.SetText(UiText.FromVerbatim("研究完了: 最初の研究"));
        }
    }

    // mock-host の仮アイコン（demoAssets.ts placeholderIcon の 64×64 の SVG）を画素の被覆率で描く
    // Draw the mock-host placeholder icon (the 64x64 SVG of demoAssets.ts placeholderIcon) by pixel coverage
    Texture2D PlaceholderIcon(int itemId)
    {
        var hue = itemId * 47 % 360 / 360f;
        var pixels = new Color[64 * 64];
        Fill(10f, 10f, 54f, 54f, Hsl(hue, 0.40f, 0.52f));
        Ring(9f, 9f, 55f, 55f, 2f, Hsl(hue, 0.35f, 0.34f));
        Fill(11.5f, 12f, 12.5f, 52f, Hsl(hue, 0.45f, 0.68f));
        Fill(12f, 11.5f, 52f, 12.5f, Hsl(hue, 0.45f, 0.68f));
        Fill(12f, 51.5f, 52f, 52.5f, Hsl(hue, 0.35f, 0.38f));
        Fill(51.5f, 12f, 52.5f, 52f, Hsl(hue, 0.35f, 0.38f));
        Fill(18f, 20f, 46f, 29f, Hsl(hue, 0.42f, 0.62f));
        Fill(18f, 34f, 46f, 43f, Hsl(hue, 0.38f, 0.44f));
        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;

        // SVG の座標（左上原点）の矩形を、画素との重なりの面積を α にして重ねる（テクスチャは左下原点）
        // Composite an SVG-space (top-left origin) rect using each pixel's overlap area as alpha (the texture is bottom-left origin)
        void Fill(float x0, float y0, float x1, float y1, Color color)
        {
            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                var coverage = Mathf.Max(0f, Mathf.Min(x + 1, x1) - Mathf.Max(x, x0)) * Mathf.Max(0f, Mathf.Min(y + 1, y1) - Mathf.Max(y, y0));
                if (coverage <= 0f) continue;
                var index = (63 - y) * 64 + x;
                var under = pixels[index];
                var alpha = coverage + under.a * (1f - coverage);
                pixels[index] = new Color((color.r * coverage + under.r * under.a * (1f - coverage)) / alpha, (color.g * coverage + under.g * under.a * (1f - coverage)) / alpha, (color.b * coverage + under.b * under.a * (1f - coverage)) / alpha, alpha);
            }
        }

        void Ring(float x0, float y0, float x1, float y1, float width, Color color)
        {
            Fill(x0, y0, x1, y0 + width, color);
            Fill(x0, y1 - width, x1, y1, color);
            Fill(x0, y0 + width, x0 + width, y1 - width, color);
            Fill(x1 - width, y0 + width, x1, y1 - width, color);
        }
    }

    // CSS の hsl() と同じ換算（sRGB の値のまま）
    // Same conversion as CSS hsl() (values stay sRGB)
    Color Hsl(float hue, float saturation, float lightness)
    {
        var chroma = (1f - Mathf.Abs(2f * lightness - 1f)) * saturation;
        float Channel(float n)
        {
            var k = (n + hue * 12f) % 12f;
            return lightness - chroma / 2f * Mathf.Max(-1f, Mathf.Min(k - 3f, Mathf.Min(9f - k, 1f)));
        }
        return new Color(Channel(0f), Channel(8f), Channel(4f), 1f);
    }
});
