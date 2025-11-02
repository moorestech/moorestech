# 実装タスク

## 1. マスターデータスキーマの拡張とビルド検証

- [ ] 1.1 train.ymlスキーマにaddressablePathプロパティを追加
  - VanillaSchema/train.ymlを開き、trainUnitsのitemsプロパティ配列にaddressablePathフィールドを追加
  - 型をstring、optionalをtrue、defaultを空文字列に設定
  - YAMLスキーマの文法が正しいことを確認
  - _要件: 1.1_

- [ ] 1.2 SourceGeneratorによる自動生成とコンパイル検証
  - サーバープロジェクト（moorestech_server）でビルドを実行
  - Mooresmaster.Model.TrainModuleにTrainUnitMasterElementクラスが生成されることを確認
  - AddressablePathプロパティ（string型）が存在することを確認
  - コンパイルエラーがないことを確認
  - _要件: 1.2_

- [ ] 1.3 テスト用マスターデータにaddressablePathを追加
  - Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/train.jsonを編集
  - trainUnitsの既存エントリーにaddressablePathキーを追加（テスト用パス: "Assets/TestTrainPrefab"）
  - addressablePathが空のエントリーも1つ追加（フォールバックテスト用）
  - JSONスキーマ検証が通ることを確認
  - _要件: 7.1_

## 2. サーバー側：列車エンティティアダプターの実装

- [ ] 2.1 VanillaEntityType定数にVanillaTrainを追加
  - VanillaEntityTypeクラスに"VanillaTrain"定数を追加
  - 命名規則が他のエンティティタイプ（VanillaItem, VanillaPlayer）と一貫していることを確認
  - _要件: 2.2_

- [ ] 2.2 TrainEntityクラスの基本構造を実装
  - Game.Entity.Interface.EntityInstance名前空間にTrainEntityクラスを作成
  - IEntityインターフェースを実装
  - TrainUnitへの参照をprivateフィールドとして保持
  - コンストラクタでEntityInstanceIdとTrainUnitを受け取る設計
  - _要件: 2.1_

- [ ] 2.3 TrainEntity.InstanceIdプロパティを実装
  - TrainUnit.TrainId（Guid）からEntityInstanceId（long）を生成するロジックを実装
  - GuidのGetHashCode()を使用してlong値を生成
  - 同じTrainIdに対して常に同じInstanceIdが生成されることを保証
  - _要件: 2.1_

- [ ] 2.4 TrainEntity.Positionプロパティを実装（RailPositionからVector3計算）
  - RailPosition.GetNodeApproaching()とGetNodeJustPassed()から2つのRailNodeを取得
  - RailNode.GetDistanceToNode()で総距離を計算
  - DistanceToNextNodeから進行度（t = 1.0f - distanceToNext/totalDistance）を計算
  - Vector3.Lerpで補間してVector3座標を返す
  - RailNodeがnullの場合はVector3.zeroを返す（エラーハンドリング）
  - _要件: 2.4_

- [ ] 2.5 TrainEntity.StateプロパティとSetPosition()を実装
  - StateプロパティはTrainUnit.TrainId.ToString()を返す
  - SetPosition()メソッドは空実装（列車位置はRailPositionで管理）
  - 日本語・英語のコメントでSetPosition()が使用されない理由を記載
  - _要件: 2.3_

## 3. サーバー側：エンティティファクトリーとプロトコル統合

- [ ] 3.1 EntityFactoryにVanillaTrainハンドリングを追加
  - EntityFactory.CreateEntity()メソッドにVanillaTrainのif分岐を追加
  - TrainEntityインスタンスを生成して返却
  - TrainUnitの取得方法を決定（引数で受け取る、またはInstanceIdから検索）
  - 既存のVanillaItem、VanillaPlayer処理に影響がないことを確認
  - _要件: 2.5_

- [ ] 3.2 RequestWorldDataProtocolで列車収集ロジックを実装
  - RequestWorldDataProtocol.GetResponse()メソッドを拡張
  - TrainUpdateService.Instance.GetRegisteredTrains()で全TrainUnitを取得
  - 各TrainUnitに対してTrainEntityを生成
  - EntityMessagePackに変換してentitiesリストに追加
  - 既存のベルトコンベアアイテム収集ロジックと並列で動作することを確認
  - _要件: 3.1, 3.2, 3.3_

