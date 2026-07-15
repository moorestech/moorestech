# 歯車ネットワーク状態通知の差分化（FPS改善）

## 背景（実測済みの因果チェーン）

実プレイでfpsが10〜20に落ち、Profilerの`UpdateFunction.Invoke()`が27〜690msのスパイクを起こす。ランタイム計測で以下を確定済み：

1. サーバー側: 巨大歯車ネットワーク（transformers=5,414 + generators=20）が燃料発電機（`ContinuousTickNetworks`）の出力変動により実質毎tick再計算対象になる
2. `GearTickUpdater.cs:63-71`が再計算networkの**全メンバー**に無条件で`NotifyStateChanged()`を呼ぶ → 毎tick約6,240件・毎秒約12.5万件の`va:event:changeBlockState`イベントが発生（1件ずつ座標付きタグで個別MessagePackシリアライズ）
3. クライアント側: 50msポーリング（`VanillaApiEvent`）の応答が`SwitchToMainThread`経由でUniTask YieldUpdateに載り、**1フレームで12,000〜18,700件（570〜860KB）を同期ディスパッチ**して実測28〜270ms消費。これがfps低下・不安定の主因

## 修正方針

per-gearの変化検知：`SimpleGearService.NotifyStateChanged()`で、クライアント可視状態（`GetBlockStateDetail`がシリアライズする isClockwise / rpm / torque の3値）が前回通知時から変化していない場合は発火をスキップする。定常運転中の歯車は値が不変なので、イベント量が桁で減る。

**codebase内前例**: `GearEnergyTransformerComponent.SetTorqueRequestRate`（`Mathf.Approximately`で変化時のみ`NotifyConsumerDemandChanged`）。「変化を起こす操作の直後に、実変化があった場合のみプッシュ」パターンの踏襲。

**安全性の根拠（調査済み）**:
- クライアントの初期状態はイベントではなく`BlockGameObject.SubscribeBlockState`内の`VanillaApi.SendOnly.InvokeBlockState(pos)`によるpull（サーバー側`InvokeBlockStateEventProtocol` → `ChangeBlockStateEventPacket.ChangeState`直呼び）で取得している。この経路は`SimpleGearService.NotifyStateChanged`を通らないため、差分化しても新規接続・ブロック設置直後の初期状態取得は壊れない
- `OnGearUpdate`の購読者は`GearChainPoleComponent.cs:49`のみで、用途は自身のクライアントイベント転送だけ。同時にgateしてよい

## Global Constraints

- 変更対象は**サーバー側の通知経路のみ**（`SimpleGearService`中心）。プロトコル・スキーマ・クライアントコードは変更しない
- `InvokeBlockStateEventProtocol`による初期状態pull経路の挙動は不変であること
- `GearTickUpdater` / `GearNetworkPowerCalculator`のtickフロー自体は変更しない（gateは`SimpleGearService`内部に置く）
- float比較は`Mathf.Approximately`（前例踏襲）
- partial禁止 / 1ファイル200行以下 / 主要処理にJP+EN 2行セットコメント / try-catch禁止 / 単純getter/setter禁止（値のSetは`SetHoge`メソッド）/ デフォルト引数禁止
- .cs変更後は`uloop compile --project-path ./moorestech_client`で0エラー確認（darwin、リポジトリルートは/Users/katsumi/moorestech）
- テストは`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "..."`で対象限定実行。サーバー側テストもこのコマンドで実行される
- **コミットは今回変更した.csファイルのみ**。作業ツリーに既存のprefab変更（DebugObjects.prefab / PlayerSystem.prefab）があるが絶対にコミットしない
- uloopで「Unity is reloading (Domain Reload in progress)」エラーが出たら45秒待ってリトライ

## Task 1: SimpleGearService.NotifyStateChanged の変化時のみ発火化

### 実装

