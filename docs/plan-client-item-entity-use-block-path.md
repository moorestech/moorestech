# プラン: ベルトコンベア上アイテムエンティティを「各ブロックのパス」で移動させる（確定版）

## 概要
クライアント側のベルトコンベア上アイテムエンティティ（`VanillaEntityType.VanillaItem`）の位置決定を、直線補間ではなく、各ブロックPrefabに設定された `BeltConveyorItemPath`（Bezier）を使って行うように変更します。

## 背景・現状（調査結果）
- サーバーは `RequestWorldDataProtocol` 内で `CollectBeltConveyorItems` を使い、ベルト上のアイテムを `BeltConveyorItemEntity` に変換して `WorldDataResponse.Entities` として返している
- サーバー側の現在の位置計算は「進捗（RemainingPercent）→方角別の直線座標」に固定されている（ブロック固有の曲線パス情報を使っていない）
- クライアントは `EntityObjectDatastore` → `BeltConveyorItemEntityObjectFactory` で `VanillaItem` を生成し、現状は受信した `EntityResponse.Position` を単純Lerpで追従している
- クライアント側には `BeltConveyorItemPath` コンポーネントが存在し、`startId`/`goalId` と `remainingPercent` からBezier上のワールド座標を取得できる
- ただし現状、クライアントは `BeltConveyorItemPath` を参照しておらず、また `EntityData` に `remainingPercent` や「どのブロック上のアイテムか」を特定する情報が含まれていない

## 方針（XY問題の回避）
「クライアントで各ブロックのパスを使う」ためには、曲線上のパラメータ（`remainingPercent`）と、参照すべきブロック（少なくとも`Vector3Int`のブロック座標）が必要です。
現状の `EntityResponse.Position` だけでは、曲線パスに対して逆算（位置→t）を行う必要が出て不安定/複雑になるため、サーバーから必要な情報を明示的に送る形にします。

## 要件確定（ユーザー確認済み）
- 対象: `CollectBeltConveyorItems` 経由で収集される `VanillaItem` を全て対象にする（ベルトコンベア/アイテムシューター等）
- `BeltConveyorItemPathData.StartId/GoalId` の運用: `ConnectorGuid.ToString()` の文字列と一致させる
- `EntityData` 拡張: サーバーから `RemainingPercent` と「対象ブロック座標」を追加して送る（クライアント側で逆算はしない）

## 変更対象ファイル（予定）
- `moorestech_server/Assets/Scripts/Game.Entity.Interface/EntityStateMessagePack.cs`
  - `BeltConveyorItemEntityStateMessagePack` に `RemainingPercent` と `BlockPosition`（座標）を追加する
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/CollectBeltConveyorItems.cs`
  - `BeltConveyorItemEntity` 作成時に、追加した `RemainingPercent` と `BlockPosition` をセットする
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/Factory/BeltConveyorItemEntityObjectFactory.cs`
  - `EntityData` の追加フィールドをデシリアライズし、表示オブジェクトへ渡す
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/Object/BeltConveyorItemEntityObject.cs`
  - 直線Lerpではなく「該当ブロックの `BeltConveyorItemPath` から取得した座標」に追従するようにする（必要なら `CustomModelBeltConveyorItemEntityObject` も同様）
- （必要なら）`moorestech_client/Assets/Scripts/Client.Game/InGame/Context/ClientDIContext.cs`
  - 参照経路が足りない場合に `BlockGameObjectDataStore` へアクセスできるように使う（現状すでに `ClientDIContext.BlockGameObjectDataStore` がある）

## 設計案
### エンティティデータ拡張（推奨）
- `BeltConveyorItemEntityStateMessagePack` に追加:
  - `RemainingPercent`（`float`、0.0→1.0の範囲。サーバーの「残り」ではなく、ベルト上の進捗率として送る）
  - `BlockPosX/Y/Z`（`int`、ベルトブロックの原点座標。クライアントで対象ブロックを特定するために使用）
  - `SourceConnectorGuid`/`GoalConnectorGuid` は既存のまま使用（`BeltConveyorItemPath` の `startId`/`goalId` は `Guid.ToString()` で一致させる）

#### MessagePack Key について
既存 `BeltConveyorItemEntityStateMessagePack` は `[Key(0..3)]` を使用しているため、追加フィールドは必ず `[Key(4..)]` として末尾に追加します。

### クライアントの位置決定
- `EntityResponse` 更新ごとに:
  - `entity.EntityData` から `BlockPos`/`RemainingPercent`/`SourceConnectorGuid`/`GoalConnectorGuid` を復元
  - `ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(blockPos, out var block)` で対象ブロックを取得
  - ブロック側（または子）から `BeltConveyorItemPath` を取得し、`GetWorldPosition(startId, goalId, remainingPercent)` を使って目標座標を計算（`startId`/`goalId` は `Guid?` の場合 `?.ToString()`、null/空はデフォルトパス）
  - 既存の補間（Lerp）を「目標座標」に対して行う

## 実装ステップ
1. プロトコルデータに必要情報を追加（`EntityStateMessagePack.cs` / `CollectBeltConveyorItems.cs`）
2. クライアントで追加情報を使って目標座標を算出し、アイテムエンティティの移動更新をBezierパス準拠に変更（`BeltConveyorItemEntityObject` / 必要なら `CustomModelBeltConveyorItemEntityObject`）
3. 各ベルト系ブロックPrefabに `BeltConveyorItemPath` が付いている前提を確認し、不足があればユーザーにUnity上で付与・設定を依頼する（Unity YAMLは直接編集しない）
4. コンパイルチェックを実行する（実装フェーズで `./tools/unity-test.sh moorestech_client \"^0\"`。サーバー変更も含むため `moorestech_server` 側も関連テストを正規表現で限定して実行）

## リスク・注意点（要確認）
- どのブロックPrefabに `BeltConveyorItemPath` が付いているか（直進/カーブ/上り下り/接続系）を揃える必要がある
- `startId`/`goalId` は `ConnectorGuid.ToString()` の文字列で統一する（本プランで確定）

## 承認確認
この確定版プランで実装を開始してよいですか？
