# Requirements Document

## はじめに

このドキュメントは、クライアント側から列車をレールに配置できるシステムの要件を定義します。プレイヤーがインベントリ内の列車アイテムを選択し、既存のレール上に列車を配置できる機能を実装します。

このシステムは、既存のPlaceSystemアーキテクチャを活用し、列車配置専用のPlaceSystemと、サーバー・クライアント間通信のためのプロトコルを追加します。

## ビジネス価値

- プレイヤーがUIを通じて直感的に列車を配置できるようになる
- レールシステムと列車システムの統合が完成し、プレイヤーが自由に輸送ネットワークを構築できる
- ゲームプレイの幅が広がり、工業オートメーションの楽しさが向上する

## 要件

### 要件1: クライアント側の列車配置システム（TrainCarPlaceSystem）

**目的:** プレイヤーとして、インベントリ内の列車アイテムを選択し、既存のレール上に列車を配置したい。これにより、自分の輸送ネットワークを構築できる。

#### 受入基準

1. WHEN プレイヤーが列車アイテムをホットバーで選択 THEN TrainCarPlaceSystem SHALL 有効化される
2. WHEN TrainCarPlaceSystemが有効化されている AND プレイヤーがカーソルを既存のレールブロック上に移動 THEN システム SHALL レール上の配置可能位置にプレビューを表示する
3. WHEN プレイヤーがレールブロック上でクリック AND 配置位置が有効 THEN TrainCarPlaceSystem SHALL サーバーに列車配置プロトコルを送信する
4. WHEN プレイヤーがレールが存在しない位置にカーソルを移動 THEN システム SHALL プレビューを非表示にする
5. WHEN プレイヤーが別のアイテムを選択 OR PlaceSystemが無効化 THEN TrainCarPlaceSystem SHALL プレビューをクリーンアップし、無効化される
6. WHERE 配置対象のレール上に既に列車が存在する場合 THE システム SHALL 配置を無効とし、プレビューに配置不可を示す

### 要件2: レール上の配置位置計算

**目的:** プレイヤーとして、クリックしたレール位置に正確に列車が配置されることを期待する。これにより、意図した位置に列車を配置できる。

#### 受入基準

1. WHEN プレイヤーがレールブロックをクリック THEN システム SHALL レールコンポーネント（RailComponent）を特定する
2. WHEN レールコンポーネントが特定された THEN システム SHALL RailComponentSpecifierを生成する
3. WHERE RailComponentSpecifierには THE ブロック座標情報 SHALL 含まれる
4. WHEN 配置位置が計算される THEN システム SHALL レールノードの開始位置を列車の初期位置として決定する
5. WHERE 複数のレールコンポーネントが同一ブロックに存在する可能性がある場合 THE システム SHALL 適切なコンポーネントを選択するロジックを実装する

### 要件3: プレビュー表示システム

**目的:** プレイヤーとして、配置前に列車がどこに配置されるか視覚的に確認したい。これにより、誤配置を防ぎ、意図した位置に配置できる。

#### 受入基準

1. WHEN 配置可能な位置にカーソルがある THEN システム SHALL 列車のプレビューモデルを表示する
2. WHEN 配置が可能 THEN プレビュー SHALL 緑色または配置可能を示す色で表示される
3. WHEN 配置が不可能（既に列車が存在する等） THEN プレビュー SHALL 赤色または配置不可を示す色で表示される
4. WHERE プレビューモデルは THE 列車の実際の見た目と同じモデル SHALL 使用される
5. WHEN プレイヤーがカーソルを移動 THEN プレビュー SHALL リアルタイムで位置を更新する

### 要件4: サーバー側の列車配置プロトコル（PlaceTrainOnRailProtocol）

**目的:** クライアントからの列車配置リクエストを受信し、サーバー側でTrainUnitを生成し、ゲームワールドに追加したい。これにより、クライアントとサーバー間で列車配置の同期が保たれる。

#### 受入基準

1. WHEN クライアントから列車配置プロトコルを受信 THEN サーバー SHALL リクエストをデシリアライズする
2. WHERE リクエストには THE 以下の情報 SHALL 含まれる
   - RailComponentSpecifier（レール位置情報）
   - 配置する列車のItemId
   - プレイヤーID
3. WHEN リクエストが受信された THEN サーバー SHALL 以下を検証する
   - 指定されたレール位置が有効であること
   - プレイヤーのインベントリに列車アイテムが存在すること
   - 指定位置に既に列車が配置されていないこと