対象: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Gear/SimpleGearService.cs`

- 最後に通知したクライアント可視状態（isClockwise: bool, rpm: float, torque: float）と「一度でも通知したか」フラグをフィールドで保持する
- `NotifyStateChanged()`の先頭で現在値を導出し（既存の`IsCurrentClockwise` / `CurrentRpm` / `CurrentTorque`プロパティを使ってよい）、通知済みかつ3値すべて前回一致（rpm/torqueは`Mathf.Approximately`、isClockwiseは`==`）なら`_onBlockStateChange`も`_onGearUpdate`も発火せずreturnする
- 変化があれば保持値を更新して従来どおり両Subjectを発火する
- 初回（未通知）は必ず発火する

### テスト（TDD: 先に失敗するテストを書く）

- 既存の歯車系サーバーテスト（`moorestech_server/Assets/Scripts/Tests.*`配下、例: `Gear`を含むテストクラス）を検索し、その初期化パターン・ブロック設置パターン・tick進行方法（`GameUpdater`系ヘルパー）に**従うこと**。新規テストクラスを作る場合は同ディレクトリ・同命名規約
- 検証方法: `ServerContext.WorldBlockDatastore.OnBlockStateChange`（または対象ブロックコンポーネントの`BlockStateObservable`）をSubscribeして発火回数をカウントする
- ケース1（差分化の本体）: generator+歯車数個の小規模ネットワークを構築し、回転が安定した後の追加tickでは対象歯車の状態変化イベントが発火しないこと
- ケース2（変化時は発火）: generator出力またはconsumer要求トルクを変化させた直後のtickでは発火すること
- ケース3（初回通知）: ネットワーク構築後の最初の計算tickでは必ず1回発火すること
- 実装完了後、`--filter-value "Gear"`で歯車系テスト一式がグリーンであることを確認する

## Task 2: ChangeBlockStateEventPacket のペイロード差分ブロードキャスト化

### 背景（Task 1後の実測）

Task 1で歯車のイベントは激減した（62.3万→7.5万件/5秒）が、毎tick約750件が残存し15〜37msのヒッカップが継続している。残存源の実測内訳（8秒集計）: 鉄のパイプ73,075件/161block（`FluidPipeComponent`が受入時+転送時に毎tick発火）、石窯等の加工機械（`VanillaMachineProcessorComponent.Update`がProcessing中毎tick+`SupplyPower`がIdle中毎tick発火）、蒸気機関・燃料式風車（generatorが`changed || rpm>0`で毎tick発火）。

コンポーネント個別対処ではなく、**イベント層の一点**（`moorestech_server/Assets/Scripts/Server.Event/EventReceive/ChangeBlockStateEventPacket.cs`）で「シリアライズ済みペイロードが前回ブロードキャストとバイト列一致ならスキップ」する。全コンポーネント種別を恒久的にカバーし、値が本当に変化した場合（流体量の変動・加工進捗等）は従来どおり送信される。

### 実装

- `ChangeBlockStateEventPacket`に、ブロック位置（`Vector3Int`）→最後にブロードキャストしたペイロード（`byte[]`）のDictionaryを保持する
- 購読経路（`WorldBlockDatastore.OnBlockStateChange`→`ChangeState`）では、シリアライズ後に前回ペイロードとバイト列比較（`ReadOnlySpan<byte>.SequenceEqual`等）し、一致ならAddBroadcastEventせずreturn。不一致なら記録を更新して送信する
- **初期状態pull経路（`InvokeBlockStateEventProtocol`から直接呼ばれる方）は必ず送信する**（差分スキップ禁止）。強制送信用の別メソッド（例: `ForceChangeState`）を用意し、`InvokeBlockStateEventProtocol`の呼び出しをそちらに変更する。強制送信時もDictionaryは更新する
- ブロック削除時のDictionaryエントリ掃除: `WorldBlockDatastore`にブロック削除のObservable（`OnBlockRemoved`相当）があればSubscribeして該当位置を削除する。無ければ設置時の上書きに任せてよい（その場合は理由をコメントで明記）
- デフォルト引数禁止のため、bool引数のデフォルト値で分岐させない（メソッドを分けるか、呼び出し側で明示する）

### テスト（TDD: 先に失敗するテストを書く）

- 既存のイベント系サーバーテスト（`ChangeBlockState`や`EventProtocol`を含む既存テストを検索）の初期化・イベント取得パターンに従う
- ケース1: 同一状態のブロックが2回`OnBlockStateChange`を発火しても、イベントキューに積まれるのは1回であること（`EventProtocolProvider`経由でイベント取得して件数確認）
- ケース2: 状態（ペイロード）が変化した場合は2回とも積まれること
- ケース3: 強制送信経路（`InvokeBlockStateEventProtocol`相当の呼び出し）は同一ペイロードでも必ず積まれること
- 実装完了後、`--filter-value "Event|BlockState"`等で関連テスト一式がグリーンであることを確認する

## 検証（コントローラ実施・subagent対象外）

Task 1完了後、コントローラが実機検証する：
1. `uloop compile`後、PlayModeを起動し実プレイ環境（巨大工場セーブ）をロード
2. ランタイム注入計測で (a) 1ポーリングあたりの`changeBlockState`イベント件数（ベースライン: 6,237件/tick・12k〜18k件/フレーム）(b) 平均fps（ベースライン: 10〜20）(c) YieldUpdateスパイク（ベースライン: 27〜690ms）を再計測
3. 成功条件: 定常状態のイベント件数が桁で減少し、fpsがベースラインから明確に改善していること
