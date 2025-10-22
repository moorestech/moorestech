# 要求仕様書

## はじめに
歯車ネットワークシステムにおいて、ネットワーク全体の必要エネルギー（消費電力）が生成エネルギーを上回った場合に、ネットワーク全体を停止させるエネルギー管理機能を追加します。この機能により、エネルギー不足時の動作を明確化し、プレイヤーに適切なフィードバックを提供します。

現在の実装では、`GearNetwork.ManualUpdate()`メソッド内の`DistributeGearPower()`において、必要トルクが供給量を上回る場合にRPMを減速させる処理が行われていますが、完全停止の仕様は実装されていません。本機能では、エネルギー不足時にネットワーク全体を停止状態にする新たな動作を追加します。

## 要求事項

### 要求1: エネルギー不足判定
**目的:** システム管理者として、歯車ネットワークのエネルギー収支を正確に判定し、不足状態を検知できるようにしたい。これにより、ネットワークの停止条件を適切に決定できる。

#### 受入基準
1. WHEN 歯車ネットワークの更新処理が実行される THEN GearNetworkシステム SHALL 総必要ギアパワー（totalRequiredGearPower）と総生成ギアパワー（totalGeneratePower）を計算する
2. WHEN 総必要ギアパワーが総生成ギアパワーを上回る THEN GearNetworkシステム SHALL エネルギー不足状態として判定する
3. WHEN 総必要ギアパワーが総生成ギアパワー以下である THEN GearNetworkシステム SHALL エネルギー充足状態として判定する
4. IF エネルギー不足状態である AND 前回の判定が充足状態であった THEN GearNetworkシステム SHALL 状態変化を記録する

### 要求2: ネットワーク全体停止
**目的:** プレイヤーとして、エネルギー不足時にネットワーク全体が停止することで、エネルギー管理の重要性を理解し、適切な対応を取れるようにしたい。

#### 受入基準
1. WHEN エネルギー不足状態が判定される THEN GearNetworkシステム SHALL ネットワーク内のすべてのGearTransformerに対してRPM=0、Torque=0を供給する
2. WHEN エネルギー不足状態が判定される THEN GearNetworkシステム SHALL ネットワーク内のすべてのGearGeneratorに対してRPM=0を供給する
3. WHEN ネットワークが停止状態である THEN すべての歯車コンポーネント SHALL 回転を停止する
4. WHEN エネルギー不足が解消される THEN GearNetworkシステム SHALL 通常の動力分配処理（DistributeGearPower）を再開する

### 要求3: ネットワーク状態情報の更新
**目的:** 開発者として、ネットワークの状態を`GearNetworkInfo`を通じて取得し、UI表示やデバッグに利用できるようにしたい。

#### 受入基準
1. WHEN ネットワークが停止状態になる THEN GearNetworkシステム SHALL `CurrentGearNetworkInfo`にエネルギー不足による停止状態を反映する
2. WHEN ネットワークが停止状態である THEN `CurrentGearNetworkInfo.OperatingRate` SHALL 0として設定される
3. WHEN ネットワークが通常動作している THEN `CurrentGearNetworkInfo.OperatingRate` SHALL 実際の稼働率（rpmRate）を反映する
4. WHERE デバッグ時やUI表示時 THE GearNetworkシステム SHALL `CurrentGearNetworkInfo`を通じてネットワークの詳細状態を提供する

### 要求4: 既存動作との整合性
**目的:** システム管理者として、新機能が既存のロック検知やRPM減速機能と矛盾なく動作することを保証したい。

#### 受入基準
1. WHEN 歯車のロックが検知される THEN GearNetworkシステム SHALL エネルギー不足判定よりも優先してロック状態を設定する
2. IF ジェネレーターが存在しない THEN GearNetworkシステム SHALL すべてのコンポーネントにRPM=0、Torque=0を供給する（既存動作を維持）
3. WHEN エネルギー充足状態である AND RPM減速が必要である THEN GearNetworkシステム SHALL 既存のrpmRate計算ロジックを使用する
4. WHEN エネルギー不足による停止が発生する THEN GearNetworkシステム SHALL 既存のDistributeGearPowerロジックを実行せず、直接停止処理を行う

### 要求5: パフォーマンスと保守性
**目的:** 開発者として、新機能が既存のパフォーマンスを損なわず、コードの保守性を維持できるようにしたい。

#### 受入基準
1. WHEN 新しいエネルギー判定ロジックが追加される THEN 実装 SHALL 既存の`ManualUpdate()`メソッドの処理フローに統合される
2. WHERE 複雑なロジックが実装される THE 実装 SHALL #regionとローカル関数を使用して可読性を確保する
3. WHEN エネルギー不足判定が実行される THEN 処理 SHALL 既存の`totalRequiredGearPower`と`totalGeneratePower`の計算結果を再利用する
4. IF 新しいメソッドやプロパティが追加される THEN 実装 SHALL 既存の命名規則とコーディングスタイルに従う