4. WHEN すべての検証が成功 THEN サーバー SHALL TrainUnitを生成する
5. WHEN TrainUnitが生成される THEN サーバー SHALL 以下の初期化を実行する
   - RailPositionを初期化（指定されたレールノードから開始）
   - TrainCarリストを初期化（配置する列車の仕様に基づく）
   - TrainUpdateServiceに登録
6. WHEN TrainUnitが正常に生成された THEN サーバー SHALL プレイヤーのインベントリから列車アイテムを削除する
7. WHEN 配置処理が完了 THEN サーバー SHALL クライアントに成功レスポンスを返す
8. WHEN 検証が失敗 OR エラーが発生 THEN サーバー SHALL エラーレスポンスを返し、列車を生成しない

### 要件5: MessagePackデータ構造

**目的:** 開発者として、型安全で効率的な通信を実現したい。これにより、プロトコルの保守性と信頼性が向上する。

#### 受入基準

1. WHERE PlaceTrainOnRailRequestMessagePack THE 以下のフィールド SHALL 含まれる
   - Tag (string): プロトコルタグ
   - RailSpecifier (RailComponentSpecifier): レール位置情報
   - TrainItemId (ItemId): 配置する列車のアイテムID
2. WHERE PlaceTrainOnRailResponseMessagePack THE 以下のフィールド SHALL 含まれる
   - Tag (string): プロトコルタグ
   - IsSuccess (bool): 配置成功/失敗
   - ErrorMessage (string, optional): エラーメッセージ（失敗時のみ）
   - TrainId (Guid, optional): 生成された列車のID（成功時のみ）
3. WHERE すべてのMessagePackクラス THE `[MessagePackObject]`属性 SHALL 付与される
4. WHERE すべてのフィールド THE `[Key(n)]`属性で連番 SHALL 付与される
5. WHERE すべてのMessagePackクラス THE デシリアライズ用の引数なしコンストラクタ SHALL 実装される

### 要件6: プロトコルの登録と統合

**目的:** 開発者として、新しいプロトコルが既存のプロトコルシステムに正しく統合されることを保証したい。これにより、通信が正常に機能する。

#### 受入基準

1. WHEN PlaceTrainOnRailProtocolが実装される THEN プロトコル SHALL PacketResponseCreatorに登録される
2. WHERE プロトコルタグは THE 一意の値 SHALL 使用される（例: "va:place_train_on_rail"）
3. WHEN クライアントがプロトコルを送信 THEN ClientContext.VanillaApi SHALL 適切なメソッドを提供する
4. WHERE VanillaApiメソッドは THE TrainCarPlaceSystemから呼び出し可能 SHALL である

### 要件7: RailGraph統合

**目的:** 開発者として、列車配置時にRailGraphから正しいレールノード情報を取得したい。これにより、列車が正しいレール位置に配置される。

#### 受入基準

1. WHEN RailComponentSpecifierを受信 THEN サーバー SHALL RailGraphDatastoreから対応するRailComponentを取得する
2. WHEN RailComponentが取得される THEN システム SHALL RailNodeリストを抽出する
3. WHERE RailNodeリストは THE 列車の初期RailPosition生成に使用 SHALL される
4. WHEN レールノードが存在しない OR 無効 THEN システム SHALL エラーを返し、列車を生成しない
5. WHERE 駅レールの場合 THE システム SHALL 駅ノード情報も考慮する

### 要件8: 列車の初期状態設定

**目的:** 配置された列車が適切な初期状態で生成されることを保証したい。これにより、配置後すぐにプレイヤーが列車を使用できる。

#### 受入基準

1. WHEN TrainUnitが生成される THEN 列車 SHALL 静止状態（速度0）で初期化される
2. WHEN TrainUnitが生成される THEN 列車 SHALL 自動運転が無効な状態で初期化される
3. WHERE 列車の長さ（trainLength）は THE TrainCarリストの合計長 SHALL 使用される
4. WHEN RailPositionが初期化される THEN initialDistanceToNextNode SHALL 0で初期化される
5. WHERE 列車の向きは THE レールの方向に基づいて自動的に決定 SHALL される

### 要件9: PlaceSystemSelectorへの統合

**目的:** プレイヤーが列車アイテムを選択したときに、自動的にTrainCarPlaceSystemが有効化されることを保証したい。これにより、シームレスなユーザー体験を提供する。

#### 受入基準

