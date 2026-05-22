# 乗車システム Phase 1 → Phase 2 申し送り

Phase 2（`docs/superpowers/plans/2026-05-21-riding-phase2-server-core.md`）に着手する前に必読。
Phase 1 実装中に確定した事実・計画からの逸脱・ユーザー判断による設計変更をまとめる。
**Phase 2 計画書は Phase 1 着手前に書かれており、以下の項目で実態とズレている。**

---

## 1. アセンブリ構造（最重要・計画と相違）

外部監査（Codex）で **`Server.Util` が `Game.Train` を参照している**ことが判明。
当初計画の「`Game.PlayerRiding.Interface` が `Server.Util` を参照」だと、Phase 2 で
`Game.Train → Game.PlayerRiding.Interface`（`TrainCar : IRidable`）を足した瞬間に
`Game.Train → Game.PlayerRiding.Interface → Server.Util → Game.Train` の循環になる。

**修正済みの実態:**
- `Game.PlayerRiding.Interface.asmdef` の参照は **`MessagePack.Annotations` のみ**。
- `RidableType` / `RidableIdentifierMessagePack` / `IRidableIdentifier` /
  `TrainCarRidableIdentifier` / `RidableIdentifierConverter` は全て
  `Game.PlayerRiding.Interface` アセンブリ・同名 namespace に集約済み。
- Phase 2 計画書中の `using Server.Util.MessagePack;` は全て
  `using Game.PlayerRiding.Interface;` に読み替えること。

**Phase 2 で `Game.PlayerRiding.Interface` に型を追加するときの制約:**
- `IRidable` 等は `Game.Train` から参照される。よって `Game.PlayerRiding.Interface` は
  **`Server.Util` も `Game.Train` も参照してはいけない**（循環する）。
- `IRidable` のメソッドシグネチャに使う型（プレイヤーID・座席インデックス・座標等）は、
  `Game.PlayerRiding.Interface` が参照可能な低レベルアセンブリの型に限定する。
  `int` / `long` / プリミティブ、または `MessagePack.Annotations` のみで完結させる。
  プレイヤーID型が `Game.Train` 等を引き込む場合は `long` 等のプリミティブで持つ
  （`TrainCarRidableIdentifier` が `long` を持つのと同じ理由）。
- 実装アセンブリ `Game.PlayerRiding`（新規）は `Game.Train` / `Game.Context` /
  `Server.Util` 等を自由に参照してよい（こちらは循環しない）。

## 2. テスト配置（計画と相違）

- 計画書の `moorestech_server/Assets/Scripts/Tests.UnitTest/` は**存在しない誤り**。
- 正: `moorestech_server/Assets/Scripts/Tests/UnitTest/{Layer}/`、
  asmdef は `Server.Tests`、namespace は `Tests.UnitTest.*`。
- `Server.Tests.asmdef` には Phase 1 で `Game.PlayerRiding.Interface` 参照を追加済み。
  Phase 2 で実装アセンブリ `Game.PlayerRiding` を新設したら、
  `Server.Tests.asmdef` にもその参照を追加すること。
- 新規 `.asmdef` 追加後は `uloop compile` を一度通して Unity に `.meta` を生成させる。

## 3. 座席マスタの生成型（確定）

- `train.yml` の `trainCars` items に `ridableSeats`（`items: {ref: ridableSeat}`）を追加済み。
- 生成型: 要素型 `RidableSeat`（`Mooresmaster.Model.TrainModule` namespace）、
  プロパティ `TrainCarMasterElement.RidableSeats`（`RidableSeat[]`）。
- `RidableSeat` のフィールド: `OffsetX` / `OffsetY` / `OffsetZ`（いずれも number 由来）。
- **`optional` は付けていない**（コードレビュー指摘で削除）。`RidableSeats` は常に非 null 配列。
  座席なし車両は `ridableSeats: []`。ロードされる全 train.json
  （`ForUnitTest` / `server_v8`）に `ridableSeats: []` を追加済み。
- SourceGenerator はコードをディスクに出力しない（メモリ生成）。`.cs` ファイルは探しても無い。

