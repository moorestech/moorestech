# 建築UI Ctrl+Z アンドゥ設計

日付: 2026-07-22
ステータス: レビュー待ち
スコープ確定: 設置＋撤去の両方をUndo対象（ユーザー裁定）／Redoはスコープ外（YAGNI）

## 1. 目的

建築UI（設置モード・破壊モード）で Ctrl+Z（macOSは Cmd+Z も可）を押すと、自分が直前に行った建築操作（ブロック設置／ブロック撤去）を1操作単位で取り消せるようにする。

## 2. 前提となる調査事実

- 設置は `PlaceSystemUtil.SendPlaceBlockProtocol(List<PlaceInfo>)`（`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs:149`）に一本化されており、全設置系（Common・BeltConveyor・TrainRail・Blueprint）がここを通る。送信は `va:placeBlock` の fire-and-forget（レスポンス無し）。サーバーは設置不能セルを黙ってスキップする
- 撤去は `VanillaApiWithResponse.BlockRemove(Vector3Int, ct)` → `va:removeBlock`。Response型で `Success / FailureReason` が返る。撤去成功時、建設コスト＋ブロック内部インベントリの中身はプレイヤーインベントリへ全額返却される（`RemoveBlockProtocol.cs:89-125`）
- 設置イベント `va:event:blockPlace` は全プレイヤー分ブロードキャストされ送信者IDを含まないため、「自分の操作」の識別はイベントからは不可能
- クライアントの `BlockGameObjectDataStore` / `BlockGameObject` は座標ごとに `BlockId`・`BlockInstanceId`・`BlockPosInfo.BlockDirection` を保持している
- 既存のアンドゥ・操作履歴機構はゲームランタイムに存在しない（新規実装）
- ドラッグ直線設置・ブループリント貼付により、1操作＝複数ブロックが標準

## 3. アーキテクチャ（クライアント完結・サーバー変更なし）

アンドゥの実体は既存プロトコルの逆操作呼び出しで表現する。

- 設置のUndo ＝ 記録した各座標へ `va:removeBlock` を発行（Response型なので成否が取れる）
- 撤去のUndo ＝ 記録した `BlockId / Direction / 座標` で `va:placeBlock` を再発行（建設コストは撤去時に全額返却済みのため、再設置での再消費と収支が合う）

サーバー側に新プロトコル・操作ログ・アンドゥ状態は一切持たない（導出可能テスト: 逆操作は既存プロトコルで完全に表現できる）。

### 新規コンポーネント（`Client.Game/InGame/BlockSystem/PlaceSystem/Undo/`）

| クラス | 責務 |
|---|---|
| `BuildOperationHistory` | 履歴スタック本体。上限32エントリのLIFO。`Push(IBuildOperationRecord)` / `TryPop(out record)` のみ |
| `PlaceOperationRecord` | 設置1バッチの記録。送信した `List<PlaceInfo>` のスナップショット（Position / BlockId / Direction / VerticalDirection） |
| `RemoveOperationRecord` | 撤去1バッチの記録。コミット時点で選択されていた各ブロックの `Position / BlockId / BlockDirection` のリスト（**楽観的記録**。撤去の成否は追わず、失敗セルはUndo時の照合ガード＝「座標が空でなければ再設置しない」で自然に無効化される） |
| `BuildUndoService` | Ctrl+Z受付とUndo実行。照合ガード→逆操作プロトコル発行。実行中の再入は無視（bool ガード） |

インスタンスはシングルトンにせず、既存の建築UI系の生成経路（`ClientDIContext` / UIState構築）でDIする。イベントにはUniRxを使う（規約）。

### 記録フック（2箇所）

1. **設置**: `PlaceSystemUtil.SendPlaceBlockProtocol` 内で、送信した `PlaceInfo` リストのディープコピーを `PlaceOperationRecord` として `Push`。`Placeable == false` のセルは除外して記録する
2. **撤去**: `DragDeleteSelection.CommitDelete()` がコミットした対象リストを返すよう変更し、呼び出し側の `DeleteObjectService` が `BlockGameObjectChild` 対象から `Position / BlockId / BlockDirection` を控えて `RemoveOperationRecord` として `Push` する（コミット時の楽観的記録。`DeleteAsync` の成否には触れない）。`IDeleteTarget` のうち `BlockGameObjectChild` 以外（レールノード・列車車両等）は記録対象外