1. WHEN PlaceSystemSelectorがPlaceSystemを選択 THEN システム SHALL アイテムIDから列車アイテムを検出する
2. WHERE 列車アイテムは THE PlaceSystemMasterに新しいPlaceModeとして登録 SHALL される
3. WHEN 列車アイテムが選択された THEN PlaceSystemSelector SHALL TrainCarPlaceSystemを返す
4. WHERE PlaceModeは THE 例えば"TrainCar"という新しい値 SHALL 使用される
5. WHEN TrainCarPlaceSystemがDI登録される THEN システム SHALL MainGameStarterで適切に登録される

### 要件10: エラーハンドリングとバリデーション

**目的:** 開発者として、様々なエラーケースが適切に処理されることを保証したい。これにより、システムの堅牢性が向上する。

#### 受入基準

1. WHEN 無効なRailComponentSpecifierを受信 THEN サーバー SHALL エラーレスポンスを返す
2. WHEN プレイヤーのインベントリに列車アイテムが存在しない THEN サーバー SHALL 配置を拒否する
3. WHEN 指定位置に既に列車が存在する THEN サーバー SHALL 配置を拒否する
4. WHEN RailGraphから対応するレールが見つからない THEN サーバー SHALL エラーレスポンスを返す
5. WHERE すべてのエラーケース THE 適切なエラーメッセージ SHALL クライアントに返される
6. WHEN クライアントがエラーレスポンスを受信 THEN システム SHALL ユーザーにフィードバックを表示する（ログ出力等）

### 要件11: テストカバレッジ

**目的:** 開発者として、すべての機能が適切にテストされることを保証したい。これにより、リグレッションを防ぎ、品質を維持する。

#### 受入基準

1. WHERE PlaceTrainOnRailProtocol THE 単体テストクラス SHALL 作成される
2. WHEN テストが実行される THEN 正常系のシナリオ SHALL テストされる
   - 有効なリクエストで列車が正常に配置される
3. WHEN テストが実行される THEN 異常系のシナリオ SHALL テストされる
   - 無効なレール位置での配置失敗
   - インベントリに列車がない場合の失敗
   - 既に列車が存在する位置での失敗
4. WHERE TrainCarPlaceSystem THE 統合テストまたは手動テスト SHALL 実施される
5. WHEN テストが作成される THEN テスト SHALL ForUnitTestModのテストデータを使用する

## 非機能要件

### パフォーマンス

- この機能ではパフォーマンス最適化は考慮不要（プロジェクト方針に基づく）
- まず動作する実装を優先し、必要に応じて後から改善

### 保守性

- 既存のPlaceSystemパターンに従い、一貫性のあるコードを実装
- 適切なコメント（日本語・英語の2行セット）を記述
- #regionとローカル関数を活用し、可読性を向上

### テスト容易性

- すべての主要機能に対して単体テストを作成
- テストでは既存のテストデータ（ForUnitTestMod）を活用

## 制約条件

1. **既存システムの活用**: 新しいTrainCarPlaceSystemは既存のIPlaceSystemインターフェースを実装する
2. **プロトコル規約遵守**: プロトコルタグは"va:"プレフィックスを使用
3. **MessagePack規約**: すべてのデータクラスはMessagePack属性を適切に使用
4. **try-catch禁止**: エラーハンドリングは条件分岐とnullチェックで対応
5. **Null前提の最小化**: 基本的な部分はnullではない前提で実装
6. **後方互換性は考慮不要**: より良い設計を優先

## 依存関係

### サーバー側

- `Game.Train`: TrainUnit、RailPosition、TrainCarの生成
- `Game.Train.RailGraph`: RailGraphDatastore、RailComponentSpecifier
- `Core.Item`: アイテムID、インベントリ操作
- `Server.Protocol.PacketResponse`: プロトコル基盤

### クライアント側

- `Client.Game.InGame.BlockSystem.PlaceSystem`: PlaceSystemアーキテクチャ
- `Client.Network`: プロトコル送信
- `Mooresmaster.Model`: マスターデータアクセス

## 参照ドキュメント

- `docs/ProtocolImplementationGuide.md`: プロトコル実装の詳細ガイド
- `docs/ServerGuide.md`: サーバー側実装ガイド
- `docs/ClientGuide.md`: クライアント側実装ガイド
- `.serena/memories/train_on_rail_placement_investigation.md`: 列車配置の技術調査
- `.kiro/steering/structure.md`: プロジェクト構造
