# Requirements Document

## Introduction

GearChainPoleのConnect機能をクライアント側で操作するためのPlaceSystemを作成する。

GearChainPoleは、チェーンアイテムを消費して離れた2つのギアポール間を接続し、ギアネットワークを拡張するためのブロックである。現在、サーバー側のプロトコル（`GearChainConnectionEditProtocol`）とビジネスロジック（`GearChainSystemUtil`）は実装済みだが、クライアント側でプレイヤーがこの接続を操作するためのPlaceSystemが未実装である。

本仕様では、既存のTrainRailConnectSystemの2ステップ接続パターンを参考に、GearChainPole専用のConnectPlaceSystemを実装する。

## Requirements

### Requirement 1: GearChainPoleの接続操作UI

**Objective:** As a プレイヤー, I want GearChainPole間を視覚的に接続できるようにしたい, so that ギアネットワークを効率的に構築できる

#### Acceptance Criteria
1. When プレイヤーがチェーンアイテムを手に持っている, the PlaceSystemStateController shall GearChainConnectPlaceSystemを有効にする
2. When プレイヤーがGearChainPoleを左クリックで選択する, the GearChainConnectPlaceSystem shall 選択したポールを接続元として記録する
3. While 接続元ポールが選択されている, the GearChainConnectPlaceSystem shall 接続元から現在のマウスカーソル位置までの接続プレビューを表示する
4. When プレイヤーが別のGearChainPoleを左クリックで選択する, the GearChainConnectPlaceSystem shall 接続元と接続先の2つのポール間で接続リクエストを送信する
5. When 右クリックまたはESCキーが押される, the GearChainConnectPlaceSystem shall 接続元の選択を解除する

### Requirement 2: 接続範囲の視覚的フィードバック

**Objective:** As a プレイヤー, I want 接続可能な範囲と状態を視覚的に確認したい, so that 無効な操作を事前に回避できる

#### Acceptance Criteria
1. While 接続元ポールが選択されている, the GearChainConnectPlaceSystem shall 接続元の最大接続距離（maxConnectionDistance）を視覚的に表示する
2. When マウスカーソルが別のGearChainPole上にある, the GearChainConnectPlaceSystem shall 接続可否に応じて接続ラインの色を変化させる（接続可能：緑、接続不可：赤）
3. If 対象ポールが接続範囲外にある, then the GearChainConnectPlaceSystem shall 接続不可としてラインを赤で表示する
4. If 対象ポールの接続数が上限に達している, then the GearChainConnectPlaceSystem shall 接続不可としてラインを赤で表示する
5. If 既に接続済みのポール同士を選択している, then the GearChainConnectPlaceSystem shall 既存接続として別の色（黄色など）でラインを表示する

### Requirement 3: サーバー通信とレスポンス処理

**Objective:** As a システム, I want 接続・切断リクエストを正しくサーバーに送信したい, so that ゲーム状態が同期される

#### Acceptance Criteria
1. When 接続リクエストが送信される, the VanillaApiSendOnly shall GearChainConnectionEditRequestをConnectモードで送信する
2. When 切断リクエストが送信される, the VanillaApiSendOnly shall GearChainConnectionEditRequestをDisconnectモードで送信する
3. The VanillaApiSendOnly shall リクエストに接続元位置、接続先位置、プレイヤーID、チェーンアイテムIDを含める
4. If サーバーからエラーレスポンスを受信した, then the GearChainConnectPlaceSystem shall エラー内容を通知UIに表示する

### Requirement 4: 既存接続の切断操作

**Objective:** As a プレイヤー, I want 既存のGearChainPole間接続を切断したい, so that ギアネットワークを再構成できる

#### Acceptance Criteria
1. When プレイヤーが既存接続ラインを持つ2つのポールを選択する, the GearChainConnectPlaceSystem shall 接続の切断確認UIを表示するか、切断モードへ切り替える
2. When 切断が確定される, the GearChainConnectPlaceSystem shall 切断リクエストをサーバーに送信する
3. When 切断が完了する, the GearChainConnectPlaceSystem shall 消費されていたチェーンアイテムがプレイヤーインベントリに返却されることを確認する

### Requirement 5: PlaceSystemの登録とアイテム関連付け

**Objective:** As a システム, I want GearChainConnectPlaceSystemを適切なアイテムと関連付けたい, so that 正しいコンテキストでシステムが有効になる

#### Acceptance Criteria
1. The PlaceSystemSelector shall チェーンアイテム（gearChainItemsに定義されたアイテム）保持時にGearChainConnectPlaceSystemを選択する
2. The GearChainConnectPlaceSystem shall IPlaceSystemインターフェースを実装する（Enable, ManualUpdate, Disableメソッド）
3. When 手持ちアイテムがチェーンアイテムから変更される, the PlaceSystemStateController shall GearChainConnectPlaceSystemを無効にして接続元選択をリセットする

### Requirement 6: コスト表示

**Objective:** As a プレイヤー, I want 接続に必要なチェーンアイテム数を確認したい, so that インベントリを適切に準備できる

#### Acceptance Criteria
1. While 接続プレビューが表示されている, the GearChainConnectPlaceSystem shall 接続に必要なチェーン消費量を計算して表示する
2. The GearChainConnectPlaceSystem shall 消費量を「距離 / consumptionPerLength（切り上げ）」で計算する
3. If プレイヤーのインベントリに必要なチェーンアイテムが不足している, then the GearChainConnectPlaceSystem shall 不足を視覚的に警告する（例：赤色で表示）
