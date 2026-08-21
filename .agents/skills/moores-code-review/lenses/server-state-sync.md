---
paths:
  - "Server\.Protocol"
  - "Server\.Event"
  - "Client\.Network"
  - "DataStore"
  - "Datastore"
model: sonnet
---

# Lens: サーバー状態同期の3点セット（PR988由来）

## あなたの役割
cwdを読み、patchが**サーバー権威の可変状態をクライアントへ同期する経路**を正しく構成していないCriticalのみを返す。本プロジェクトの唯一の正規経路は「イベントパケット＋初期データ＋クライアント購読」の3点セットである。

## 検査対象の絞り込み
起動prompt 2行目 `Patch path` をReadする。patchが次のいずれかを含む場合のみ本検査を行う（含まなければ `Critical: なし` で即返す）:
- サーバー側の新規可変状態（DataStore・プレイヤー横断の動的データ）の追加
- クライアント側での サーバー状態の受信・反映コードの追加

## Critical判定基準

### 1. 3点セットの欠落
サーバー側に新しい可変状態が追加されたら、次の3点が**すべて**揃っているか確認する:
1. **イベントパケット** — `Server.Event/EventReceive/*EventPacket.cs` がDataStoreの `IObservable` を購読し `va:event:*` で配信。DI登録（`MoorestechServerDIContainerGenerator`）とeager init込み。
2. **初期データ** — ログイン/再接続時に全量復元できるよう `InitialHandshakeProtocol` のResponseに同梱、または `va:get*` プロトコルで全量取得。
3. **クライアント購読** — `SubscribeEventResponse(XxxEventPacket.EventTag, ...)` する `*EventHandler`（`IInitializable`/`RegisterEntryPoint`）がクライアント側DataStoreへ反映。

**前例（この形に合わせる）**: `UnlockedEventPacket` + `GetGameUnlockStateProtocol` + `ClientGameUnlockStateDatastore`、および `ItemStackLevelUnlockEventPacket` + `InitialHandshakeProtocol.ItemStackLevels` + `ItemStackLevelEventHandler`。

### 2. 間接導出Applierの禁止
他プロトコル（研究完了・チャレンジ等）の応答をパースして**別の状態を間接的に導出する** `*Applier` / ハンドラ追随コードはCritical。クライアントが「別のイベントに追従して状態を推測する」構造は、新イベント追加のたびに追従漏れを生む。→ 専用イベントパケットの新設へ。

### 3. 初期適用の順序
`VanillaApiWithResponse.InitialHandShake` で、状態の適用が**それに依存するデータ（インベントリ等）の取得より後**になっていないか。前例: アイテムスタックレベルはインベントリ取得より先に適用（`VanillaApiWithResponse` L46-51相当）。

### 4. 決定論チェックとの照合
起動側が `candidates.event_tag_sync`（新規EventTagの購読漏れ候補）を渡している場合、各候補についてcwdをGrepして購読の実在を裏取りし、真の漏れだけをCriticalに載せる。

## Criticalにしないもの（過検知ガード）
- クライアント要求起点のRequest-Response型（状態変化通知ではないもの）。
- サーバー内部でしか使わない状態（クライアントに見えない）— 3点セット不要。
- 既存イベントの**ペイロード拡張**（新規状態ではなくフィールド追加）— 初期データ側の対応漏れだけ確認する。
- 設計書・ユーザーが明示的に「ポーリング取得のみ」と合意している場合（User promptの4カテゴリで確認）。

## 依頼動詞優先ガード
起動prompt 3行目 `User prompt` をRead。「許容するトレードオフ」「非目標」に合致する指摘は**破棄せず**、`suppressed-by: <トレードオフ1行, 出所ラベル>` を付けて**重大度そのまま**で返す（統合側が報告の「免責で消された指摘」節に載せる）。suppressed化できるのは出所が `[ユーザー裁定: ...]` / `[ADR: ...]` の行だけ。`[agent前提]` またはラベル無しの行は免責事由にならない（通常のCritical/Warningとして返す）。

## 出力フォーマット
Criticalが1件でもあれば `Critical: あり`、0件なら `Critical: なし`。
続けて `修正方針:` に `- <ファイル:行>: <3点セットのどれが欠けていて、どの前例の形で足すか>` を1行ずつ列挙する。

## 既存多数派パターンの扱い（ユーザー裁定 2026-08-14）

payload の `PlayerId` を無検証で信じるプロトコルは `PacketResponse/` 配下で多数派（実測32件）であり、`PacketResponseContext.PlayerId` を使う実装と混在している。**新設プロトコルがこの形を踏襲しただけの場合は Warning 止まり**とし、Critical・設計判断に上げない（リポジトリ全体の一括是正案件であって機能PRのスコープではない）。Criticalに上げてよいのは、本ブランチが**新たに**なりすまし経路を作った場合（例: 従来 context 由来だった同定を payload 由来へ変更した）に限る。参照: [[2026-08-14-プロトコルのplayerId自己申告は既存多数派として放置する]]
