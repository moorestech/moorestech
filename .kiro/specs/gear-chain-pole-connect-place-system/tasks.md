# Implementation Plan

## Tasks

- [ ] 1. PlaceSystemスキーマの拡張
- [ ] 1.1 (P) GearChainConnect PlaceModeの追加
  - PlaceModeの定義にGearChainConnect値を追加し、チェーンアイテム専用のPlaceModeを定義する
  - SourceGeneratorでコード自動生成を実行して、新しいPlaceModeが使用可能になることを確認する
  - _Requirements: 5.1_

- [ ] 2. サーバー通信APIの拡張
- [ ] 2.1 (P) GearChainPole接続・切断リクエスト送信機能の追加
  - 接続元位置、接続先位置、チェーンアイテムIDを指定して接続リクエストを送信する機能を追加する
  - 2つのポール位置を指定して切断リクエストを送信する機能を追加する
  - 既存のConnectRail/DisconnectRailパターンに従い、GearChainConnectionEditRequestを活用する
  - _Requirements: 3.1, 3.2, 3.3, 4.2_

- [ ] 3. ポール検出システムの作成
- [ ] 3.1 GearChainPole検出用インターフェースとコンポーネントの作成
  - レイキャストでGearChainPoleブロックを特定するためのインターフェースを定義する
  - ポール位置、最大接続距離、接続数上限到達状態、既存接続確認の機能を持たせる
  - GearChainPoleブロックのGameObjectにアタッチされるコンポーネントを作成する
  - ブロック生成時にコンポーネントが追加される仕組みを用意する
  - _Requirements: 1.2_

- [ ] 4. 接続プレビュー表示システムの作成
- [ ] 4.1 (P) 接続ラインプレビューの作成
  - 接続元から接続先またはカーソル位置への接続ラインを表示する機能を作成する
  - LineRendererを使用して2点間のラインを描画する
  - 接続状態に応じた色変更機能を実装する（接続可能：緑、接続不可：赤、既存接続：黄）
  - _Requirements: 1.3, 2.2, 2.3, 2.4, 2.5_

- [ ] 4.2 (P) 接続範囲表示の作成
  - 接続元ポールの最大接続距離を視覚的に表示する機能を作成する
  - EnergizedRangeObjectパターンに従い、スケールで範囲を表現する
  - 接続元選択時に表示、選択解除時に非表示とする制御を実装する
  - _Requirements: 2.1_

- [ ] 4.3 接続コスト計算と表示機能の追加
  - 接続に必要なチェーンアイテム消費量を計算する機能を追加する
  - 消費量は「距離 / consumptionPerLength（切り上げ）」で計算する
  - gearChainItemsマスターデータからconsumptionPerLength値を取得する
  - プレイヤーインベントリの所持数と比較し、不足時は視覚的に警告する（赤色表示など）
  - _Requirements: 6.1, 6.2, 6.3_

- [ ] 5. GearChainConnectPlaceSystemの実装
- [ ] 5.1 PlaceSystem基本構造の作成
  - IPlaceSystemインターフェースを実装するクラスを作成する
  - Enable、ManualUpdate、Disableメソッドの基本構造を実装する
  - 接続元ポールの状態を保持するフィールドを用意する
  - カメラとプレビューオブジェクトへの依存関係を設定する
  - _Requirements: 5.2, 5.3_

- [ ] 5.2 接続元選択機能の実装
  - 左クリック入力時にレイキャストでGearChainPoleを検出する
  - 検出したポールを接続元として記録する
  - 接続元選択時にプレビューオブジェクト（ラインと範囲）を有効化する
  - _Requirements: 1.2_

- [ ] 5.3 接続先選択と接続リクエスト送信機能の実装
  - 接続元選択後、毎フレームカーソル位置のポールを検出する
  - 検出したポールへの接続ラインプレビューを更新する
  - 接続可否を判定し、プレビューの色を適切に変更する
  - 左クリックで接続先ポールを選択し、接続リクエストをサーバーに送信する
  - 送信後に接続元選択をリセットし、プレビューを非表示にする
  - _Requirements: 1.3, 1.4, 2.2, 2.3, 2.4, 2.5_

- [ ] 5.4 選択解除機能の実装
  - 右クリックまたはESCキー入力を検出する
  - 入力検出時に接続元選択を解除し、プレビューを非表示にする
  - _Requirements: 1.5_

- [ ] 5.5 切断操作機能の実装
  - 既に接続済みのポール同士を選択した場合を検出する
  - 切断モードに切り替え、切断リクエストをサーバーに送信する
  - 切断完了後の状態リセット処理を実装する
  - _Requirements: 4.1, 4.2, 4.3_

- [ ] 5.6 エラーハンドリングの実装
  - サーバーからのエラーレスポンスを処理する機能を追加する
  - エラー内容に応じた通知UIへの表示を実装する
  - _Requirements: 3.4_

- [ ] 6. PlaceSystemSelectorへの統合
- [ ] 6.1 PlaceSystem選択ロジックの拡張
  - PlaceSystemSelectorにGearChainConnectPlaceSystemフィールドを追加する
  - コンストラクタ引数にGearChainConnectPlaceSystemを追加する
  - GetCurrentPlaceSystemのswitch文にGearChainConnect分岐を追加する
  - チェーンアイテム保持時にGearChainConnectPlaceSystemを返すようにする
  - _Requirements: 1.1, 5.1_

- [ ] 7. DI登録とマスターデータ設定
- [ ] 7.1 MainGameStarterへのDI登録追加
  - GearChainConnectPlaceSystemのRegister文を追加する
  - プレビューオブジェクト用のSerializeFieldを追加する
  - VContainerのbuilderに必要な依存関係を登録する
  - _Requirements: 5.1_

- [ ] 7.2 チェーンアイテムのPlaceSystem設定追加
  - placeSystem.jsonにチェーンアイテムの設定を追加する
  - usePlaceItemsにチェーンアイテムのGUIDを設定する
  - PlaceModeをGearChainConnectに設定する
  - _Requirements: 1.1, 5.1_

- [ ] 7.3 ユーザー向けPrefab設定手順の確認
  - GameSystem.prefabにプレビューオブジェクトを配置するための手順を文書化する
  - GearChainPoleブロックPrefabにColliderコンポーネントを追加するための手順を確認する
  - ユーザーが実施すべきPrefab編集作業を明確にする
  - _Requirements: 1.2, 2.1_

- [ ] 8. 統合テストと動作確認
- [ ] 8.1 PlaceSystem選択の統合テスト
  - チェーンアイテム保持時にGearChainConnectPlaceSystemが選択されることを確認するテストを作成する
  - アイテム変更時に適切にPlaceSystemが切り替わることを確認する
  - _Requirements: 1.1, 5.1, 5.3_

- [ ] 8.2 接続操作の統合テスト
  - 接続元選択、接続先選択、接続完了の一連フローをテストする
  - 接続範囲外のポールへの接続が拒否されることを確認する
  - 既存接続の切断操作が正しく動作することを確認する
  - _Requirements: 1.2, 1.3, 1.4, 1.5, 4.1, 4.2, 4.3_

- [ ] 8.3 コンパイル確認と最終検証
  - 全ての変更が正しくコンパイルされることを確認する
  - MCPツールを使用してクライアント側のテストを実行する
  - _Requirements: 全要件_
