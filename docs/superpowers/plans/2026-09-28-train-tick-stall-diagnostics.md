# Train Tick Stall Diagnostics Implementation Plan

> **For the controller session (実装担当は無視):** subagent-driven-developmentを使う。既存の作業ディレクトリ・`codex/remove-train-resync-request`を継続するというユーザー指定が隔離worktree・新規セッション・PR作成の一般既定に優先する。親がUnity検証・コミット・同名featureへのpushを担う。

**Goal:** 欠落した列車同期tickを待機し、乖離の始点と受信履歴をクライアントの診断ファイルに残す。
**Architecture:** 既存の連番バッファとhash gateを使い、未来hashによる飛び越しを除去する。列車専用の診断クラスへ受信・適用・待機・初期化完了を通知し、履歴・閾値・保存判断を集約する。サーバーの採番と他系統の更新は既存経路を使う。
**Tech Stack:** C#, Unity, VContainer, Newtonsoft.Json, NUnit, uloop。

## Requirements

- R1: 期待tick/連番が欠けたとき、後続hash/eventがあっても列車・レールの順序付き処理は進まない。数千回の再評価でも飛び越さない。
- R2: 必要なメッセージが後から揃えば順番に適用して再開する。hash不一致は検証成功まで待機する。
- R3: UI等のフレーム更新は継続する。終了・全体pause・再同期要求を追加しない。
- R4: 最初の待機日時、期待tick/連番、直前の適用位置、最新受信tick、検知時と保存時の乖離、hash比較値、初回待機直前と保存直前の受信履歴を保存する。
- R5: 後続受信で確認できた最新tickと適用tickの差が200以上で保存。無通信中は最初の待機位置と日時を保持し、後続受信で実測乖離が閾値へ達したときに保存する。
- R6: 同じ未解消の待機では保存・警告を連発しない。待機解消後の別の欠落は新しい診断になる。短い正常待ちでは診断ファイルを作らない。
- R7: 履歴は上限付き。直前履歴が循環しても初回待機の記録は失わない。保存失敗はログと失敗結果で観測でき、他の処理を止めない。
- R8: 初回full snapshot適用成功後に診断を有効化する。初回rail→train適用順序・watermark・完了/失敗伝播を維持する。

## Global Constraints

- 既存folder/branch、開始HEAD `9ae6eebc39eda1c7a7d11038a0f0af43d9d41d3d`。master操作、worktree作成、PRは行わない。ユーザーの承認済み段階1〜3を保持する。
- 実装範囲はクライアントのTrain/Network・Unit・View、DI、対応テスト。サーバーtick時計・プロトコル・セーブ形式・アセットの再設計をしない。
- 記録日時はUTC。新しい汎用clock/interfaceを作らない。受信済みtickを推測で変更しない。
- .metaはUnityに生成させる。Library削除、Unity YAML直接編集、partial、Func、新しいテスト専用publicは禁止。
- コードファイルは200行以内、作成先1ディレクトリ10ファイル以内。主要コメントは簡潔な日英ペア。
- 保存条件はADRのユーザー裁定に従う。追加のバグ報告送信機能は要求範囲外。

## Task 1: Ordered waiting and local diagnostics

