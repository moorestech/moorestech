# Requirements Document

## Introduction
本機能は、歯車システムにおいて特定の歯車ネットワークを流れるRPMおよびトルクが閾値を超過した場合に、ネットワーク内のGearEnergyTransform系ブロックが一定の確率と時間条件に基づき破壊される挙動を提供する。破壊は「過負荷による破損」と「プレイヤーによる手動撤去」を区別して扱われ、閾値・確率・時間・倍率といった動作パラメーターはマスターデータによって定義可能とする。

## Requirements

### Requirement 1: 歯車ネットワーク過負荷検知
**Objective:** As a 工場設計プレイヤー, I want 歯車ネットワークがRPMとトルクの過負荷状態を検知して扱えること, so that 過負荷条件下での挙動を予測・制御できる。

#### Acceptance Criteria
1. The Gear Network Overload System shall 管理対象となる歯車ネットワークごとにRPMとトルクの現在値を継続的に把握する。
2. The Gear Network Overload System shall 各歯車ネットワークに対してRPMの許容閾値とトルクの許容閾値をマスターデータから取得して評価する。
3. When 歯車ネットワークのRPM現在値がRPM許容閾値を超過した場合, the Gear Network Overload System shall そのネットワークを「RPM過負荷状態」としてマークする。
4. When 歯車ネットワークのトルク現在値がトルク許容閾値を超過した場合, the Gear Network Overload System shall そのネットワークを「トルク過負荷状態」としてマークする。
5. While 歯車ネットワークがRPM過負荷状態またはトルク過負荷状態である間, the Gear Network Overload System shall 過負荷状態の継続時間を計測し続ける。
6. If 歯車ネットワークがRPM過負荷状態でもトルク過負荷状態でもない場合, then the Gear Network Overload System shall 当該ネットワークに対する過負荷状態のフラグと継続時間をリセットする。

### Requirement 2: 破壊パラメーターと確率計算
**Objective:** As a バランス調整担当者, I want 過負荷破壊の閾値や確率をデータ駆動で設定できること, so that 歯車ネットワークのリスクとリワードを柔軟に調整できる。

#### Acceptance Criteria
1. The Gear Network Overload System shall 過負荷破壊に関するRPM閾値、トルク閾値、判定時間間隔、基礎破壊確率、RPM倍率係数、トルク倍率係数をマスターデータから取得できる。
2. When 過負荷破壊パラメーターがマスターデータに設定されている歯車ネットワークに対して破壊判定を行う場合, the Gear Network Overload System shall RPM超過倍率を「RPM現在値 ÷ RPM許容閾値」として算出する。
3. When 過負荷破壊パラメーターがマスターデータに設定されている歯車ネットワークに対して破壊判定を行う場合, the Gear Network Overload System shall トルク超過倍率を「トルク現在値 ÷ トルク許容閾値」として算出する。
4. When RPM現在値がRPM許容閾値を超過している場合, the Gear Network Overload System shall RPM超過倍率にRPM倍率係数を乗算してRPM由来の破壊確率倍率を求める。
5. When トルク現在値がトルク許容閾値を超過している場合, the Gear Network Overload System shall トルク超過倍率にトルク倍率係数を乗算してトルク由来の破壊確率倍率を求める。
6. If RPM現在値がRPM許容閾値を超過していない場合, then the Gear Network Overload System shall RPM由来の破壊確率倍率を1として扱う。
7. If トルク現在値がトルク許容閾値を超過していない場合, then the Gear Network Overload System shall トルク由来の破壊確率倍率を1として扱う。
8. When RPM由来の破壊確率倍率とトルク由来の破壊確率倍率が決定された場合, the Gear Network Overload System shall 基礎破壊確率に両倍率を乗算した値を当該判定タイミングにおける最終破壊確率として用いる。

### Requirement 3: 過負荷破壊トリガー
**Objective:** As a 工場設計プレイヤー, I want 過負荷状態が続くと一定間隔で破壊判定が行われること, so that 持続的な過負荷が具体的なリスクとして表現される。

