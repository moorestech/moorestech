# Train Tick Stall Diagnostics Implementation Plan

> **For the controller session (実装担当は無視):** subagent-driven-developmentを使う。既存の作業ディレクトリ・`codex/remove-train-resync-request`を継続するというユーザー指定が隔離worktree・新規セッション・PR作成の一般既定に優先する。親がUnity検証・コミット・同名featureへのpushを担う。

**Goal:** 欠落した列車同期tickを待機し、乖離の始点と受信履歴をクライアントの診断ファイルに残す。
**Architecture:** 既存の連番バッファとhash gateを使い、未来hashによる飛び越しを除去する。受信位置と同期状態をTickStateへ、停止判定をhash gateへ集約する。診断クラスは履歴とJSON保存を担当する。確定停止後のpayload保持を打ち切り、サーバーの採番と他系統の更新は既存経路を使う。
**Tech Stack:** C#, Unity, VContainer, Newtonsoft.Json, NUnit, uloop。

**実装・検証状況:** 実装は完了。本体コードの最終検証は `f679e509a` 時点で、非表示のUnity batchmodeによるコンパイル成功・既存テスト12件成功を確認した。成功件数は各コミット時点のテスト構成に対する記録である。`333bc2887` では `TrainFullSnapshotFailurePropagationTest` と `TrainUnitFutureMessageBufferTest` および対応metaが削除され、本体コードは同じである。

## Requirements

- R1: 期待tick/連番が欠けたとき、後続hash/eventがあっても列車・レールの順序付き処理は進まない。数千回の再評価でも飛び越さない。
- R2: 通常の到着待ちでは、必要なメッセージが届けば順番に適用する。後続受信で欠番が確定した場合は永久待機する。hash不一致だけの待機も、実受信の乖離が200tickに達したら永久待機する。
- R3: UI等のフレーム更新は継続する。終了・全体pause・再同期要求を追加しない。
- R4: 最初の待機日時、期待tick/連番、直前の適用位置、最新受信tick、検知時と保存時の乖離、hash比較値、初回待機直前と保存直前の受信履歴を保存する。
- R5: 既存のtickループ内のhash gateで、後続連番を受信済みなのに期待IDが実バッファにないと検知した場合、1seqの欠番でもその場で保存する。後続がまだ来ない間は待機位置と日時を保持する。hash不一致だけの場合は受信済み最新tickと適用tickの差が200以上で保存する。
- R6: 同じ未解消の待機では保存・警告を連発しない。短い正常待ちでは診断ファイルを作らない。停止状態は診断の保存成否に依存しない。
- R7: 履歴は256件を上限とし、初回待機の記録は別に保持する。永久待機時にevent/hash本体を解放し、以後の受信payloadを蓄積しない。保存失敗は理由をエラーログに残し、他の処理へ例外を漏らさない。
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
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/TrainUnitTickState.cs` — 既存の最大受信位置をtickとseqの統合IDで保持し、初期化と永久待機の状態を管理する。
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainUnitFutureMessageBuffer.cs` — 受信・適用位置の診断通知。並び順・古いメッセージ破棄の意味を維持。
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainFullSnapshotEventNetworkHandler.cs` — 初回成功後の診断有効化。
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs` — 診断と保存先をDIへ登録。
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/Diagnostics/TrainSynchronizationDiagnostics.cs` — 待機エピソード・履歴・診断保存の所有者。
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/Diagnostics/TrainSynchronizationDiagnosticReport.cs` — 永続診断のスナップショットと受信履歴データ。
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/Diagnostics/TrainSynchronizationDiagnosticWriter.cs` — JSONローカル保存のディスク境界。

**Interfaces:** 既存gate `bool CanAdvanceTick(ulong expectedId)` とバッファAPIを維持。診断の公開メソッドは実際の呼び出し元を持つ通知に限定し、次を基本形にする（データ引数の型はreportへまとめてよい）。

```csharp
// 受信時、適用時、検証時に呼ぶ。初期化状態はTickStateが所有する。
internal void RecordReceived(string kind, uint tick, uint sequenceId);
internal void RecordApplied(ulong appliedId);
internal void RecordMissingOrderedMessage(ulong expectedId, bool confirmedGap);
internal void RecordHashMismatch(ulong expectedId, uint localTrainHash, uint serverTrainHash, uint localRailHash, uint serverRailHash, bool permanentWait);
```

診断は `TrainUnitTickState` と具体のwriterを受け取る。writerはDIが渡す `Application.persistentDataPath/Logs/TrainSynchronization` を使う。UTC日時の取得は診断境界に揃える。

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
- [x] 停止条件はgateへ集約する。欠番は後続IDとの比較、hash不一致は200tick差で判定し、診断へ通知する。通常待機の解消は適用通知で記録する。
- [x] writerは一意な名前のJSONを保存し、成功パスをログへ出す。IO/権限エラーのみ外部境界で捕捉し、失敗理由をエラーログに残す。ファイル書き込み例外でUIや他の更新を止めない。同一エピソードの書き込み試行を毎フレーム繰り返さない。

履歴は到着履歴として、staleによる破棄や同じIDの上書きより前に記録する。欠落時のserver hashは不明であり0やdummyを実測値として記録しない。missing通知とmismatch通知を別メソッドにして、存在しないhash値を要求しないAPIにする。

- [x] `f679e509a` の本体コードについて、非表示のUnity batchmodeでコンパイル成功（終了コード0、C#コンパイルエラー0件）を確認した。当時存在した `TrainUnitTickStateTest`・`TrainUnitFutureMessageBufferTest`・`TrainFullSnapshotFailurePropagationTest` の12件すべてが成功した。
- [x] branch・HEAD・対象ファイル一覧を確認し、検証済みの作業をfeatureへコミットする。Unityが更新した外部revision/pin差分は復元する。

## 検証履歴とテスト構成

- `1ed10e58b`: 当時の構成でコンパイルエラー0件、対象テスト35/35成功。PlayMode遷移時のCLI接続断後、Unity保存XMLで全件完了を確認した。
- `e9b2ff1c3`: このタスクで追加したテスト3クラス・共通補助ファイルと対応metaを削除し、非表示のUnity batchmodeでコンパイル成功を確認した。
- `f679e509a`: 受信位置の統合と不要状態の削除を反映し、コンパイル成功・既存テスト12/12成功を確認した。
- `333bc2887`: `TrainFullSnapshotFailurePropagationTest` と `TrainUnitFutureMessageBufferTest` および対応metaを削除した。本体コードは `f679e509a` と同一である。

## Closing tasks

- [x] 永久待機中の保持量と同期状態の責務に関する修正を反映した。2026-09-28のユーザー指定「レビューは私が指示します」に従い、追加レビューはユーザーの指示時に実行する。
- [x] ユーザー指定の既存featureへ明示refspecでpushし、masterのrefと作業treeを確認する。

## 判断記録（ADR）

- [ADR 0071](../../adr/0071-train-tick-stalls-retain-client-diagnostics.md) が挙動と出所の正本。欠番確定時の即保存はユーザー裁定。hash不一致だけの待機には先の200tick裁定を維持する。
- [agent前提] gateと診断の接続は同時に検証可能な一つの実装タスクとする。時計・汎用同期基盤の新設は利益が無いため行わない。
- [agent前提] データフローは既存受信/適用→TickState/gateの待機判断→診断通知→JSON保存。新規ファイルはClient.Gameの列車Network下、DIはClient.Starterに置く。
