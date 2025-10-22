# 実装計画

## 実装完了サマリー

すべてのタスクが完了し、全テスト（15/15）がパスしました。

### 実装内容
- エネルギー不足判定機能（CalculateEnergyBalance）
- ネットワーク停止処理（HaltNetworkForEnergyDeficit）
- 5つの新規テストケース追加
- 2つの既存テストを新仕様に合わせて更新

### テスト結果
- 新規テスト: 5/5 パス
- 既存テスト: 10/10 パス（2つは新仕様に合わせて更新）
- 合計: 15/15 パス

## 1. エネルギー不足判定ロジックの実装

- [x] 1.1 GearNetwork.ManualUpdate()にエネルギー収支判定機能を追加
  - ロック検知後、動力分配前のタイミングにエネルギー判定処理を挿入
  - 既存の`DistributeGearPower()`から総必要ギアパワーと総生成ギアパワーの計算ロジックを抽出
  - 総必要ギアパワー > 総生成ギアパワーの条件でエネルギー不足を判定
  - エネルギー不足時とエネルギー充足時で処理フローを分岐
  - _要求事項: 1.1, 1.2, 1.3_ ✓

- [x] 1.2 エネルギー不足時の停止処理を実装
  - すべてのGearTransformerに対してRPM=0、Torque=0を供給する処理
  - すべてのGearGeneratorに対してRPM=0を供給する処理
  - GearNetworkInfoのOperatingRateを0に設定
  - 総必要ギアパワーと総生成ギアパワーをGearNetworkInfoに記録
  - _要求事項: 2.1, 2.2, 3.1, 3.2_ ✓

## 2. コードの可読性とパフォーマンスの最適化

- [x] 2.1 #regionとローカル関数で処理を整理
  - エネルギー判定ロジックをローカル関数として実装
  - エネルギー不足時の停止処理をローカル関数として実装
  - #region Internalブロック内に新規ローカル関数を配置
  - メインフローが一目で理解できるように整理
  - _要求事項: 5.1, 5.2_ ✓

- [x] 2.2 既存のDistributeGearPower()を再利用するよう調整
  - エネルギー充足時は既存のDistributeGearPower()を呼び出し
  - エネルギー不足時はDistributeGearPower()をスキップ
  - 総必要ギアパワーと総生成ギアパワーの計算結果を再利用
  - 既存のrpmRate計算ロジックを維持
  - _要求事項: 2.4, 4.3, 4.4, 5.3_ ✓

## 3. 既存動作との整合性確保

- [x] 3.1 ロック検知の優先順位を維持
  - ロック検知がエネルギー判定より先に実行されることを確認
  - ロック状態の場合はエネルギー判定をスキップ
  - 既存のSetRocked()処理が正しく動作することを確認
  - _要求事項: 4.1_ ✓

- [x] 3.2 ジェネレーター不在時の既存動作を維持
  - ジェネレーターが存在しない場合の処理フローを確認
  - すべてのコンポーネントにRPM=0、Torque=0を供給する既存動作を維持
  - GearNetworkInfo.CreateEmpty()が正しく呼び出されることを確認
  - _要求事項: 4.2_ ✓

## 4. テストコードの実装

- [x] 4.1 エネルギー不足時の停止テストを作成
  - SimpleGearGeneratorと高負荷歯車を配置してエネルギー不足状態を作成
  - ManualUpdate()を呼び出し、すべてのコンポーネントのCurrentRpmが0であることを検証
  - CurrentGearNetworkInfo.OperatingRateが0であることを検証
  - TotalRequiredGearPowerがTotalGenerateGearPowerより大きいことを検証
  - _要求事項: 1.2, 2.1, 2.2, 2.3, 3.2_ ✓

- [x] 4.2 エネルギー回復時の通常動作再開テストを作成
  - エネルギー不足状態を作成後、高負荷歯車を削除してエネルギー充足状態に変更
  - ManualUpdate()を呼び出し、GearTransformer/GeneratorのCurrentRpmが0より大きいことを検証
  - CurrentGearNetworkInfo.OperatingRateが0より大きいことを検証
  - 通常の動力分配処理が正しく実行されることを確認
  - _要求事項: 2.4, 3.3_ ✓

- [x] 4.3 ロック検知優先度テストを作成
  - RPM矛盾を含むネットワークを構築し、同時にエネルギー不足も発生させる
  - ManualUpdate()を呼び出し、すべてのコンポーネントのIsRockedがtrueであることを検証
  - ロック状態がエネルギー不足判定より優先されることを確認
  - CurrentGearNetworkInfo.OperatingRateが0であることを検証（ロック状態の結果）
  - _要求事項: 4.1_ ✓

- [x] 4.4 ジェネレーター不在時の既存動作維持テストを作成
  - ジェネレーターなしでGearTransformerのみ配置
  - ManualUpdate()を呼び出し、すべてのGearTransformerのCurrentRpmが0であることを検証
  - CurrentGearNetworkInfoがCreateEmpty()の結果と一致することを検証
  - 既存動作が変更されていないことを確認
  - _要求事項: 4.2_ ✓

- [x] 4.5 境界値テストを作成
  - 必要ギアパワーと生成ギアパワーが完全に等しいネットワークを構築
  - ManualUpdate()を呼び出し、GearTransformer/GeneratorのCurrentRpmが0より大きいことを検証
  - CurrentGearNetworkInfo.OperatingRateが1.0であることを検証
  - エネルギー充足として正しく扱われることを確認
  - _要求事項: 1.3_ ✓

## 5. 既存テストの回帰確認とコンパイル検証

- [x] 5.1 既存の全テストケースを実行
  - GearNetworkTestの全テストケースが正常に動作することを確認
  - SimpleGeneratorAndGearRpmTest、LoopGearNetworkTest等の既存テストをパス
  - **回帰検出**: ServeTorqueOverTest、TorqueHalfTestが失敗
    - 原因: これらのテストはエネルギー不足状態（required > generated）を想定しているが、既存実装ではRPM減速、新実装では完全停止
    - 要求仕様に従えば、エネルギー不足時は完全停止が正しい動作
    - 既存テストの期待値を更新する必要がある
  - _要求事項: 4.3, 5.4_

- [x] 5.2 コンパイルエラーの確認と修正
  - MCPツールでサーバー側のコンパイルを実行
  - コンパイルエラーなし
  - 既存の命名規則とコーディングスタイルに従っていることを確認
  - _要求事項: 5.4_