- [ ] 3.3 サーバー側ユニットテスト：TrainEntity Position計算
  - TrainEntityのPosition計算が正確か検証するテストを作成
  - 既知のRailPosition（2ノード間、距離50、DistanceToNext=20）でPositionを計算
  - 期待されるVector3座標（Linear補間結果）と一致することを確認
  - RailNodeがnullの場合にVector3.zeroが返ることを確認
  - _要件: 2.4_

- [ ] 3.4 サーバー側ユニットテスト：TrainEntity State文字列とInstanceId
  - TrainEntity.StateがGuid.ToString()形式であることを確認
  - Guid.TryParseでパース可能な文字列であることを検証
  - 同じTrainIdに対してInstanceIdが一意かつ一貫していることを確認
  - _要件: 2.3_

- [ ] 3.5 統合テスト：RequestWorldDataProtocolに列車が含まれる
  - サーバーでTrainUnitを1つ作成
  - RequestWorldDataProtocol.GetResponse()を呼び出し
  - ResponseWorldDataMessagePackのEntities配列に列車エンティティが含まれることを確認
  - EntityMessagePackのType、InstanceId、State、Positionが正しく設定されていることを検証
  - _要件: 3.2, 3.3, 3.4_

## 4. クライアント側：列車エンティティビジュアル表示の実装

- [ ] 4.1 TrainEntityObjectクラスの基本構造を実装
  - Client.Game.InGame.Entity名前空間にTrainEntityObjectクラスを作成
  - MonoBehaviourとIEntityObjectを継承
  - EntityIdプロパティ、_previousPosition、_targetPosition、_linerTimeフィールドを定義
  - Initialize()、SetDirectPosition()、SetInterpolationPosition()、Destroy()メソッドのスタブを作成
  - _要件: 4.1_

- [ ] 4.2 TrainEntityObject.Initialize()でState文字列をパース
  - EntityResponseのState文字列からGuid.TryParseでTrainIdを抽出
  - パース成功時はTrainIdをprivateフィールドに保存
  - パース失敗時はDebug.LogErrorを出力（try-catch禁止のため、TryParseで対応）
  - EntityIdプロパティを設定
  - _要件: 4.2_

- [ ] 4.3 TrainEntityObject位置補間ロジックを実装
  - SetDirectPosition()で即座に位置を設定（_previousPosition、_targetPosition、transform.positionを同じ値に）
  - SetInterpolationPosition()で補間開始（_previousPosition=現在位置、_targetPosition=新位置、_linerTime=0）
  - Update()でLinear補間（rate = _linerTime / NetworkConst.UpdateIntervalSeconds、Mathf.Clamp01で0-1に制限）
  - transform.positionをVector3.Lerpで更新
  - _要件: 4.4, 4.5_

- [ ] 4.4 TrainEntityObject.Destroy()メソッドを実装
  - Destroy(gameObject)を呼び出してGameObjectを破棄
  - リソースリークがないことを確認
  - _要件: 4.1_

## 5. クライアント側：Prefab動的ロードとフォールバック

- [ ] 5.1 デフォルト列車Prefabの作成と設定
  - moorestech_client/Assets/AddressableResources/に白色キューブのPrefabを作成（DefaultTrainPrefab）
  - Inspectorでマテリアルを白色に設定
  - EntityObjectDatastoreにSerializeFieldでdefaultTrainPrefabを追加
  - InspectorでDefaultTrainPrefabを設定
  - _要件: 8.1_

- [ ] 5.2 テスト用列車PrefabをAddressablesに登録
  - moorestech_client/Assets/AddressableResources/にテスト用列車Prefab（TestTrainPrefab）を作成
  - 簡易的な形状（直方体など）でテストしやすいビジュアルに設定
  - AddressablesグループにTestTrainPrefabを追加
  - アドレスをtrain.jsonのaddressablePathと一致させる（"Assets/TestTrainPrefab"）
  - _要件: 7.2_

- [ ] 5.3 EntityObjectDatastore.CreateEntity()でVanillaTrainハンドリングを実装
  - CreateEntity()メソッドにVanillaTrainのif分岐を追加
  - State文字列からGuid.TryParseでTrainIdを抽出
  - パース失敗時はDebug.LogError + CreateFallbackTrainEntity()呼び出し
  - MasterHolder.TrainMasterでTrainIdからマスターデータを検索
  - _要件: 5.1, 5.2_

