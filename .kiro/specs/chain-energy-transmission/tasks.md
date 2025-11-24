# Implementation Plan

## Task Summary
- **Total Tasks**: 4 major tasks, 6 sub-tasks
- **Requirements Covered**: All requirements (1.1, 1.2, 2.1-2.5, 3.1-3.4, 4.1-4.4)
- **Estimated Effort**: 1-3 hours per sub-task

## Tasks

- [x] 1. マスターデータの定義とスキーマ更新
- [x] 1.1 (P) チェーンポールブロックの定義と生成
  - `VanillaSchema/blocks.yml`に`ChainPole`の`blockType`とパラメータを追加する
  - プロジェクトをビルドし、SourceGeneratorにより`ChainPoleBlock`のクラス定義を生成させる
  - `MasterHolder`が新しいブロックタイプを正しくロードできることを確認する
  - _Requirements: 1.1_

- [x] 1.2 (P) チェーンポールコンポーネントの実装
  - `Game.Block.Blocks.ChainPole`配下に`ChainPoleComponent`クラスを作成する
  - `IGearEnergyTransformer`インターフェースを実装し、GearNetworkに参加させる
  - `GetGearConnects`で接続されたチェーンポールを返すロジックを実装する（接続情報はまだ空でよい）
  - _Requirements: 1.2, 3.1, 3.4_

- [x] 2. プロトコルとシステム実装
- [x] 2.1 (P) チェーンシステムの実装
  - `Game.Context`配下に`ChainSystem`クラスを作成し、`IChainSystem`インターフェースを定義する
  - `TryConnect`メソッドを実装し、距離、視線、アイテム消費、既存接続のバリデーションを行う
  - `TryDisconnect`メソッドを実装し、接続解除のロジックを実装する
  - `ChainPoleComponent`の状態更新ロジックを統合する
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

- [x] 2.2 (P) 通信プロトコルの定義
  - `Server.Protocol`に`ConnectChainProtocol`クラスを作成し、接続リクエストを処理する
  - `Server.Protocol`に`DisconnectChainProtocol`クラスを作成し、切断リクエストを処理する
  - クライアントからのリクエスト（座標指定）を受け取り、`ChainSystem`に委譲する
  - _Requirements: 4.1, 4.2_

- [x] 3. エネルギー伝送ロジックの統合
- [x] 3.1 エネルギー伝送計算の実装
  - `ChainPoleComponent`の`IGearEnergyTransformer`実装を完成させ、`GearNetwork`が正しくRPMとトルクを計算できるようにする
  - 接続されたチェーンポール間で回転方向が維持されるようにロジックを調整する
  - `ChainSystem`での接続変更時に`GearNetwork`の再計算をトリガーする
  - _Requirements: 3.2, 3.3_

- [x] 4. テストとイベント同期
- [x] 4.1 (P) イベントブロードキャストとクライアント同期
  - 接続・切断成功時に、関連するクライアントへイベントをブロードキャストする処理を`Server.Protocol`に追加する
  - クライアント側でイベントを受信し、チェーンの視覚的状態を更新するハンドラを実装する（視覚実装はモックまたは簡易的なログ出力で確認）
  - _Requirements: 4.3, 4.4_
