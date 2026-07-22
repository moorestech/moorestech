# Task 8 Step 2 — サーバー通知基盤 PlayMode スモーク検証レポート

## 結論
**DONE** — 全4完了基準を満たした。フィーチャは3ゴールとも正常動作。実プロダクトのバグは検出されず。

- Success: true / Asserts 5件すべて PASS
- 録画 mp4 生成（1,173,112 bytes）、スクショ2枚が期待UIを右上に表示、絵は実プレイ視点（アバター・地面・HUD・ホットバー）

## シナリオ設計

ファイル: `.claude/skills/unity-playmode-recorded-playtest/scenarios/server-notification-smoke.cs`

Web UI（CEF）モードで PlayMode を起動（`SetupDebugEnvironment`）。検証は3層で行う:
1. **クライアント側の実ペイロード assert** — `ClientContext.VanillaApi.Event.SubscribeEventResponse(NotificationService.EventTag, ...)` で通知イベントを購読し、届いた `NotificationMessagePack` を `MessagePackSerializer` で復元して Category / MessageId / MessageParams を直接検証。DOM のテキスト読取ができない（後述）ため、これがワイヤ到達の強い assert になる。
2. **Web UI ホストの可視化 Until** — `p.UntilWebUiElement("notification-host", ...)` で右上 Stack が可視（＝通知が描画され高さを持つ）になるのを待つ。空の時は高さ0で不可視になるため「通知が出た」信号として機能する。
3. **録画 + スクショ** — 節目で `p.Screenshot`。

操作トリガは `ClientContext.VanillaApi.SendOnly.CompleteResearch(guid)`（本番プロトコル経路）。第一研究ノード `原始研究1`（GUID `837e9697-8586-406e-a0f6-16a010050218`、前提なし、消費: 木の板×5・木の棒×5）を使用。

### ゴール対応
- **Goal 1（操作拒否）**: 素材ゼロで `CompleteResearch` → `CompleteResearch` が false → `denied.researchNotCompletable` 通知。クライアント購読で受信を確認し、右上に黄色トースト「Cannot complete research (prerequisites or materials missing)」を撮影（01）。
- **Goal 2（実績）**: `GiveItemDirect` で木の板×5・木の棒×5 付与 → `CompleteResearch` 成功 → `achievement.researchCompleted` 通知、`MessageParams[0]=="原始研究1"` まで assert。右上に teal トースト「Research completed: 原始研究1」を撮影（03）。研究完了に伴うアンロック放送（`New item unlocked`）も同時に表示されている（`AchievementNotificationWiring` 仕様どおり）。
- **Goal 3（クールダウン）**: Goal 1 の直後、3秒クールダウン窓内に同一失敗を連打（2回目送信）→ 1.8秒待って受信件数が増えていないことを assert（`件数 1 のまま 実際:1`）。**ランタイムでワイヤ到達数を直接 assert**しており、単なる目視より強い検証になっている。

## 実行

| Run | 結果 | 備考 |
|---|---|---|
| 1 | 失敗（`game not ready within 180s`） | PlayMode boot 中にドメインリロードが発生し初期化が破壊。MapObjectMiningController / PlayerInventoryViewController / ThirdPersonController / HotBarView など多数の Update が毎フレーム NRE（6万件超）。シナリオコードは未実行（ready 前で中断）＝フィーチャ起因ではなく環境（boot 中リロード）起因。 |
| 2 | 成功 | 事前に `uloop compile`（0 error / 0 warning）で保留コンパイルをフラッシュ → ドメインリロード沈静後に再実行。全 assert PASS。 |

## result.json サマリ（Run 2）

Success: **true**

| Assert | Passed |
|---|---|
| 1回目の失敗で操作拒否通知(denied.researchNotCompletable)がクライアントに届く | ✅ |
| クールダウンで2回目の失敗通知は抑制される(件数 1 のまま 実際:1) | ✅ |
| 直近通知が操作拒否である 実際:denied.researchNotCompletable | ✅ |
| 研究完了で実績通知(achievement.researchCompleted)がクライアントに届く | ✅ |
| 実績通知の研究名パラメータが『原始研究1』 実際:原始研究1 | ✅ |

ErrorLogs: 空。

## 証跡パス

- 動画: `docs/superpowers/evidence/2026-07-22-server-notification-smoke.mp4`
- スクショ（操作拒否）: `docs/superpowers/evidence/2026-07-22-server-notification-smoke-01-denied.png`
- スクショ（実績）: `docs/superpowers/evidence/2026-07-22-server-notification-smoke-03-achievement.png`
- 元 result 一式: `moorestech_client/PlaytestResults/20260722_140023/server-notification-smoke/`（git 管理外）

## Caveat / 検証方法の注記

- **Goal 3 の検証方法**: プランは「notification-host の DOM 子要素数で数える」案を挙げていたが、`playtest.dom_query`（`domQueryResponder.ts` / `PlaytestDomQuery.cs`）は**単一要素の bounding rect と hit-test のみ**を返し、子要素数やテキストは返さない。よって DOM 子数カウントは現状の機構では不可。代わりに**クライアント側の EventTag 購読で「2回目の配信が来ないこと」をランタイム assert**した（ワイヤ到達層での直接検証。DOM 子数カウントより境界が近い）。加えてサーバー単体テスト `NotificationServiceTest.NotifySendsEventAndCooldownSuppressesDuplicate` でもクールダウンは検証済み。
- **通知テキストの検証**: DOM テキスト読取ができないため、Web 表示文言そのものは目視（スクショ01/03）で確認。文言の元になる `MessageId` / `MessageParams` はクライアント購読で厳密 assert 済み（テンプレ `notificationMessages.ts` が `{p0}` に流し込む値まで一致）。
- Run 1 の boot 中リロードは environment 起因の既知パターン（troubleshooting.md 参照）。フィーチャの欠陥ではない。フィーチャ側のバグは検出されなかった。