#### Acceptance Criteria
1. While 歯車ネットワークがRPM過負荷状態またはトルク過負荷状態である間, the Gear Network Overload System shall マスターデータで定義された判定時間間隔ごとに破壊判定をスケジュールする。
2. When 判定時間間隔が経過した時点で歯車ネットワークが依然として過負荷状態である場合, the Gear Network Overload System shall Requirement 2で定義された方法に従って当該タイミングの最終破壊確率を算出する。
3. When 判定時間間隔が経過した時点で歯車ネットワークが過負荷状態ではない場合, the Gear Network Overload System shall 当該タイミングにおける破壊判定を実施しない。
4. When 最終破壊確率に基づく乱数判定が成功した場合, the Gear Network Overload System shall 対象ネットワーク内のGearEnergyTransform系ブロックのうち少なくとも1つを「過負荷破壊」として除去対象に選択する。
5. If 過負荷破壊対象とするGearEnergyTransform系ブロックがネットワーク内に存在しない場合, then the Gear Network Overload System shall その判定タイミングにおける破壊処理をスキップする。

### Requirement 4: ブロック破壊と破壊タイプ
**Objective:** As a ゲームプレイヤー, I want 過負荷による破壊と手動撤去を区別して扱えること, so that 破壊原因を理解し適切に対処できる。

#### Acceptance Criteria
1. The Gear Network Overload System shall ブロック破壊の種別として少なくとも「過負荷による破損」と「プレイヤーによる手動撤去」を区別して保持できる。
2. When 過負荷破壊判定によりGearEnergyTransform系ブロックを除去する場合, the Gear Network Overload System shall 当該ブロックに対する破壊タイプを「過負荷による破損」として指定する。
3. When プレイヤーの操作によってGearEnergyTransform系ブロックが撤去される場合, the Gear Network Overload System shall 当該ブロックに対する破壊タイプを「プレイヤーによる手動撤去」として指定する。
4. When いずれかの破壊タイプでブロック除去が要求された場合, the World Block Removal System shall 対象位置のブロックをワールドから除去し、その破壊タイプ情報を受け取って処理できる。
5. If ブロック除去要求が無効な座標または存在しないブロックに対して行われた場合, then the World Block Removal System shall ブロックの実体を変更しない。

### Requirement 5: GearEnergyTransformブロックとの連携
**Objective:** As a システム設計者, I want GearEnergyTransform系ブロックが過負荷破壊ロジックと一貫して連携すること, so that 歯車ネットワークの振る舞いを統一的に扱える。

#### Acceptance Criteria
1. The Gear Network Overload System shall GearEnergyTransform系ブロックが属する歯車ネットワークを特定し、そのネットワークに対してRequirement 1〜3で定義された過負荷判定を適用する。
2. When GearEnergyTransform系ブロックが属する歯車ネットワークで過負荷破壊が発生した場合, the Gear Network Overload System shall そのネットワーク内のGearEnergyTransform系ブロックから破壊対象ブロックを決定する。
3. When GearEnergyTransform系ブロックが過負荷破壊により除去対象に選択された場合, the Gear Network Overload System shall Requirement 4で定義された破壊タイプを指定してWorld Block Removal Systemにブロック除去を依頼する。
4. If GearEnergyTransform系ブロックが過負荷破壊により除去された場合, then the Gear Network Overload System shall 当該ブロックが属していた歯車ネットワークの構造変化を次回以降の過負荷判定に反映する。

### Requirement 6: 可観測性とデバッグ
**Objective:** As a デバッグ担当者, I want 過負荷判定や破壊発生状況を確認できること, so that トラブルシュートやバランス調整を効率的に行える。

#### Acceptance Criteria
1. The Gear Network Overload System shall 過負荷状態の開始と終了、判定時間間隔ごとの破壊判定実施有無、破壊確率、および破壊発生結果を内部的に記録できる。
2. When デバッグモードまたは同等の観測手段が有効化されている場合, the Gear Network Overload System shall 主要な過負荷イベント（過負荷状態への遷移、破壊判定実施、ブロック破壊発生）を識別可能な形で出力できる。
3. If デバッグモードが無効化されている場合, then the Gear Network Overload System shall プレイヤー体験に不要なログや可視情報を出力しない。

