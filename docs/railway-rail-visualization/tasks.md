# 実装計画 - Railway Rail Visualization

- [x] 1. サーバー側データモデルとMessagePack構造体の実装
- [x] 1.1 レール接続情報のMessagePackデータ構造を定義
  - RailConnectionDataクラスを作成し、レールノード間の接続を表現
  - RailNodeInfoクラスで、レールノードの識別情報と制御点を保持
  - RailControlPointMessagePackでベジエ曲線制御点の座標情報を定義
  - RailComponentIDMessagePackでレールブロックの一意識別子を定義
  - 各クラスにMessagePackObject属性とKey属性を適切に付与
  - デシリアライズ用のObsolete属性付きコンストラクタを実装
  - RailConnectionInfo構造体を作成し、タプルの代わりに使用
  - MessagePackクラスのコンストラクタで変換処理を共通化
  - _Requirements: 1.2, 1.3, 1.5_

- [x] 1.2 RailGraphDatastoreからレール接続情報を取得するヘルパー機能を実装
  - RailGraphDatastoreから全RailNodeの接続情報を取得する処理
  - 各RailNodeの接続先、距離、制御点情報を収集
  - ConnectionDestinationからRailComponentID情報への変換処理
  - 無効なRailNodeや接続情報のスキップ処理
  - 空のレールグラフの場合は空配列を返す処理
  - RailConnectionInfo構造体を返すように変更
  - _Requirements: 1.1, 1.4_

- [x] 2. GetRailConnectionsProtocolの実装
- [x] 2.1 プロトコルリクエスト・レスポンスクラスを実装
  - GetRailConnectionsRequestクラスを作成（リクエストボディなし）
  - GetRailConnectionsResponseクラスでRailConnectionData配列を返す構造
  - プロトコルタグ "va:getRailConnections" を定義
  - MessagePack互換のコンストラクタとObsolete属性を実装
  - GetRailConnectionsResponseのコンストラクタでList<RailConnectionInfo>を受け取るように実装
  - _Requirements: 1.1, 1.5_

- [x] 2.2 GetRailConnectionsProtocolクラスの実装
  - IPacketResponseインターフェースを実装
  - GetResponse()メソッドでRailGraphDatastoreから全接続情報を取得
  - 取得した接続情報をRailConnectionData配列に変換
  - GetRailConnectionsResponseを返す処理
  - ServiceProviderからの依存性注入対応
  - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 2.3 PacketResponseCreatorへのプロトコル登録
  - PacketResponseCreatorのコンストラクタに新規プロトコルを追加
  - プロトコルタグをキーとして辞書に登録
  - 既存のAllBlockStateProtocolと同様のパターンで実装
  - _Requirements: 1.1_

- [x] 2.4 GetRailConnectionsProtocolの単体テストを作成
  - 空のレールグラフで空配列を返すテスト
  - 複数のレール接続が存在する場合の正常系テスト
  - MessagePackシリアライズ・デシリアライズの検証テスト
  - レール接続情報の完全性確認テスト
  - _Requirements: 10.1_

- [x] 3. RequestWorldDataProtocolの拡張実装（仕様変更により取り消し）
- [x] 3.1 ResponseWorldDataMessagePackクラスにレール情報フィールドを追加（仕様変更により取り消し）
  - 仕様変更により、RequestWorldDataProtocolへの統合は不要となりました
  - _Requirements: 3.1, 3.2_

- [x] 3.2 RequestWorldDataProtocolのGetResponse()メソッドを拡張（仕様変更により取り消し）
  - 仕様変更により、RequestWorldDataProtocolへの統合は不要となりました
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [ ] 3.3 RequestWorldDataProtocol拡張の単体テストを作成（仕様変更により不要）
  - 仕様変更により、RequestWorldDataProtocolへの統合は不要となりました
  - _Requirements: 10.1_

- [x] 4. RailGraphDatastoreへのイベントシステム追加
- [x] 4.1 RailGraphUpdateEventを定義
  - UniRx.Subjectを使用したイベント定義
  - イベントペイロードに変更されたRailComponentID情報を含める
  - RailGraphDatastoreにイベントのstaticプロパティを追加
  - イベントのSubscribe/Publishパターンを実装
  - _Requirements: 2.1, 2.3_