## 4. TrainCarInstanceId の永続化（確定）／一意性検出は削除された

- `TrainCarSaveData.TrainCarInstanceId`（`long`）追加済み。
- `TrainCar` に ID 指定コンストラクタ `TrainCar(TrainCarMasterElement, bool, TrainCarInstanceId)` あり。
  引数なし版は `TrainCarInstanceId.Create()` を渡して委譲。
- `CreateTrainCarSaveData()` が ID を保存、`RestoreTrainCar()` が ID を復元（往復する）。

**重要な逸脱 — 計画 Task 8 の一意性担保は撤回された:**
- 計画では `TrainUnitDatastore.EnsureTrainCarInstanceIdsUnique` ＋
  `RebuildCarToUnitIndex` での重複 ID 検出（重複時 throw）を入れた。
- **ユーザー判断でメソッドごと削除された**（コミット `a49532244`、「不要な static」）。
- 現状、重複 `TrainCarInstanceId` を検出するガードは**一切無い**。
  Phase 2 以降で乗車状態が `TrainCarInstanceId` を参照先に使うため、
  重複が問題になるなら**別途設計し直す**こと（前回の `public static` 直テスト方式は不可）。
- 旧セーブ（`TrainCarInstanceId` フィールド無し）対応もしない（ユーザー決定・
  AGENTS.md「後方互換性は考慮不要」）。旧セーブをロードすると全車両 ID=0 になる前提。

## 5. 識別子の値型・変換（確定）

- `TrainCarRidableIdentifier` は `long TrainCarInstanceId` を保持（`TrainCarInstanceId`
  struct ではなく `long`。アセンブリ循環回避）。
- `RidableIdentifierMessagePack.TrainCarInstanceId` は `string`（`long` を `ToString()`）。
- 変換は `RidableIdentifierConverter`：拡張メソッド `IRidableIdentifier.ToMessagePack()` と
  static `FromMessagePack(RidableIdentifierMessagePack)`。
- `Game.Train` 側の `TrainCarInstanceId` struct ⇔ `long` 変換は
  `.AsPrimitive()` / `new TrainCarInstanceId(long)`。

## 6. Phase 2/3 に申し送る既知の検討事項

- **`long.Parse` の不正データ耐性**: `RidableIdentifierConverter.FromMessagePack` の
  `long.Parse(messagePack.TrainCarInstanceId)` は不正文字列／null で例外。
  Phase 3 でクライアント受信経路ができたら `long.TryParse`／null ガードを検討。
- **discriminator の二重化**: `IRidableIdentifier.Type`（`RidableType`）と
  `RidableIdentifierConverter` の C# 型 switch が二重の判別子。Phase 1 では
  `IRidableIdentifier.Type` は未使用。Phase 2-4 で `Type` を実際に使うか、
  型 switch に一本化するか決める（既存 `ISubInventoryIdentifier` 踏襲で `Type` は残してある）。
- Codex 観点2（`AttachTrainCarToUnitProtocol` の部分状態）は、重複検出が削除されたため
  現状は無効化。Phase 2/3 で新たな登録時バリデーションを入れる場合は再考。

### Phase 2 デュアルレビューで判明した Phase 3 申し送り（2026-05-22）

Phase 2 完了後の外部監査（Codex）で、Phase 3 で対応すべき項目が3点判明した:
- **`OnTrainCarRemoved` の発火順**: `TrainCar.Destroy()` 由来の車両削除イベントは
  `TrainUnitDatastore` の `RegisterTrain/UnregisterTrain` 更新より**前**に発火する。
  仕様§4.4 は「`RidableResolver` で当該車両が解決できなくなった後にハンドラが走る」前提。
  Phase 3 で `OnTrainCarRemoved` を購読して `PlayerRidingDatastore.OnRidableRemoved` を
  呼ぶ際、発火点を datastore 更新後へ移すか、削除プロトコル側で更新後に呼ぶこと。