- [ ] 5.4 Addressables Prefabロードとフォールバック処理
  - マスターデータ取得成功時、addressablePathが空でないか確認
  - addressablePathが有効ならAddressables.InstantiateAsync()で非同期ロード
  - AsyncOperationHandle.StatusがSucceededでない場合はDebug.LogError + フォールバック
  - ロード成功時はPrefabにTrainEntityObjectコンポーネントをアタッチ
  - addressablePath空、マスターデータ未存在、ロード失敗の全てでCreateFallbackTrainEntity()を使用
  - _要件: 5.3, 5.4, 5.5, 8.1, 8.2, 8.4_

- [ ] 5.5 CreateFallbackTrainEntity()ヘルパーメソッドを実装
  - defaultTrainPrefabをInstantiateしてTrainEntityObjectコンポーネントをアタッチ
  - 位置、親transformを適切に設定
  - IEntityObjectを返却
  - _要件: 8.1_

- [ ] 5.6 クライアント側ユニットテスト：State文字列パース
  - 有効なGuid文字列でGuid.TryParseが成功することを確認
  - 無効な文字列（"invalid-guid"）でパース失敗を確認
  - パース失敗時にエラーログが出力されることを検証（LogAssertを使用）
  - _要件: 8.3_

- [ ] 5.7 クライアント側ユニットテスト：マスターデータ検索
  - テスト用のTrainIdでMasterHolder.TrainMasterから検索成功を確認
  - 存在しないTrainIdで検索失敗を確認
  - addressablePathが空文字列の場合の挙動を検証
  - _要件: 5.2_

## 6. エンティティライフサイクル統合とPlayModeテスト

- [ ] 6.1 サーバー側：TrainUnit作成時のエンティティ登録
  - TrainUnit作成時にTrainEntityを生成してEntitiesDatastoreに登録する処理を実装
  - TrainUnit.OnDestroy()時にEntitiesDatastoreから削除する処理を実装
  - TrainUpdateServiceとの統合を確認
  - _要件: 6.1, 6.5_

- [ ] 6.2 クライアント側：EntityObjectDatastore.OnEntitiesUpdate()での列車処理
  - OnEntitiesUpdate()で列車エンティティを受信した際の動作を確認
  - 新規列車エンティティでCreateEntity()が呼ばれPrefabがインスタンス化されることを検証
  - 既存列車エンティティでSetInterpolationPosition()が呼ばれることを検証
  - 1秒間未更新の列車が自動削除されることを確認（既存のUpdate()タイムアウトロジック）
  - _要件: 6.3, 6.4_

- [ ] 6.3 PlayModeテスト：列車配置から表示までのE2Eフロー
  - サーバーを起動してTrainUnitを1つ作成
  - クライアントを起動してサーバーに接続
  - RequestWorldDataProtocol経由で列車エンティティを受信
  - EntityObjectDatastoreでTrainEntityObjectが生成されることを確認
  - GameObjectがシーンに表示されることを目視確認
  - _要件: 6.3, 7.4_

- [ ] 6.4 PlayModeテスト：列車移動時のスムーズな表示
  - サーバーで列車をレール上で移動させる（TrainUnit.Update()）
  - クライアント側で500ms間隔の同期を待つ
  - TrainEntityObject.Update()でLinear補間が動作することを確認
  - transform.positionが滑らかに更新されることを目視確認
  - _要件: 6.2, 4.4, 4.5_

- [ ] 6.5 PlayModeテスト：列車削除時のクライアント側削除
  - サーバーでTrainUnit.OnDestroy()を呼び出す
  - クライアント側で1秒後にGameObjectが自動削除されることを確認
  - メモリリークがないことを確認（UnityProfiler使用）
  - _要件: 6.4, 6.5_

## 7. Addressablesロード成功・失敗ケースのテスト

- [ ] 7.1 PlayModeテスト：有効なaddressablePathでPrefabロード成功
  - train.jsonにaddressablePath="Assets/TestTrainPrefab"を持つエントリーを作成
  - サーバーでそのTrainUnitを作成
  - クライアント側でAddressables.InstantiateAsync()が成功することを確認
  - TestTrainPrefabがインスタンス化されることを目視確認
  - _要件: 5.3, 5.4_