- [x] 4.2 RailGraphDatastoreのConnectNode/DisconnectNodeでイベント発火
  - ConnectNode実行時にRailGraphUpdateEventを発火
  - DisconnectNode実行時にRailGraphUpdateEventを発火
  - 変更されたRailComponentの位置情報をイベントに含める
  - 複数変更の統合処理（同一フレーム内の変更を1イベントに）
  - _Requirements: 2.1, 2.3, 2.4_

- [x] 5. RailConnectionsEventPacketの実装
- [x] 5.1 RailConnectionsEventMessagePackクラスを実装
  - AllConnections配列で全レール接続情報を保持
  - ChangedComponentIds配列で変更があったRailComponentを記録
  - イベントタグ "va:event:railConnectionsUpdate" を定義
  - MessagePack互換の構造とObsolete属性付きコンストラクタ
  - RailConnectionsEventMessagePackのコンストラクタでList<RailConnectionInfo>を受け取るように実装
  - _Requirements: 2.2, 2.3_

- [x] 5.2 RailConnectionsEventPacketクラスを実装
  - コンストラクタでRailGraphUpdateEventをSubscribe
  - イベント受信時に全レール接続情報を収集
  - 変更されたRailComponentIDを記録
  - RailConnectionsEventMessagePackを生成してEventProtocolProviderへEnqueue
  - _Requirements: 2.1, 2.2, 2.3, 2.5_

- [x] 5.3 イベントパケットの初期化処理を追加
  - サーバー起動時にRailConnectionsEventPacketをインスタンス化
  - RailGraphDatastore初期化後に実行
  - インスタンスを保持してガベージコレクションを防止
  - 既存のPlaceBlockEventPacketと同様の初期化パターン
  - _Requirements: 2.1_

- [ ] 5.4 RailConnectionsEventPacketの単体テストを作成
  - イベント発火時にEventProtocolProviderへEnqueueされることを確認
  - 全レール接続情報が含まれることを検証
  - 変更RailComponentIDが正しく記録されることを確認
  - MessagePackシリアライズ・デシリアライズを検証
  - _Requirements: 10.2_

- [ ] 6. Unity Splinesパッケージのセットアップ
- [ ] 6.1 Unity Splinesパッケージをインストール
  - Package Managerでcom.unity.splinesパッケージを追加
  - バージョン2.x以上を指定
  - パッケージ依存関係の解決
  - プロジェクトのコンパイル確認
  - _Requirements: 6.1_

- [ ] 7. BezierRailCurveCalculatorの実装
- [ ] 7.1 ベジエ曲線制御点計算ロジックを実装
  - RailNodeInfoペアから4つの制御点（P0, P1, P2, P3）を計算
  - RailControlPointのOriginalPositionとControlPointPositionを使用
  - 座標正規化と相対座標変換処理
  - BezierUtility.RAIL_LENGTH_SCALE（1024.0f）を考慮したスケール変換
  - _Requirements: 5.1, 5.5_

- [ ] 7.2 距離に応じた曲線簡略化機能を実装
  - 短距離レール接続の判定処理
  - 短距離の場合は制御点を始終点近くに配置
  - 長距離の場合は通常のベジエ曲線計算
  - 既存BezierUtilityロジックとの互換性維持
  - _Requirements: 5.3, 5.4, 5.5_

- [ ] 7.3 レール接続データのバリデーション機能を実装
  - RailNodeInfoの有効性チェック
  - 座標のNaN/Infinity検出
  - 距離が正の値であることの確認
  - 無効データの場合はデフォルト値返却とエラーログ出力
  - _Requirements: 9.1, 9.2, 9.3_

- [ ] 7.4 BezierRailCurveCalculatorの単体テストを作成
  - 正常な制御点計算のテスト
  - 短距離レールの簡略化計算テスト
  - 長距離レールの通常計算テスト
  - 無効データのバリデーションテスト
  - 様々なノード配置パターンでの検証
  - _Requirements: 10.4_

- [ ] 8. TrainRailObjectManagerの基本実装
- [ ] 8.1 TrainRailObjectManagerクラスの基本構造を実装
  - MonoBehaviourを継承したクラス定義
  - シングルトンパターンの実装（Awakeで_instance設定）
  - ITrainRailObjectManagerインターフェースの実装
  - Initialize()とDispose()ライフサイクルメソッド
  - 内部状態管理（Uninitialized, Initialized, DataLoaded, Disposed）
  - _Requirements: 4.1, 7.1, 7.4_

