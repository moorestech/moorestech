# Requirements Document

## Introduction
チェーンエネルギー伝送システムは、離れた場所にある機械や発電機同士をチェーンを用いて接続し、回転エネルギー（GearPower）を伝達するための機能です。
既存の歯車システム（GearNetwork）を拡張し、物理的に隣接していないブロック間でのエネルギー共有を実現します。
本機能は、新たなブロック「チェーンポールブロック」と、それらを接続・切断するための通信プロトコル、およびエネルギー伝送ロジックから構成されます。

## Requirements

### Requirement 1: Master Data Definition
**Objective:** 開発者として、ゲーム内でインスタンス化できるようにチェーンポールブロックをマスターデータに定義したい。

#### Acceptance Criteria
1. **チェーンポールブロックは、チェーン接続制限（最大距離など）のプロパティを含み、ブロックマスターデータ（`blocks.yml`）で定義されるものとする。**
2. **チェーンポールブロックは、ギアネットワーク内での振る舞いを定義するために、標準的な`Gear`コンポーネントプロパティを利用するものとする。**

### Requirement 2: Chain Connection Management
**Objective:** プレイヤーとして、エネルギー伝送ラインを構築するために、チェーンポールブロックをチェーンで接続および切断したい。

#### Acceptance Criteria
1. **プレイヤーが2つの`ChainPoleBlock`インスタンスを指定して「チェーン接続」リクエストを送信したとき、`ChainSystem`は接続を検証するものとする。**
   - バリデーション基準:
     - 両方のターゲットが有効な`ChainPoleBlock`であること。
     - 距離が許容最大範囲内であること。
     - 視線が遮られていないこと（オプション、設計判断によるが、ここでは単純な距離を想定）。
     - 両方のブロックに空き接続スロットがあること。
2. **接続検証が失敗した場合、`ChainSystem`はクライアントにエラーレスポンスを返すものとする。**
3. **有効な接続が確立されたとき、`ChainSystem`は両方の`ChainPoleBlock`の状態を更新し、互いに参照するようにするものとする。**
4. **プレイヤーが既存の接続に対して「チェーン切断」リクエストを送信したとき、`ChainSystem`は両方のブロックから参照を削除するものとする。**
5. **`ChainSystem`は、接続成功時にプレイヤーのインベントリから「チェーン」アイテムを消費するものとする（アイテムが必要な場合）。**

### Requirement 3: Energy Transmission Logic
**Objective:** プレイヤーとして、遠隔の機械に電力を供給するために、回転エネルギーがチェーンを介して伝送されるようにしたい。

#### Acceptance Criteria
1. **2つの`ChainPoleBlock`が接続されている間、`GearNetwork`はそれらを機械的に結合されたコンポーネントとして扱うものとする。**
2. **`GearNetwork`は、接続された`ChainPoleBlock`間でRPMとトルクを1:1の比率で伝達するものとする。**
3. **`GearNetwork`は、チェーン接続を通じて同じ回転方向（時計回り/反時計回り）を維持するものとする。**
4. **`GearNetwork`が接続コンポーネントを走査するとき（`GetGearConnects`など）、`ChainPoleBlock`は接続されたパートナーを有効なギア接続として返すものとする。**

### Requirement 4: Network Protocol
**Objective:** クライアントとして、ゲームの状態が同期されるように、接続変更をサーバーに伝えたい。

#### Acceptance Criteria
1. **`ServerProtocol`は、接続リクエストを処理するための`ConnectChainProtocol`を定義するものとする。**
2. **`ServerProtocol`は、切断リクエストを処理するための`DisconnectChainProtocol`を定義するものとする。**
3. **チェーン接続状態が変化したとき、`ServerProtocol`は関連するすべてのクライアントに更新イベントをブロードキャストするものとする。**
4. **`ClientNetwork`は、チェーンの視覚表現を更新するためにこれらのイベントを処理するものとする。**
