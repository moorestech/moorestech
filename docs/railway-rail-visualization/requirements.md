# Requirements Document

## Introduction
サーバー側の鉄道レール接続情報をクライアント側で視覚化するシステムを構築します。このシステムにより、プレイヤーはレールの配置状況と接続関係を視覚的に把握でき、効率的な鉄道ネットワークの構築が可能になります。

サーバー側では既存のRailGraphDatastoreが管理するレール接続情報をプロトコル経由でクライアントへ送信し、クライアント側ではUnity Splineパッケージを用いてベジエ曲線による滑らかなレール表示を実現します。

## Requirements

### Requirement 1: 全レール情報取得プロトコル
**Objective:** クライアント開発者として、ゲーム開始時に全てのレール接続情報を取得したい。これにより、初期状態のレールネットワークを表示できる。

#### Acceptance Criteria
1. WHEN クライアントが全レール情報取得リクエストを送信する THEN サーバーシステムは全てのレールノード接続情報を返す SHALL
2. WHEN サーバーがレール情報を返す THEN レスポンスにはレールノードの座標、接続先ノード座標、ノード間の距離情報が含まれている SHALL
3. WHEN サーバーがレール情報を返す THEN レスポンスにはレールノードの向き情報（表裏）が含まれている SHALL
4. WHERE レール接続が存在しない場合 THE サーバーシステムは空のレール配列を返す SHALL
5. WHEN プロトコルがMessagePack形式でシリアライズされる THEN サーバーシステムは正常にデシリアライズできる SHALL

### Requirement 2: レール更新イベントプロトコル
**Objective:** クライアント開発者として、レール接続の変更をリアルタイムで受信したい。これにより、他プレイヤーのレール編集を即座に画面へ反映できる。

#### Acceptance Criteria
1. WHEN サーバー側でレール接続が追加・削除される THEN サーバーシステムは全てのクライアントへイベントパケットを送信する SHALL
2. WHEN レール更新イベントが発生する THEN イベントデータには現在の全レール接続情報が含まれている SHALL
3. WHEN レール更新イベントが発生する THEN イベントデータには変更があったレールノードの位置情報が含まれている SHALL
4. WHERE 複数のレール接続が同時に変更される場合 THE サーバーシステムは1つのイベントパケットに統合して送信する SHALL
5. WHEN イベントパケットがキューへ追加される THEN EventProtocolProvider.EventPacketsに正しく格納される SHALL

### Requirement 3: 初期データ取得への統合
**Objective:** システム設計者として、ゲーム開始時のワールドデータ取得にレール情報を含めたい。これにより、クライアントは初期ロード時に全ての必要情報を一度に取得できる。

#### Acceptance Criteria
1. WHEN クライアントがRequestWorldDataプロトコルを実行する THEN レスポンスに全レール接続情報が含まれている SHALL
2. WHEN ワールドデータがロードされる THEN レール情報はブロック情報やエンティティ情報と同じレスポンス内に含まれている SHALL
3. WHERE レールが存在しないワールドの場合 THE レール情報フィールドは空配列として返される SHALL
4. WHEN 初期データ取得が完了する THEN クライアントは別途レール情報取得リクエストを送信する必要がない SHALL

### Requirement 4: クライアント側レールデータ管理
**Objective:** クライアント開発者として、受信したレール情報を効率的に管理したい。これにより、表示更新時の参照が高速化される。

#### Acceptance Criteria
1. WHEN クライアントがレール情報を受信する THEN TrainRailObjectManagerがデータを保持する SHALL
2. WHEN レール更新イベントを受信する THEN TrainRailObjectManagerは既存データを更新する SHALL
3. WHERE レールノードが削除される場合 THE TrainRailObjectManagerは対応するデータを削除する SHALL
4. WHEN レールデータが更新される THEN TrainRailObjectManagerは変更通知を発行する SHALL
5. WHERE 同一レール接続の重複データを受信した場合 THE TrainRailObjectManagerは重複を排除する SHALL

### Requirement 5: ベジエ曲線計算
**Objective:** クライアント開発者として、レールノード間の接続をベジエ曲線で表現したい。これにより、視覚的に自然なレール表示が実現できる。

