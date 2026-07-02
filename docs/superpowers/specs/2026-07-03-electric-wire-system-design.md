# 電力ワイヤーシステム（明示的電線接続） 設計

## 背景・目的

現在の電力システムは「範囲ベースの自動接続」で、接続というデータ自体が存在しない。電柱（`IElectricTransformer`）の設置/撤去イベントのたびに周辺を全スキャンしてセグメント（`EnergySegment`）を作り直し（`ConnectElectricPoleToElectricSegment` / `DisconnectElectricPoleToFromElectricSegment` 等）、クライアントにも電線の描画は無い（範囲ボックス表示 `DisplayEnergizedRange` のみ）。

これを Satisfactory 型の**明示的ワイヤーグラフ**に置き換える。すべての電力接続（電柱⇔電柱、電柱⇔機械、機械⇔発電機など任意の電気系ブロック同士）が個別のワイヤー（エッジ）として存在し、設置時は自動でワイヤーが張られ、あとから任意の線を撤去・追加できる。セグメント＝ワイヤーグラフの連結成分となる。

## 決定事項サマリ

| 論点 | 決定 |
|---|---|
| 給電モデル | Satisfactory型：全接続が明示ワイヤー。機械・発電機同士の直結も可 |
| 機械/発電機設置時の自動接続 | 接続可能な最寄り電柱1本のみ |
| 電柱設置時の自動接続 | 接続可能な最寄り電柱1本＋範囲内の未接続機械/発電機全部 |
| 電線アイテム | 距離分消費。切断・ブロック撤去時に返却 |
| 電線不足時の設置 | 設置自体を不可にする（プレビューで赤＋必要数表示） |
| 接続数上限 | マスタで種別ごとに定義（`maxWireConnectionCount`） |
| 切断操作 | 電線ツール（電線アイテム所持）でワイヤーを直接クリック |
| 手動接続UX | レール式延長フルセット（起点選択→空きスペースで電柱自動設置＋接続→連続延長） |
| ワイヤーの見た目 | カテナリー曲線（垂れた電線）メッシュ |
| 後方互換 | 考慮しない（旧セーブデータの電力接続は復元されない） |

## データモデル

### ElectricWireConnectorComponent（新規・サーバー）

電柱・電気機械・発電機・採掘機・ポンプ等、`IElectricConsumer` / `IElectricGenerator` / `IElectricTransformer` を持つ全ブロックに追加する接続コンポーネント。`GearChainPoleComponent` と同型。

- 接続先を `Dictionary<BlockInstanceId, WireConnection>` で双方向保持（両端のコンポーネントが互いを参照）
- `WireConnection` は相手の `BlockInstanceId` と消費した電線アイテム（`ItemGuid`＋個数）を持つ。返却時はGUIDから `ItemId` を解決する（揮発intは保持しない）
- `TryAddWireConnection` / `TryRemoveWireConnection` で追加・削除。削除時は電線アイテムを返却
- ブロック撤去時は自分の全ワイヤーを切断して電線を返却

### マスタデータ（blocks.yml / items.yml）

電気系ブロックの各 `BlockParam` に追加：

- `maxWireConnectionCount`（integer）— ワイヤー接続本数の上限。電柱は多め（例:8）、機械・発電機は少なめ（例:1〜2）
- `maxWireLength`（number）— この端点から張れるワイヤーの最大長。接続可否は `min(両端のmaxWireLength)` で判定（`GearChainPole.MaxConnectionDistance` と同方式）

既存の電柱パラメータ `poleConnectionRange` / `poleConnectionHeightRange` / `machineConnectionRange` / `machineConnectionHeightRange` は「**設置時の自動接続の探索範囲**」として意味を再定義して存続する。自動接続候補は探索範囲内かつ `maxWireLength` 以内のもの。

電線アイテムは新規アイテムとして追加し、`consumptionPerLength`（距離あたり消費数）をマスタ定義する（歯車チェーンアイテムと同じ計算式）。ワイヤー長は両端ブロック中心間のユークリッド距離。