### Undo実行フロー（Ctrl+Z 1回）

```
Ctrl+Z → BuildUndoService.TryUndo()
  ├─ 実行中なら無視（再入防止）
  ├─ TryPop → 無ければ何もしない
  ├─ PlaceOperationRecord の場合:
  │    各セル: BlockGameObjectDataStore で「同座標に同BlockIdのブロックが現存」を照合
  │      → 一致セルのみ BlockRemove を await 発行（失敗セルは黙ってスキップ）
  └─ RemoveOperationRecord の場合:
       各セル: 同座標にブロックが存在しないことを照合
         → 通過セルを PlaceInfo に再構成し、1回の SendOnly.PlaceBlock でバッチ再設置
```

照合ガードの根拠: 設置失敗セル（サーバーが黙ってスキップした分）や、Undoまでの間に他要因で変化した座標に対して逆操作を発行すると、無関係のブロックを破壊し得るため。BlockIdの一致まで確認する。

### PlaceInfo再構成の詳細（撤去Undo）

- `Direction`: 記録した `BlockPosInfo.BlockDirection`（12方位。Up/Down成分を含む）
- `VerticalDirection`: `BlockDirection` の Up*/Down*/水平 から `Up/Down/Horizontal` を導出
- `BlockCreateParams`: **空配列で再設置する**。既知の制限として、設置時パラメータに依存するブロック（列車レールの接続情報等）は撤去Undoで元の接続状態まで復元されない（レールは撤去時点で接続が壊れるため、Undoでの完全復元はそもそも成立しない）

## 4. 入力

- 検知: `HybridInput.GetKey(KeyCode.LeftControl/RightControl/LeftCommand/RightCommand)` ＋ `GetKeyDown(KeyCode.Z)`。既存の建築キー（Q/E/X等）と同じ直接キー参照の流儀に従い、InputActionアセットは変更しない
- 有効な状態: `PlaceBlockState` と `DeleteObjectState` の `GetNextUpdate()` から `BuildUndoService.TryUndo()` を呼ぶ（建築UI中のみ有効）。Web UIのkeydownは使わない（建築操作は全てUnity側で処理されている前例に従う）
- キー説明UI: 両Stateの `KeyControlDescription` テキストに「Ctrl+Z: 元に戻す」を追記する

## 5. エラーハンドリング

- Undo中の撤去リクエスト失敗（インベントリ満杯・列車使用中レール等）: そのセルは残り、エントリは消費済みとする。既存の削除拒否ツールチップは経由せず、静かにスキップ（設置プロトコル自体が失敗セルを黙殺する既存挙動と整合）
- 再設置の成否はレスポンスが無いため確認しない（通常の設置と同じ扱い。建設コスト不足セルはサーバーが黙ってスキップ）
- 履歴が上限32を超えたら最古を破棄

## 6. 既知の制限（仕様として許容）

1. 撤去Undoでブロック内部インベントリは復元されない（中身は撤去時にプレイヤーへ返却済みのまま）
2. 撤去Undoで `BlockCreateParams` は復元されない（レール接続等）
3. マルチプレイで他プレイヤーが同座標を変更した場合、照合ガードによりそのセルのUndoは黙って無効になる
4. Redo（Ctrl+Y）は無い。履歴はセッション内のみでセーブされない
5. Undo自体は履歴に積まない（Undoの取り消しは不可。積むとRedo相当が必要になるため）

## 7. テスト

- `BuildOperationHistory` / レコード構築・照合ガードのロジックは Unity非依存の純C#として切り、EditModeユニットテストを書く
- E2E: プレイテストDSL（unity-playmode-recorded-playtest）で「設置 → Ctrl+Z → ブロック消滅」「撤去 → Ctrl+Z → 再出現」のシナリオを追加する（Ctrl+ZはInputSystemの `QueueStateEvent` で注入）

