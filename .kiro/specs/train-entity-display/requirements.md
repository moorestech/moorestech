# 要件定義書

## はじめに
本機能は、鉄道車両（TrainUnit）をゲーム内エンティティとして扱い、クライアント側で3D Prefabを用いてビジュアル表示するシステムを実現します。既存のエンティティ同期アーキテクチャ（`Game.Entity`、`Client.Game.InGame.Entity`）を拡張し、マスターデータ（train.yml）にAddressable Pathを追加することで、列車の外観をデータ駆動で管理可能にします。

### ビジネス価値
- プレイヤーが配置した列車をゲーム世界で視覚的に確認できる
- MOD制作者が独自の列車モデルを追加できる拡張性
- 既存のエンティティシステムとの統一的な設計による保守性向上

## 要件

### 要件1: マスターデータスキーマへのAddressable Path追加
**目的:** MOD制作者として、train.ymlのtrainUnitに3D Prefabのパスを指定できるようにすることで、列車の外観をカスタマイズ可能にする

#### 受け入れ基準
1. WHEN train.ymlスキーマを編集する THEN trainUnitsプロパティにaddressablePathフィールド（string型、optional）が追加される
2. WHEN SourceGeneratorがビルド時に実行される THEN TrainUnitMasterElementクラスにAddressablePathプロパティが自動生成される
3. WHERE mods/*/master/train.jsonファイルで THEN 各trainUnitエントリーにaddressablePathキーを指定できる
4. WHEN addressablePathが指定されていない THEN デフォルト値（空文字列またはnull）が使用される

### 要件2: サーバー側TrainEntityの実装
**目的:** 開発者として、TrainUnitを既存のIEntityインターフェースに適合させることで、エンティティシステムで列車を管理可能にする

#### 受け入れ基準
1. WHEN TrainEntityクラスを実装する THEN IEntityインターフェース（EntityInstanceId, EntityType, Position, State）を実装する
2. WHEN TrainEntityのEntityTypeを取得する THEN 新しいVanillaEntityType定数（例: "VanillaTrain"）を返す
3. WHEN TrainEntityのStateを取得する THEN TrainUnitのID（Guid）と向き情報を文字列形式で返す（例: "guid,isFacingForward"）
4. WHEN TrainEntityのPositionを取得する THEN TrainUnitのRailPosition.GetPosition()から計算された3D座標を返す
5. WHEN EntityFactory.CreateEntity()でentityType="VanillaTrain"を指定 THEN TrainEntityインスタンスが生成される

### 要件3: サーバー側エンティティ同期への統合
**目的:** システム管理者として、列車の位置情報を既存のエンティティ同期プロトコルで送信することで、追加のプロトコル実装を不要にする

#### 受け入れ基準
1. WHEN RequestWorldDataProtocol.GetResponse()が実行される THEN TrainUpdateService.Instance.GetAllTrains()から全TrainUnitを取得する
2. WHEN 各TrainUnitをエンティティ化する THEN EntityMessagePack形式でシリアライズされる
3. WHERE ResponseWorldDataMessagePackのEntities配列で THEN 列車エンティティが他のエンティティ（VanillaItem, VanillaPlayer）と共に含まれる
4. WHEN クライアントがWorldDataを500ms間隔でポーリング THEN 列車の最新位置が取得される

### 要件4: クライアント側TrainEntityObjectの実装
**目的:** プレイヤーとして、ゲーム世界で動く列車の3Dモデルを視覚的に確認できるようにする

#### 受け入れ基準
1. WHEN TrainEntityObjectクラスを実装する THEN IEntityObjectインターフェース（EntityId, Initialize, SetDirectPosition, SetInterpolationPosition, Destroy）を実装する
2. WHEN TrainEntityObjectが初期化される THEN EntityResponseのState文字列から列車IDと向き情報をパースする
3. WHEN 列車の向きが変更される THEN Prefabのrotationを適切に反転させる（例: 180度回転）
4. WHEN SetInterpolationPosition()が呼ばれる THEN 前回位置から新位置へのLinear補間を開始する（NetworkConst.UpdateIntervalSeconds秒かけて）
5. WHEN Update()が毎フレーム実行される THEN transform.positionが補間されたVector3に更新される

### 要件5: クライアント側Prefab動的ロード
**目的:** MOD制作者として、Addressable Pathで指定した列車Prefabがゲーム実行時に正しくロードされるようにする

#### 受け入れ基準
1. WHEN EntityObjectDatastore.CreateEntity()でentityType="VanillaTrain"を処理 THEN EntityResponseのState文字列から列車IDを抽出する
2. WHEN 列車IDからマスターデータを取得 THEN MasterHolder.TrainMasterから対応するTrainUnitMasterElementを検索する
3. IF TrainUnitMasterElement.AddressablePathが空でない THEN Addressables.InstantiateAsync()でPrefabを非同期ロードする
4. WHEN Addressablesロードが完了 THEN TrainEntityObjectコンポーネントをアタッチし、Initialize()を呼ぶ
5. IF AddressablePathが空またはロード失敗 THEN デフォルトのキューブPrefabを表示する（フォールバック）

### 要件6: エンティティライフサイクル管理
**目的:** システム管理者として、列車エンティティのスポーン・デスポーン・更新が既存システムと一貫性を持つようにする

#### 受け入れ基準
1. WHEN サーバーでTrainUnitが作成される THEN 新しいTrainEntityがEntitiesDatastoreに追加される
2. WHEN TrainUnitがRailPosition.Update()で移動 THEN TrainEntity.Positionが自動的に更新される
3. WHEN クライアントが新しい列車エンティティを受信 THEN EntityObjectDatastore._entitiesに追加され、Prefabがインスタンス化される
4. WHEN 列車エンティティが1秒間更新されない THEN クライアント側で自動的にGameObjectが破棄される（既存のタイムアウトロジック）
5. WHEN TrainUnitがサーバーで削除される（OnDestroy()） THEN EntitiesDatastoreから削除され、次回の同期でクライアント側も削除される

### 要件7: テスト用マスターデータとPrefab準備
**目的:** 開発者として、ユニットテストとPlayModeテストで列車エンティティ表示を検証できるようにする

#### 受け入れ基準
1. WHERE Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/train.jsonで THEN addressablePathを含むテスト用trainUnitデータが定義される
2. WHERE moorestech_client/Assets/AddressableResources/で THEN テスト用の列車PrefabがAddressableとして登録される
3. WHEN ユニットテストでTrainEntityを生成 THEN EntityMessagePackへのシリアライズ・デシリアライズが正常に動作する
4. WHEN PlayModeテストでTrainEntityObjectを生成 THEN Prefabロードと位置補間が正常に動作する

### 要件8: エラーハンドリングとフォールバック
**目的:** プレイヤーとして、Prefabロードエラーが発生してもゲームが継続できるようにする

#### 受け入れ基準
1. IF Addressables.InstantiateAsync()が失敗（null返却、例外） THEN デフォルトPrefab（シンプルなキューブ）をインスタンス化する
2. IF TrainUnitMasterElementがマスターデータに存在しない THEN エラーログを出力し、デフォルトPrefabを使用する
3. WHEN State文字列のパースに失敗 THEN デバッグログを出力し、そのエンティティをスキップする（Destroyしない）
4. WHERE EntityObjectDatastore.CreateEntity()で THEN try-catchを使用せず、条件分岐でエラーケースを処理する