- [ ] 7.2 PlayModeテスト：無効なaddressablePathでフォールバック
  - train.jsonにaddressablePath="InvalidPath/DoesNotExist"を持つエントリーを作成
  - サーバーでそのTrainUnitを作成
  - クライアント側でAddressablesロード失敗を検出
  - Debug.LogErrorが出力されることを確認（LogAssertで検証）
  - デフォルトキューブPrefabが表示されることを目視確認
  - _要件: 5.5, 8.1_

- [ ] 7.3 PlayModeテスト：addressablePath空文字列でフォールバック
  - train.jsonにaddressablePath=""を持つエントリーを作成
  - サーバーでそのTrainUnitを作成
  - クライアント側でデフォルトキューブPrefabが即座に使用されることを確認
  - Addressables.InstantiateAsync()が呼ばれないことを確認
  - _要件: 5.5, 8.1_

- [ ] 7.4 PlayModeテスト：マスターデータ未存在でフォールバック
  - サーバーで存在しないTrainIdを持つ偽のTrainEntityを送信（テスト用モック）
  - クライアント側でMasterHolder.TrainMaster検索失敗を確認
  - Debug.LogWarningが出力されることを確認
  - デフォルトキューブPrefabが表示されることを確認
  - _要件: 8.2_

## 8. 統合テストとパフォーマンス検証

- [ ] 8.1 統合テスト：複数列車の同時表示
  - サーバーで5つのTrainUnitを異なる位置に作成
  - クライアント側で5つのTrainEntityObjectが生成されることを確認
  - InstanceIdが全て異なることを確認（衝突なし）
  - 全ての列車が正しい位置に表示されることを目視確認
  - _要件: 6.3_

- [ ] 8.2 統合テスト：列車エンティティのシリアライズ・デシリアライズ
  - サーバーでTrainEntityをEntityMessagePackに変換
  - MessagePackシリアライズ・デシリアライズを実行
  - クライアント側でEntityResponseに正しく変換されることを確認
  - InstanceId、Type、State、Positionが全て保持されることを検証
  - _要件: 7.3_

- [ ] 8.3 パフォーマンステスト：100両の列車でのフレームレート
  - サーバーで100個のTrainUnitを作成
  - クライアント側で100個のTrainEntityObjectをUpdate()
  - フレームレートが60fps以上を維持することを確認（Unity Profiler使用）
  - TrainEntityObject.Update()の合計CPU時間が5ms以下であることを確認
  - _要件: パフォーマンス要件_

- [ ] 8.4 パフォーマンステスト：RequestWorldDataProtocol応答時間
  - サーバーで100個のTrainUnitを作成
  - RequestWorldDataProtocol.GetResponse()の実行時間を計測
  - 100ms以内に応答することを確認
  - EntityMessagePack変換のオーバーヘッドが許容範囲内であることを確認
  - _要件: パフォーマンス要件_

## 9. コード品質とドキュメント

- [ ] 9.1 コード可読性の向上
  - TrainEntity、TrainEntityObjectの主要メソッドに日本語・英語の2行コメントを追加
  - 複雑なロジック（Position計算、Prefabロード）に#regionとローカル関数を適用
  - usingディレクティブを整理（System、Unity、サードパーティ、プロジェクト内の順）
  - _要件: コード規約_

- [ ] 9.2 nullチェックと条件分岐の最適化
  - RailNode、TrainUnit、マスターデータのnullチェックを必要最小限に
  - try-catchを使用せず、条件分岐でエラーハンドリング
  - Guid.TryParse、AsyncOperationHandle.Statusチェックで安全性確保
  - _要件: 8.4, コード規約_

- [ ] 9.3 全コンパイルエラーの解消
  - サーバープロジェクト（moorestech_server）でコンパイルエラーゼロを確認
  - クライアントプロジェクト（moorestech_client）でコンパイルエラーゼロを確認
  - 警告も可能な限り解消
  - _要件: 全要件_

- [ ] 9.4 全テストの実行と成功確認
  - サーバー側ユニットテスト（3.3、3.4、3.5）を実行して全て成功
  - クライアント側ユニットテスト（5.6、5.7）を実行して全て成功
  - PlayModeテスト（6.3-6.5、7.1-7.4、8.1、8.2）を実行して全て成功
  - パフォーマンステスト（8.3、8.4）を実行して目標メトリクス達成
  - _要件: 全要件_