**Files (責務):**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/TrainUnitHashVerifier.cs` — hash判定と待機理由通知。
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainUnitFutureMessageBuffer.cs` — 受信・適用位置の診断通知。並び順・古いメッセージ破棄の意味を維持。
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainFullSnapshotEventNetworkHandler.cs` — 初回成功後の診断有効化。
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/TrainUnitClientSimulator.cs` — 進行予算が0の間も欠落を観測し、待機中の後着メッセージを再評価する。
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs` — 診断と保存先をDIへ登録。
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/Diagnostics/TrainSynchronizationDiagnostics.cs` — 待機エピソード・閾値・履歴の所有者。
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/Diagnostics/TrainSynchronizationDiagnosticReport.cs` — 永続診断のスナップショットと受信履歴データ。
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/Diagnostics/TrainSynchronizationDiagnosticWriter.cs` — JSONローカル保存のディスク境界。
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/TrainUnitFutureMessageBufferTest.cs` — 必須依存更新。
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/TrainFullSnapshotFailurePropagationTest.cs` — 必須依存更新、初回失敗伝播保持。
- Create: `moorestech_client/Assets/Scripts/Client.Tests/TrainSynchronization/TrainSynchronizationDiagnosticsTest.cs` — 閾値・履歴・無通信・重複抑制・保存失敗。
- Create: `moorestech_client/Assets/Scripts/Client.Tests/TrainSynchronization/TrainSynchronizationWaitingTest.cs` — 欠落継続と後着再開、hash不一致。
- Create: `moorestech_client/Assets/Scripts/Client.Tests/TrainSynchronization/TrainSynchronizationTestContext.cs` — 実キャッシュ・gate・simulator・snapshot適用と一時診断フォルダを組み合わせるテスト環境。
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/TrainSynchronization/TrainSynchronizationWaitingPlayTest.cs` — 実バッファとgate/simulatorによる順序欠落・他フレーム継続・初期snapshot後の無通信。

**Interfaces:** 既存gate `bool CanAdvanceTick(ulong expectedId)` とバッファAPIを維持。診断の公開メソッドは実際の呼び出し元を持つ通知に限定し、次を基本形にする（データ引数の型はreportへまとめてよい）。

```csharp
// 初期snapshot成功時、受信時、適用時、検証時に呼ぶ。
internal void Initialize(ulong appliedId);
internal void RecordReceived(string kind, uint tick, uint sequenceId);
internal void RecordApplied(ulong appliedId);
internal void RecordMissingOrderedMessage(ulong expectedId);
internal void RecordHashMismatch(ulong expectedId, uint localTrainHash, uint serverTrainHash, uint localRailHash, uint serverRailHash);
```

診断は `TrainUnitTickState` と具体のwriterを受け取る。writerはDIが渡す `Application.persistentDataPath/Logs/TrainSynchronization` を使い、テストも同じ正規コンストラクタに固有の一時フォルダを渡す。新設interface、偽時計、テスト用publicを作らない。UTC日時の取得は診断境界に揃える。

- [x] gateの飛び越しを以下の意味へ変更する。未来メッセージ有無は診断に使い、進行許可には使わない。正常な一時待ちを毎tick警告しない。明らかな欠番/hash不一致は最初に理由を記録・警告する。

```csharp
if (!_futureMessageBuffer.TryDequeueHashAtTickSequenceId(currentTickUnifiedId, out var message))
{
    // 診断へ期待IDとMissingOrderedMessageを通知した後、必ず待機。
    return false;
}
// dummy、hash一致のみRecordAppliedTickUnifiedIdしてtrue。不一致は診断へ通知してfalse。
```

- [x] 診断クラスは最初の待機位置とその時点の履歴を固定する。受信履歴は前例 `FrameTickLog` と同じ上限付きQueue（256件）で保持し、保存時の直近履歴も別に記録する。初回snapshot以前の待機では保存しない。
- [x] 閾値判定は診断クラスの一箇所へ置き、列車tickループが止まっても後続受信で閾値へ達したら保存する。待機位置が消費されたことを適用通知で判定して次エピソードへ移る。
- [x] writerは一意な名前のJSONを保存し、成功パスをログへ出す。IO/権限エラーのみ外部境界で捕捉し、失敗理由をログと結果に残す。ファイル書き込み例外でUIや他の更新を止めない。同一エピソードの書き込み試行を毎フレーム繰り返さない。
- [x] 正常経路・異常経路のテストを追加する。以下を実バッファ/gateで確認し、診断は実際の一時フォルダのJSONを読み戻して検証する。

```csharp
// 概念例。fixtureで実在クラスと空のtrain/rail cacheを組む。
for (var i = 0; i < 1000; i++) Assert.IsFalse(gate.CanAdvanceTick(missingId));
Assert.AreEqual(beforeId, tickState.GetAppliedTickUnifiedId());
buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, missingTick, missingSequence);
Assert.IsTrue(gate.CanAdvanceTick(missingId));
Assert.AreEqual(missingId, tickState.GetAppliedTickUnifiedId());
```

検証ケース: 後続hashのみ/同tick連番欠落/後続eventのみ/空buffer/199対200tick境界/初回snapshot前は保存なし/無通信中は保存せず後続受信で閾値判定/初回履歴の保持/同じ停止で一度/回復後の別停止/保存先がファイルで書けない/一致hashとdummy正常進行。PlayModeへ移行する軽量テストで複数フレームの列車待機中も別のフレームカウンタが進むことを確認する。初回snapshot完了直後から一件も受信せず待機するケースを、実バッファ・gate・simulatorの駆動経路で検証する。

履歴は到着履歴として、staleによる破棄や同じIDの上書きより前に記録する。欠落時のserver hashは不明であり0やdummyを実測値として記録しない。必要ならmissing通知とmismatch通知を別メソッドにして、存在しないhash値を要求しないAPIにする。

- [x] 親が `uloop compile --project-path ./moorestech_client` を実行し、該当TrainSynchronizationテストと既存のTrainFullSnapshotEventPacketTest・TrainFullSnapshotFailurePropagationTest・InitialEventApplyWaiterTest・InitialApplyTaskConcurrentAwaitTest・TrainUnitFutureMessageBufferTest・TrainUnitTickStateTestを絞って実行する。2026-09-28: compileエラー0件、既存箇所の警告39件、対象テスト34/34成功。PlayMode遷移時のCLI接続断後、Unity保存XMLで全件完了を確認。
- [ ] 検証後に全作業をコミットする。コミット前にbranch・HEAD・対象ファイル一覧を再確認し、生成pin差分を除外する。

## Closing tasks

- [ ] moores-code-reviewの最終ブランチレビューを実施し、指摘を実コードで検証して必要な修正・対象テスト再実行を行う。
- [ ] ユーザー指定に従い、既存featureへ明示refspecでpushする。PR作成は行わない。masterのrefと作業treeが期待どおりであることを確認する。

## 判断記録（ADR）

- [ADR 0071](../../adr/0071-train-tick-stalls-retain-client-diagnostics.md) が挙動と出所の正本。200tick差、後続tickから実測乖離を確認できた場合だけ保存する条件はユーザー裁定。
- [agent前提] gateと診断の接続は同時に検証可能な一つの実装タスクとする。時計・汎用同期基盤の新設は利益が無いため行わない。
- [agent前提] ヘッドレスで欠番を注入する決定的テストと軽量なEditModeInPlayingTestを採用する。手動ゲームプレイの録画では任意の連番欠落・保存時点を再現しにくく、今回はunityプレイ録画テストを追加しない。
- [agent前提] データフローは既存受信/適用→診断状態→JSON保存。診断は進行可否を決めず、hash gateが進行を決める。新規ファイルはClient.Gameの列車Network下、DIはClient.Starter、テストはClient.Testsに置く。