## 8. 自己反証（設計が拒否すべき入力の確認）

- **「設置が全セル失敗した直後のCtrl+Z」**: 履歴には積まれるが、照合ガード（同座標同BlockId現存チェック）が全セル不一致となり、撤去リクエストは1件も発行されない。他人のブロックや地形上の別ブロックを誤爆しない ✓
- **「設置→他プレイヤーが同座標を撤去→別ブロックを設置→Ctrl+Z」**: BlockId不一致でガードが弾く。ただしBlockIdまで同一の場合は他人のブロックを撤去してしまう。BlockInstanceIdでの照合強化は可能（設置イベントに含まれる）が、fire-and-forget設置では自分の設置とInstanceIdの対応付け自体が座標マッチング頼みになるため、初期実装ではBlockId照合までとする（シングルプレイ主体の現状で許容）
- **「ドラッグ削除の応答が返り切る前のCtrl+Z」**: 撤去イベントが未反映のセルはクライアント上ではまだブロックが存在するため、「座標が空」ガードで再設置がスキップされ、そのセルのUndoは失われる（許容。撤去応答は通常即時であり、誤って二重ブロックが生まれる方向には倒れない）
- **「撤去に失敗したセル（列車使用中レール・インベントリ満杯）を含むバッチのCtrl+Z」**: 楽観的記録により失敗セルもレコードに入るが、ブロックが現存するため「座標が空」ガードで再設置は発行されない ✓

## 9. 配置と前例（spec-architecture-review済み）

| 配置決定 | 前例（引用） |
|---|---|
| 新規5ファイルを `Client.Game/InGame/BlockSystem/PlaceSystem/Undo/` に新設 | PlaceSystem配下のサブディレクトリ構成（`Common/` `Blueprint/` 等） |
| `BuildUndoService` はUIステートの `GetNextUpdate()` から毎フレーム `ManualUpdate()` で明示駆動し、Ctrl+Z判定はサービス内部に閉じる（ステート側にキー判定を書かない） | `PlaceBlockState`→`PlaceSystemStateController.ManualUpdate()`（制御参加者はステート駆動・入力判定はサービス内部の規約） |
| `BuildOperationHistory` / `BuildUndoService` はVContainerに `Lifetime.Singleton` 登録し、ステートへコンストラクタ注入 | `MainGameStarter.cs:216-227` の各State登録 |
| 静的ユーティリティ `PlaceSystemUtil` からの履歴アクセスは `ClientDIContext` の static プロパティ経由 | `ClientDIContext.BlockGameObjectDataStore`（DI解決物のstatic公開の既存前例） |
| Ctrl+Z検知は `HybridInput.GetKey(修飾キー)+GetKeyDown(KeyCode.Z)` の直接キー参照 | `WebUiCefToggle.cs:34`（Ctrl+I）・`UIRoot.cs:27`（Ctrl+U） |
| `HybridInput.ToInputSystemKey` に `Z`/`LeftCommand` のマッピング追加（プレイテストDSLのキー注入対応に必須） | 同メソッド内の既存マッピング群 |
| 新プロトコル・新イベント・サーバー変更なし。Undoは既存 `va:removeBlock` / `va:placeBlock` の逆操作発行のみ | 同期3点セットの適用外（新規サーバー可変状態を作らないため。履歴はクライアントローカルの入力状態） |
| 通知系の新設なし（UniRx Subject追加不要。ブロックの出現・消滅は既存の `BlockGameObjectDataStore.OnBlockPlaced/OnBlockRemoved` が既に流している） | — |

機能パリティ（死活表）: 本機能は純追加であり、既存の入力キー・UI・遷移はすべて無変更で生存する。Zキー・Ctrl+Z/Cmd+Zの既存割当は無し（grep確認済み。既存のCtrl系はCtrl+I/Ctrl+Uのみ）。`DragDeleteSelection.CommitDelete()` の戻り値変更（void→コミット済みリスト）は既存呼び出し側1箇所・既存テストとも互換。