## セグメント管理の書き直し

現在の4イベントハンドラ（`ConnectElectricPoleToElectricSegment` / `ConnectMachineToElectricSegment` / `DisconnectElectricPoleToFromElectricSegment` / `DisconnectMachineFromElectricSegment`）と周辺サービス（範囲スキャンによるセグメント再構築）は廃止し、**ワイヤーグラフの連結成分＝セグメント**として管理する。

実装は歯車ネットワーク（`GearNetworkDatastore`）の方式を移植する：

- `Dictionary<BlockInstanceId, EnergySegment>` の逆引きマップ（現行の全セグメント線形走査を置き換え）
- ワイヤー追加時：両端の所属セグメントが 0個=新規作成 / 1個=join / 2個=Union-by-sizeマージ（大きい方に吸収）
- ワイヤー削除・ブロック撤去時：BFSで連結成分分解し、分断されていれば成分ごとに新セグメント生成
- ワイヤーを1本も持たない電気系ブロックは、自分だけの単独セグメントに所属する（発電機直結のみの構成も動くようにするため）

これにより「1機械が複数セグメントに所属し得る」という現行の既知問題（`IWorldEnergySegmentDatastore` のコメント参照）は構造的に消滅する。連結成分は必ず一意。

毎tickの供給計算（`EnergySegment.Update` の `powerRate` 比例配分）は無変更。電柱がセグメントの骨格である必要も無くなるため、`EnergySegment` の transformer/consumer/generator の区別は維持しつつ、所属管理だけグラフベースに変わる。

## サーバー処理

### 設置時の自動接続

通常のブロック設置プロトコルの処理内で行う。**検証をすべて先に完了させ、通った場合のみ状態を変更する**（`va:gearChainPoleExtend` 設計と同じ原則。失敗時は設置を含め一切の状態変更をしない）。

機械・発電機の設置：

1. 各電柱の `machineConnectionRange` に設置位置が入っており、`maxWireLength` 以内で、接続数上限に達していない電柱を列挙
2. 最寄り（中心間距離最小。同距離なら `BlockInstanceId` 昇順で決定的に選択）の1本を接続先とする
3. 候補が無ければ接続なしで設置（電線消費なし・孤立状態）
4. 候補がある場合、距離分の電線アイテム所持を検証。不足なら**設置自体を失敗**させる
5. 検証通過後：設置→ワイヤー追加→電線消費→セグメント更新

電柱の設置：

1. `poleConnectionRange` 内で接続可能な最寄り電柱1本を選択（機械と同じ規則）
2. `machineConnectionRange` 内の**どのブロックともワイヤー接続されていない**機械/発電機を全列挙（自分の接続数上限まで。上限超過分は近い順に優先）
3. 1と2の合計ワイヤーの電線コストを算出し、所持数を検証。不足なら**設置自体を失敗**させる
4. 検証通過後：設置→全ワイヤー追加→電線消費→セグメント更新

### 手動接続・切断プロトコル

- `va:electricWireConnect` — 起点ブロックと対象ブロックの `BlockInstanceId` を受け取りワイヤー追加。判定（距離 > `min(両端maxWireLength)` / どちらかが接続数上限 / 電線不足 / 既に接続済み）は純関数 `EvaluateWireConnection` に切り出し、クライアントのプレビューと共有する（サーバーコードはクライアントから参照可能）
- `va:electricWireDisconnect` — 両端の `BlockInstanceId` を受け取りワイヤー削除、電線をプレイヤーインベントリに返却
- `va:electricWireExtend` — レール式延長。起点ブロック＋設置先座標＋電柱アイテムスロットを受け取り、「電柱設置＋起点とのワイヤー接続」をアトミックに実行。検証（設置先空き / 距離 / 上限 / 電柱アイテム / 電線アイテム）をすべて先に行い、通過後のみ状態変更。成功時は新規電柱の `BlockInstanceId` を返し、クライアント側で次の起点にする

## クライアント

### 電線ツール（電線アイテム所持中）