- **`EvaluateOnLogin` の戻り値**: 現状 `bool` のみ。仕様§8 は復帰した `RidingState` を
  `InitialHandshakeProtocol` のレスポンスに含める想定。Phase 3 で `EvaluateOnLogin` 後に
  `TryGetRidingState` を呼ぶ二段構えにするか、`EvaluateOnLogin` が結果 DTO を返す API に
  寄せるか決める。
- **不正な `IRidableIdentifier` 入力**: `RidableResolver.Resolve` / `PlayerRidingDatastore.TryRide`
  は `null` や未知の `IRidableIdentifier` 実装で例外になる（`identifier.Type` / キャスト）。
  Phase 3 のプロトコル入力は外部データなので、protocol 層で検証するか `Resolve` 入口で
  `null`／型不一致を `RidableNotFound` 相当（`null` 解決）に倒すこと。`long.Parse` の
  不正データ耐性（上記）と同じ経路の話。

## 7. 計画書の更新が必要な箇所

Phase 2/3/4 計画書（`docs/superpowers/plans/2026-05-21-riding-phase{2,3,4}-*.md`）は
Phase 1 着手前に書かれており、以下を着手時に実コードと突き合わせて修正する:
- `using Server.Util.MessagePack;` → `using Game.PlayerRiding.Interface;`
- `RidableType` / `RidableIdentifierMessagePack` の所在を `Game.PlayerRiding.Interface` に
- テストパス `Tests.UnitTest/` → `Tests/UnitTest/`
- Phase 2 計画の `EnsureTrainCarInstanceIdsUnique` / 重複検出への言及は撤回済みとして扱う

## 8. コードレビューで追加された規約（Phase 2 実装中も遵守）

- **`optional` / nullable は明確な理由がある場合のみ**。空配列・既定値で表現できるなら付けない。
  後方互換は（プロジェクト方針で不要なので）正当な理由にならない。
- **テスト容易性のためだけに `public static` を作らない**。テストは実際の API 経路で検証する。
- 既存 `ISubInventoryIdentifier` / `InventoryIdentifierMessagePack` パターンの踏襲は維持。

## 9. 環境メモ

- Unity Editor は起動済み前提。停止していたら `uloop launch ./moorestech_client`、
  約50秒待って `uloop list` で疎通確認。
- `uloop compile` は「Unity is compiling」が返る間ポーリングし、`"Success"` を待つ。
- `uloop` の version mismatch 警告（cli 1.7.3 / server 1.6.3）は動作に支障なし。
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<regex>"`。

---

## Phase 1 成果（master からの差分・コミット）

```
994a96dfa 乗車Phase1: ridableSeats を必須化しJSON・テストを追随
a49532244 コード修正（ユーザー: optional 削除・EnsureTrainCarInstanceIdsUnique 削除）
1290c179b 乗車Phase1: 永続化テストでセーブデータのID値を直接検証
c46863e2d 乗車Phase1: アセンブリ循環を回避するため識別子型をInterfaceへ集約
c5e5a3a79 乗車Phase1: Unity生成の.metaファイルを追加
04ac1bb29 乗車Phase1: TrainCarInstanceId の一意性を担保（後に a49532244 で撤回）
9adb44cf9 乗車Phase1: TrainCarのセーブ/復元でインスタンスIDを永続化
cb593e52a 乗車Phase1: TrainCar にインスタンスID指定コンストラクタを追加
a96f42d0b 乗車Phase1: TrainCarSaveData に TrainCarInstanceId を追加
67f41b806 乗車Phase1: 座席マスタスキーマ ridableSeat を追加
2bc201c31 乗車Phase1: 識別子とMessagePackの相互変換を追加
4f8061a3c 乗車Phase1: IRidableIdentifier と TrainCarRidableIdentifier を追加
d2517194f 乗車Phase1: RidableType と RidableIdentifierMessagePack を追加
```

別リポジトリ `../moorestech_master` にも `server_v8` の train.json へ `ridableSeats: []` をコミット済み。

検証状態: コンパイル エラー0、列車関連テスト 150/150 PASS、乗車識別子テスト全 PASS。
外部監査（Codex）＋多観点コードレビュー（code-reviewer）の両方を通過済み。
