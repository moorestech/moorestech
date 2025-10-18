# challengeAction「giveItem」追加方針

## 背景
- チャレンジや研究の進行に応じて直接アイテムを付与できるアクションが存在せず、外部コマンド（`sendCommand give ...`）などに頼っている。
- `challengeAction` スキーマはチャレンジ／研究双方で共有されているため、スキーマ拡張とゲーム実装の両面で整理された計画が必要。

## YAML編集方針
- 編集対象: `VanillaSchema/ref/challengeAction.yml`
- 追記内容:
  - `challengeActionType` の `options` に `giveItem` を追加。
  - `challengeActionParam` の `cases` に `giveItem` 用ケースを追加。
- パラメータ構造は既存の `consumeItems` などに揃えて「付与アイテムの配列」を持つ。
- 非同期ロードや外部入力ではないため、null チェックは最低限に留め、SourceGenerator による型生成を前提とする。
- 追記予定スニペット（概略）:
  ```yaml
    - when: giveItem
      type: object
      isDefaultOpen: true
      properties:
      - key: rewardItems
        type: array
        overrideCodeGeneratePropertyName: GiveItemRewardElement
        items:
          type: object
          properties:
          - key: itemGuid
            type: uuid
            foreignKey:
              schemaId: items
              foreignKeyIdPath: /data/[*]/itemGuid
              displayElementPath: /data/[*]/name
          - key: itemCount
            type: integer
            default: 1
      - key: deliveryTarget
        type: enum
        default: allPlayers
        options:
        - allPlayers
        - actionInvoker
  ```
- `deliveryTarget` は配布先の明示に使う。現状は `allPlayers`（チャレンジ報酬想定）と `actionInvoker`（研究完了プレイヤー想定）の 2 種を用意し、追加要件が出た場合に enum を拡張する。

## 実装方針

### 概要
- スキーマ拡張によって生成される `GiveItemChallengeActionParam` や `GiveItemRewardElement` を利用し、サーバーでアイテム付与ロジックを実装。
- クライアント UI は新しいアクションをリスト／ツールチップに表示できるよう対応する。
- 既存ロジックに try-catch は追加しない。必要な判定は条件分岐で行う。

### サーバー側（`docs/ServerGuide.md` を参照しながら実装）
1. `Game.Action` モジュール
   - `IGameActionExecutor` にコンテキストを渡す手段を追加（例: `ExecuteActions(ChallengeActionElement[] actions, ActionExecutionContext context)`）。従来呼び出しには `ActionExecutionContext.Default` を与え、順次置き換え。
   - `GameActionExecutor` に `IPlayerInventoryDataStore` と `IItemStackFactory` を注入。
   - `giveItem` ケースをスイッチに追加し、`deliveryTarget` に応じて付与先プレイヤー集合を決定。
     - `allPlayers`: `_playerInventoryDataStore.GetAllPlayerId()` を列挙。
     - `actionInvoker`: `ActionExecutionContext` が保持する `PlayerId` を参照。未指定の場合はエラーログを出して処理スキップ（デバッグしやすくするため）。
   - 付与処理は `rewardItems` を回しつつ `ServerContext.ItemStackFactory.Create` でスタックを生成し、対象インベントリの `MainOpenableInventory.InsertItem` を使用。
   - 既存のアンロック専用処理 (`ExecuteUnlockActions`) からも新しいシグネチャを呼ぶよう統一する。
2. 呼び出し側のコンテキスト整理
   - 研究: `ResearchDataStore.CompleteResearch` から `ExecuteActions` を呼ぶ際に `playerId` を渡す。
   - チャレンジ: 現状全プレイヤーを対象にする設計で十分と判断。`ActionExecutionContext.DefaultAllPlayers` を活用し、必要なら `ChallengeDatastore` で明示的に渡す。
   - セーブデータ読み込み (`LoadChallenge` / `ResearchDataStore.Load`) からの再実行も同じ経路を通す。
3. イベント通知
   - 必要に応じて（UI リアルタイム更新が必要であれば）`MainInventoryUpdateEvent` が発火するよう既存の仕組みを確認。既存 `InsertItem` がイベントを飛ばす設計であれば追加実装不要。

### クライアント側（`docs/ClientGuide.md` を参照）
1. チャレンジ UI (`Client.Game.InGame.UI.Challenge.ChallengeListUIElement`)
   - `giveItem` を解釈し、ツールチップ文言に「アイテム支給: {名前} x{数}」形式で追記。
   - `deliveryTarget` を考慮し、全プレイヤー向けかどうかを表示文で補足する（例: `(全員)` / `(完了者のみ)`）。
2. 研究 UI (`Client.Game.InGame.UI.Inventory.Block.Research.ResearchTreeElement`)
   - 既存 unlock アイコン生成ロジックを共通化し、`giveItem` の `rewardItems` もアイコン表示できるよう調整。
3. 他 UI/ツールチップ
   - 研究完了時のポップアップや通知が存在する場合は追加の文言が必要か確認する。

### 共通／ユーティリティ
- `ActionExecutionContext`（仮称）や `DeliveryTarget` の enum は `Game.Action` 名前空間で定義する。
- ログは `DebugLogger` 系にまとめ、付与失敗時のアイテム GUID・プレイヤー ID を出力してデバッグしやすくする。

## テスト方針
- 単体テスト
  - `GameActionExecutor` に対する新ケースのテストを追加し、`allPlayers` / `actionInvoker` で適切にインベントリが更新されることを検証。
  - 異常系: `actionInvoker` 指定で `PlayerId` が渡らない場合に処理がスキップされることの確認。
- 結合テスト
  - 研究完了 (`CompleteResearchProtocolTest`) に `giveItem` を含むケースを追加し、完了者インベントリだけが増えることを確認。
  - チャレンジ完了フローでも `giveItem` を含むマスターデータを用意して検証。
- UI テスト（必要に応じて）
  - ツールチップ文言が生成されることを PlayMode テストで確認。

## 未決事項 / 要確認
- マルチプレイ時の配布仕様（全配布で良いか、完了者のみか）を企画側に確認する。
- `giveItem` を `StartedActions` で使うケースを受け入れるか（現状は許容するが、テストケースを追加しておく）。
- 生成コードの命名（`GiveItemRewardElement` 等）がチームの命名規則と合致しているかを SourceGenerator の出力で最終確認する。

## 次ステップ
- 上記方針への合意を得た後、`challengeAction.yml` の編集から着手し、SourceGenerator の出力確認・実装・テストの順で進める。