- [ ] 8.2 レール接続データの内部モデルを実装
  - RailConnectionData配列を保持するDictionary構造
  - レール接続の一意識別子生成ロジック
  - 重複データの検出と排除機能
  - データ更新時の差分検出処理
  - _Requirements: 4.2, 4.5_

- [ ] 8.3 OnRailDataReceived()メソッドを実装
  - プロトコルから受信したRailConnectionData配列を処理
  - 内部モデルへのデータ変換
  - 各レール接続に対するベジエ曲線計算の呼び出し
  - 段階的な表示更新処理（フリーズ回避）
  - _Requirements: 4.1, 7.2, 8.4_

- [ ] 8.4 OnRailUpdateEvent()メソッドを実装
  - イベントで受信した全レール情報で内部モデルを全置換
  - 変更されたRailComponentIDを使用した差分更新最適化
  - 削除されたレール接続の特定と処理
  - 追加されたレール接続の特定と処理
  - 更新されたレール接続の特定と処理
  - _Requirements: 4.2, 4.3, 8.2_

- [ ] 8.5 SetVisualizationEnabled()メソッドを実装
  - レール表示の有効/無効を切り替える処理
  - 全SplineオブジェクトのMeshRenderer有効/無効化
  - 状態管理（Disabled状態への遷移）
  - _Requirements: 7.5_

- [ ] 9. Unity Spline表示機能の実装
- [ ] 9.1 CreateSplineObject()プライベートメソッドを実装
  - GameObjectの生成（名前: "RailSpline_{FromId}_{ToId}"）
  - SplineContainerコンポーネントの追加
  - Splineインスタンスの作成とBezierKnotの設定
  - SplineExtrudeコンポーネントの追加（メッシュ生成）
  - マテリアル設定（半透明、カラー指定）
  - _Requirements: 6.1, 6.2_

- [ ] 9.2 UpdateSplineObject()プライベートメソッドを実装
  - 既存SplineContainerの制御点更新
  - ベジエ曲線の再計算
  - メッシュの再生成
  - 最小限の更新で効率化
  - _Requirements: 6.5_

- [ ] 9.3 DestroySplineObject()プライベートメソッドを実装
  - SplineオブジェクトのGameObject破棄
  - 内部Dictionaryからの削除
  - リソースの適切な解放処理
  - メモリリークの防止
  - _Requirements: 6.3, 6.6, 7.4_

- [ ] 9.4 Spline管理用Dictionaryとライフサイクル処理を実装
  - Dictionary<RailConnectionId, SplineContainer>の定義
  - 各レール接続に対する1つのSplineContainerの管理
  - Dispose()での全Splineオブジェクトのクリーンアップ
  - OnDestroy()でのDispose()呼び出し
  - _Requirements: 6.4, 6.6, 7.4_

- [ ] 10. クライアント側プロトコル受信処理の実装
- [ ] 10.1 RequestWorldDataレスポンス受信時の処理を実装
  - NetworkClientでResponseWorldDataMessagePackを受信
  - RailConnections配列の抽出
  - TrainRailObjectManager.OnRailDataReceived()の呼び出し
  - 初期ロードフローの完成
  - _Requirements: 3.4, 4.1_

- [ ] 10.2 RailConnectionsEventの受信処理を実装
  - NetworkClientでRailConnectionsEventMessagePackを受信
  - AllConnections配列とChangedComponentIds配列の抽出
  - TrainRailObjectManager.OnRailUpdateEvent()の呼び出し
  - リアルタイム更新フローの完成
  - _Requirements: 2.1, 4.2_

- [ ] 10.3 VContainerへのTrainRailObjectManager登録
  - クライアント側DIコンテナでITrainRailObjectManagerを登録
  - Singleton寿命で登録
  - NetworkClientへの依存性注入
  - ゲームシーンのライフサイクルとの統合
  - _Requirements: 7.1_

- [ ] 11. エラーハンドリングとデータバリデーションの実装
- [ ] 11.1 サーバー側のエラーハンドリングを実装
  - RailGraphDatastore未初期化時のnullチェックと空配列返却
  - プロトコル実行エラーのログ出力
  - MessagePackシリアライズ失敗時の処理
  - ログプレフィックス "[RailVisualization][Protocol]" を使用
  - _Requirements: 9.3_