歯車チェーンポールのレール式延長設計と同じ4状態＋切断：

| 状態 | カーソル位置 | クリック時の動作 |
|---|---|---|
| ① 起点未選択 | 電気系ブロックにヒット | そのブロックを起点として選択 |
| ② 起点未選択 | 既存ワイヤーにヒット | **切断**（電線返却） |
| ③ 起点選択済み | 別の電気系ブロックにヒット | 起点⇔対象をワイヤー接続 |
| ④ 起点選択済み | 空きスペース | 電柱を自動設置しつつ起点と接続。成功後、新規電柱が次の起点（連続延長） |

すべての状態でプレビュー（ゴースト＋接続線）を表示し、可否に応じて青/赤（`MaterialConst.PlaceableColor` / `NotPlaceableColor`）に色分け。消費する電線数も表示する。プレビューの可否判定はサーバーの `EvaluateWireConnection` を直接呼び、食い違いを構造的に防ぐ。起点解除はツール切り替えのみ（明示キャンセルなし）。

通常のブロック設置プレビューにも自動接続の結果（張られるワイヤーと消費電線数、電線不足なら赤）を表示する。

### ワイヤー描画

- カテナリー曲線メッシュを新規実装。両端座標＋垂れ量パラメータから曲線を生成（`BezierRailMesh` のベジェ曲線メッシュ実装を参考にする）
- 切断クリック用のコライダーも曲線に沿って配置する
- 重複描画回避は `myId < targetId` の側だけが描画する方式（`GearChainPoleChainLineView.ShouldDrawLine` と同じ）
- 接続状態の同期は `GearChainPoleStateDetail` と同様、接続先 `BlockInstanceId` 配列を BlockStateDetail（MessagePack）で配信

既存の範囲ボックス表示（`DisplayEnergizedRange`）は「自動接続の探索範囲」の表示として存続する。

## セーブ/ロード

セグメントは現行どおり保存しない。**ワイヤー接続は保存必須**（範囲から再導出できないため）。

- `ElectricWireConnectorComponent.GetSaveState()` で接続先 `BlockInstanceId` ＋消費電線（`ItemGuid`＋個数）をJSON保存（`GearChainPoleComponent` と同方式）。`maxWireLength` 等のマスタ由来値は保存しない
- `IPostBlockLoad.OnPostBlockLoad()` で全ブロックロード後に接続先を解決して復元し、そこからセグメントを構築（ロード順非依存）
- 旧セーブデータとの互換は考慮しない。旧データをロードした場合、ブロックは残るが電力接続はすべて未接続になる

## アイテム経済

- 手動接続（状態③）＝距離分の電線アイテム
- レール式延長（状態④）＝電柱アイテム1個＋距離分の電線アイテム
- 設置時自動接続＝張られる全ワイヤーの距離分の電線アイテム（不足なら設置不可）
- 切断・ブロック撤去＝消費した電線アイテムを返却

## スコープ外

- 電圧・送電ロス・電力階層（すべてのワイヤーは等価）
- 回路信号（Factorioの赤緑ワイヤー的なロジック網）
- 歯車チェーンポール側のコード変更（パターンは共有するが実装は電力側で独立）
- 電柱の種類のUI選択（レール式延長で使う電柱はインベントリ内から自動選択）

## テスト方針

- サーバー単体：`EvaluateWireConnection`（距離超過 / 接続数上限 / 電線不足 / 既接続 の各不可判定）、セグメントのマージ（Union-by-size）と分割（BFS）、単独ブロックのセグメント所属
- サーバー結合：機械設置時の最寄り電柱選択、電柱設置時の未接続機械収集と上限打ち切り、電線不足で設置失敗時に状態が一切変化しないこと、手動接続/切断/延長プロトコルの正常系・異常系、切断・撤去時の電線返却、セーブ→ロードでワイヤーとセグメントが復元されること
- 既存の電力系テスト：範囲ベース自動接続前提のものはワイヤー前提に書き換え
- クライアント：可能であればプレビュー計算（可否・消費数）の単体テスト
