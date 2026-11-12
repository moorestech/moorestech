// サーバー発通知基盤スモーク（GameUi〈UITK〉と Web の両方で通す）
// 1) 素材ゼロで研究完了を試みる→操作拒否（operationDenied）通知が出る
// 2) 同一失敗を 3 秒以内に連打→クールダウンで 2 回目はワイヤに乗らない（通知は増えない）
// 3) 素材を付与して研究を完了→実績（achievement）通知「研究完了: <研究名>」が出る
// 研究はマスタから選ぶ（前提の研究が無く消費アイテムを持つ最初のノード）。GameUi のときは通知が背面の層に出たことと文言も確かめる
// Server-notification smoke (run on both GameUi (UITK) and Web):
// (1) complete-research with no materials -> an operation-denied notification
// (2) burst the same failure within 3s -> the cooldown suppresses the 2nd delivery
// (3) grant the materials and complete -> an achievement notification "Research completed: <name>"
// The research is picked from the master (the first node with no prerequisites and some consumed items); on GameUi it also checks the behind-stage layer and the text
// 検証は「クライアント側でEventTagを購読して実ペイロードをassert」+「通知ホストの可視化Until」+「録画/スクショ」の3点で行う
// Verification uses a client-side EventTag subscription (asserts the real payload) + notification-host visibility Until + video/screenshots
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Input;
using Client.Localization;
using Client.Playtest;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.WebUiHost.Boot;
using Core.Master;
using Cysharp.Threading.Tasks;
using MessagePack;
using Mooresmaster.Localization.Generated;
using Server.Event.Notification;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("server-notification-smoke", options, async p =>
{
    // デバッグ環境を整備する
    // Prepare the debug environment
    p.Note("デバッグ環境を構築し、通知イベントをクライアント側で購読する");
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());

    // 前提の研究が無く、消費アイテムを持つ最初のノードを選ぶ（GUID を直書きするとマスタの改訂で壊れる）
    // Pick the first node with no prerequisites and some consumed items (a hard-coded GUID breaks on master revisions)
    var firstResearch = MasterHolder.ResearchMaster.GetAllResearches()
        .First(node => node.PrevResearchNodeGuids.Length == 0 && node.ConsumeItems.Length > 0);
    var firstResearchGuid = firstResearch.ResearchNodeGuid;
    p.Note($"研究ノード: {firstResearchGuid}");

    // EventTagをクライアントで購読し、届いた通知の実ペイロードを蓄積する(NotificationTopicとは別の観測用購読)
    // Subscribe to the EventTag on the client and accumulate delivered payloads (observer sub, separate from NotificationTopic)
    var received = new List<NotificationMessagePack>();
    var subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(NotificationService.EventTag, payload =>
    {
        received.Add(MessagePackSerializer.Deserialize<NotificationMessagePack>(payload));
    });

    int DeniedResearchCount() => received.Count(n => n.Category == NotificationCategory.OperationDenied && n.MessageId == "denied.researchNotCompletable");
    int AchievementResearchCount() => received.Count(n => n.Category == NotificationCategory.Achievement && n.MessageId == "achievement.researchCompleted");

    // === Goal1+3: 拒否通知とクールダウン ===
    // === Goal1+3: denied notification and cooldown ===
    p.Note("素材ゼロで研究完了を試みる(1回目)。操作拒否通知が届くはず");
    ClientContext.VanillaApi.SendOnly.CompleteResearch(firstResearchGuid);
    await p.Until(() => 1 <= DeniedResearchCount(), 10f, "1回目の失敗で操作拒否通知(denied.researchNotCompletable)がクライアントに届く");

    // クールダウン窓内に同一失敗を連打
    // Burst the same failure inside the cooldown window
    var deniedAfterFirst = DeniedResearchCount();
    p.Note("同一失敗を3秒以内に連打(2回目)。クールダウンで通知は増えないはず");
    ClientContext.VanillaApi.SendOnly.CompleteResearch(firstResearchGuid);
    await p.WaitSeconds(1.8f);
    p.Assert(DeniedResearchCount() == deniedAfterFirst, $"クールダウンで2回目の失敗通知は抑制される(件数 {deniedAfterFirst} のまま 実際:{DeniedResearchCount()})");

    // 通知ホストの可視化を待って撮影(HUDはpointer-events:noneのためクリック可能待ちは使えない)
    // Wait for the notification host to render, then capture (the HUD is pointer-events:none, so the clickable wait cannot be used)
    await UntilHudVisible("notification-host", 10f);
    p.Assert(received.Last().MessageId == "denied.researchNotCompletable", $"直近通知が操作拒否である 実際:{received.Last().MessageId}");
    await p.Screenshot("01-denied-notification");

    // クールダウン明けを待ち、次フェーズ(実績)の観測を汚さない
    // Wait out the cooldown so the achievement phase observation stays clean
    await p.WaitSeconds(3.2f);

    // === Goal2: 実績通知 ===
    // === Goal2: achievement notification ===
    p.Note("研究に必要な素材をマスタの ConsumeItems どおりに付与する");
    foreach (var consume in firstResearch.ConsumeItems) await p.GiveItemDirect(MasterHolder.ItemMaster.GetItemMaster(consume.ItemGuid).Name, consume.ItemCount);
    await p.WaitSeconds(0.5f);

    var achievementBefore = AchievementResearchCount();
    p.Note("研究を完了する。実績通知『研究完了: <研究名>』が届くはず");
    ClientContext.VanillaApi.SendOnly.CompleteResearch(firstResearchGuid);
    await p.Until(() => achievementBefore < AchievementResearchCount(), 10f, "研究完了で実績通知(achievement.researchCompleted)がクライアントに届く");

    // ワイヤ値は研究GUIDであり、表示名はWeb側が解決する
    // The wire value is the research GUID; the web layer resolves its display name
    var achievement = received.Last(n => n.Category == NotificationCategory.Achievement && n.MessageId == "achievement.researchCompleted");
    p.Assert(achievement.MessageParams.TryGetValue("research", out var researchParam) && researchParam == firstResearchGuid.ToString(), $"実績通知の研究GUIDが送信した研究と一致する 実際:{string.Join(",", achievement.MessageParams)}");

    await UntilHudVisible("notification-host", 10f);

    // GameUi では通知が背面の層の実画面の族に居て、研究名の入った文言が出ていることを確かめる
    // On GameUi, check the host sits in the viewport family of the behind-stage layer and shows the text with the research name
    if (UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi)
    {
        p.Assert(WebUiHost.Hub == null, "GameUi の回は Web ホストが居ない");
        var behindStage = GameUiRoot.Instance.GetLayer(UiLayer.BehindStage, UiFamily.Viewport);
        p.Assert(behindStage.Q("notification-host") != null, "通知ホストは背面の層（BehindStage・Viewport）に居る");
        var researchName = Localize.GetContent(ContentLocalizationKeys.ResearchName(firstResearchGuid));
        await Client.Playtest.Operations.PlaytestUiElementOps.WaitUiTextContains("notification-host", researchName, 10f);
    }

    // 入場の動き（160ms）が終わってから撮る
    // Shoot after the enter motion (160ms) has finished
    await p.WaitSeconds(0.5f);
    await p.Screenshot("03-achievement-notification");

    // HUD要素の「存在+矩形あり」だけを待つ(空のホストは幅0のため、通知1件以上の描画完了と等価)
    // Wait only for presence + a non-empty rect (an empty host has zero width, so this equals "at least one notification rendered")
    async UniTask UntilHudVisible(string testid, float timeoutSeconds)
    {
        var deadline = UnityEngine.Time.realtimeSinceStartup + timeoutSeconds;
        while (UnityEngine.Time.realtimeSinceStartup < deadline)
        {
            var result = await Client.Playtest.Operations.PlaytestUiElementOps.FindUi(testid);
            if (result.IsFound && 0 < result.ScreenRect.width) return;
            await UniTask.Delay(TimeSpan.FromMilliseconds(250), ignoreTimeScale: true);
        }
        throw new Exception($"HUD要素が可視にならない: {testid}");
    }

    // 時間切れの退場まで録画に残す（録画は実行の終わりで切れる）
    // Keep recording until the rows time out and exit (the recording stops when the run ends)
    p.Note("通知が時間切れで退場するまで待つ");
    await p.WaitSeconds(8f);

    // 後片付け: 観測用購読を破棄する
    // Cleanup: dispose the observer subscription
    subscription.Dispose();
    p.Note("サーバー通知基盤スモーク完了(操作拒否・実績・クールダウンを確認)");
});