#### Acceptance Criteria
1. WHEN 2つのレールノード接続情報を受け取る THEN クライアントシステムはベジエ曲線の制御点を計算する SHALL
2. WHEN ベジエ曲線を計算する THEN レールノードの向き情報を考慮して接線方向を決定する SHALL
3. WHERE レールノード間の距離が短い場合 THE クライアントシステムは簡略化された曲線を生成する SHALL
4. WHERE レールノード間の距離が長い場合 THE クライアントシステムは適切な曲率の曲線を生成する SHALL
5. WHEN ベジエ曲線が計算される THEN 既存のBezierUtilityクラスとの互換性を保つ SHALL

### Requirement 6: Unity Splineによる表示
**Objective:** クライアント開発者として、Unity公式Splineパッケージを使用してレールを表示したい。これにより、標準化された手法で効率的な表示が実現できる。

#### Acceptance Criteria
1. WHEN TrainRailObjectManagerがレールデータを受け取る THEN Unity Splineオブジェクトを生成する SHALL
2. WHEN Splineオブジェクトが生成される THEN 計算されたベジエ曲線の制御点をSplineへ設定する SHALL
3. WHEN レール接続が削除される THEN 対応するSplineオブジェクトを破棄する SHALL
4. WHERE 複数のレール接続が存在する場合 THE TrainRailObjectManagerは各接続ごとに個別のSplineオブジェクトを管理する SHALL
5. WHEN Splineオブジェクトが更新される THEN 画面上のレール表示が即座に反映される SHALL
6. WHERE レール表示が不要になった場合 THE TrainRailObjectManagerはSplineオブジェクトのリソースを適切に解放する SHALL

### Requirement 7: TrainRailObjectManagerの責務
**Objective:** システム設計者として、TrainRailObjectManagerに明確な責務を定義したい。これにより、保守性の高い設計が実現できる。

#### Acceptance Criteria
1. WHEN TrainRailObjectManagerが初期化される THEN レールデータ購読、Spline管理、表示更新の各機能を持つ SHALL
2. WHEN プロトコルからレールデータを受信する THEN TrainRailObjectManagerは受信データをパースして内部モデルへ変換する SHALL
3. WHEN 内部モデルが更新される THEN TrainRailObjectManagerはベジエ曲線計算とSpline更新を実行する SHALL
4. WHERE ゲームシーンが破棄される場合 THE TrainRailObjectManagerは全てのSplineオブジェクトをクリーンアップする SHALL
5. WHEN レール表示の有効/無効が切り替わる THEN TrainRailObjectManagerはSplineオブジェクトの表示状態を制御する SHALL

### Requirement 8: パフォーマンス考慮
**Objective:** システム設計者として、大量のレール接続が存在する場合でも安定動作させたい。これにより、大規模な鉄道ネットワークでもフレームレートが維持できる。

#### Acceptance Criteria
1. WHERE 100本以上のレール接続が存在する場合 THE クライアントシステムは60FPSを維持する SHALL
2. WHEN レール更新イベントを受信する THEN 変更されたレールのSplineのみを更新する SHALL
3. WHERE 画面外のレールが存在する場合 THE クライアントシステムはカリング処理を適用する SHALL
4. WHEN 大量のレール接続データを受信する THEN 段階的に表示を更新してフリーズを回避する SHALL

### Requirement 9: エラーハンドリング
**Objective:** クライアント開発者として、不正なレールデータを受信した場合の挙動を定義したい。これにより、予期しないクラッシュを防止できる。

#### Acceptance Criteria
1. WHERE 存在しないレールノード座標を受信した場合 THE クライアントシステムはその接続をスキップする SHALL
2. WHERE 不正な向き情報を受信した場合 THE クライアントシステムはデフォルト値で補完する SHALL
3. WHERE MessagePackデシリアライズに失敗した場合 THE クライアントシステムはエラーログを出力して処理を継続する SHALL
4. WHEN プロトコルバージョン不一致を検出する THEN クライアントシステムは警告を表示する SHALL

### Requirement 10: テスト容易性
**Objective:** テストエンジニアとして、プロトコルとクライアント表示の両方をテスト可能にしたい。これにより、リグレッションを防止できる。

#### Acceptance Criteria
1. WHEN サーバー側プロトコルテストを実行する THEN 正常系と異常系の両方のケースをカバーする SHALL
2. WHEN イベントパケットテストを実行する THEN イベントキューへの追加とデシリアライズを検証する SHALL
3. WHERE クライアント側の単体テストを実行する場合 THE モックデータでTrainRailObjectManagerの動作を確認できる SHALL
4. WHEN ベジエ曲線計算のテストを実行する THEN 様々なノード配置パターンで正しい制御点が計算される SHALL