- [ ] 11.2 クライアント側のデータバリデーションを実装
  - 存在しないRailComponentIDのチェックとスキップ
  - 不正な向き情報のデフォルト値補完
  - 負の距離値の絶対値変換
  - NaN/Infinity座標のゼロベクトル補完
  - ログプレフィックス "[RailVisualization][Validation]" を使用
  - _Requirements: 9.1, 9.2, 9.3_

- [ ] 11.3 MessagePackデシリアライズエラー処理を実装
  - デシリアライズ失敗時のエラーログ出力
  - 該当データをスキップして処理継続
  - プロトコルバージョン不一致の警告表示
  - 堅牢なエラーリカバリ処理
  - _Requirements: 9.3, 9.4_

- [ ] 12. パフォーマンス最適化機能の実装
- [ ] 12.1 Splineカリング機能を実装
  - カメラ視錐台外のSpline検出
  - 画面外SplineのMeshRenderer無効化
  - カメラ移動時の動的なカリング更新
  - パフォーマンス目標（60FPS）の維持
  - _Requirements: 8.3_

- [ ] 12.2 段階的表示更新機能を実装
  - 大量レール接続受信時のフレーム分散処理
  - フレームごとに10本ずつSpline生成
  - 更新進捗の内部管理
  - フリーズ回避の確認
  - _Requirements: 8.4_

- [ ] 12.3 差分更新最適化を実装
  - 変更されたRailComponentのSplineのみ更新
  - 未変更Splineの再生成スキップ
  - 更新対象の効率的な特定
  - 更新パフォーマンスの測定
  - _Requirements: 8.2_

- [ ] 13. 統合テストの作成
- [ ] 13.1 サーバー・クライアント初期ロード統合テストを作成
  - サーバー起動とレールグラフ初期化
  - RequestWorldDataプロトコル実行
  - クライアントでのレール情報受信確認
  - Spline表示の生成確認
  - _Requirements: 10.1, 10.3_

- [ ] 13.2 レール追加・削除の統合テストを作成
  - RailConnectionEditProtocolでレール追加
  - イベント受信とSpline生成確認
  - RailConnectionEditProtocolでレール削除
  - イベント受信とSpline削除確認
  - _Requirements: 10.1, 10.2_

- [ ] 13.3 複数レール同時変更の統合テストを作成
  - 複数レール接続の一括変更
  - 単一イベントパケットの受信確認
  - 全Splineの一括更新確認
  - 状態整合性の検証
  - _Requirements: 10.2_

- [ ] 13.4 イベント欠損リカバリの統合テストを作成
  - イベント欠損のシミュレーション
  - GetRailConnectionsProtocolでの手動再取得
  - 状態復元の確認
  - クライアント・サーバー整合性の検証
  - _Requirements: 10.3_

- [ ] 14. パフォーマンステストの実施
- [ ] 14.1 基本パフォーマンステストを実行
  - レール100本配置時のフレームレート測定
  - 目標60FPS以上の達成確認
  - 初期ロード時間測定（1秒以内）
  - 更新レスポンス時間測定（100ms以内）
  - _Requirements: 8.1, 10.1_

- [ ] 14.2 大規模レールネットワークのパフォーマンステストを実行
  - レール500本配置時のフレームレート測定
  - 目標30FPS以上の達成確認
  - メモリ使用量測定（Spline1個あたり50KB以下）
  - メモリリークの検出と修正
  - _Requirements: 8.1, 10.1_

- [ ] 14.3 更新頻度パフォーマンステストを実行
  - 秒間10回のSpline更新実行
  - フレーム落ちの有無確認
  - 差分更新の効果測定
  - 段階的更新の効果測定
  - _Requirements: 8.2, 10.1_

- [ ] 15. E2Eテストとドキュメント整備
- [ ] 15.1 プレイモードE2Eテストを実行
  - ゲーム起動からレール配置までの一連の流れ
  - レール表示の視覚的確認
  - リアルタイム更新の動作確認
  - シーン遷移時のリソースクリーンアップ確認
  - _Requirements: 10.3_

- [ ] 15.2 カリング動作の手動テストを実行
  - カメラを移動してカリング動作確認
  - 画面外Splineの非表示確認
  - パフォーマンス改善効果の測定
  - _Requirements: 8.3, 10.1_

- [ ] 15.3 実装ドキュメントを整備
  - 各コンポーネントの実装詳細を記録
  - プロトコル仕様のドキュメント化
  - トラブルシューティングガイドの作成
  - 既知の制限事項と今後の拡張ポイントの記載
  - _Requirements: 全要件_
