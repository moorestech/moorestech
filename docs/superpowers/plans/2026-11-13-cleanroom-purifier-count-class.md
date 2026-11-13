# クリーンルーム単純化（濃度廃止・フィルター定率消費） Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** クリーンルームのクラスを「空きセル数に対する清浄機の除去能力（換気回数）」だけで決め、フィルターを清浄機1台につき毎分5個の定率で消費させる。

**Architecture:** 不純物量（濃度）と汚染流入のシミュレーションを丸ごと削除する。`CleanRoomDatastore` は毎tick「清浄機の除去能力合計 ÷ 空きセル数」から閾値行を決めるだけになる。フィルター摩耗は部屋から押し込む方式をやめ、清浄機コンポーネント自身が毎tick経過tickを数えて消費する。

**Tech Stack:** Unity / C#（サーバー `moorestech_server`、クライアント `moorestech_client`）、マスタ YAML スキーマ + SourceGenerator、MessagePack、webui（TypeScript / zod / vitest）、セーブマイグレーション連鎖（`ISaveMigrationStep`）。

## Requirements

- R1 フィルター定率消費: 通電していて（電力割合 > 0）フィルターが入っている清浄機は、1台につき `filterLifeSeconds` 秒ごとにフィルターを1個消費する。部屋の中か外か・部屋のクラス・他の清浄機の台数に依存しない。受け入れ基準: `filterLifeSeconds=12` の清浄機を満電力で60秒ぶんtickさせるとフィルターがちょうど5個減る。清浄機2台を同じ部屋で60秒動かすと各5個（合計10個）減る。
- R2 止まっている清浄機は消費しない: 電力割合が0、またはフィルターが空の清浄機は経過tickを進めない。受け入れ基準: 無通電で60秒tickしてもフィルター数・残り寿命が変わらない。
- R3 フィルター生産レシピ: `moorestech_master` の電気精密組立機のクリーンルームフィルターのレシピを `time: 12`（毎分5個）にする。受け入れ基準: `machineRecipes.json` の当該レシピ（machineRecipeGuid が `01a17349-9327-763a-9219` で始まりクリーンルームフィルターを出力する1件）の `time` が 12。
- R4 クラスは換気回数だけで決まる: クラス判定は「換気回数 = 清浄機の実効除去能力合計 ÷ 空きセル数」と各クラスの `requiredAirChangeRate` の比較だけで行う。昇格時だけ `requiredAirChangeRate × 1.25` を要求する既存のヒステリシスは残す。`requiredAirChangeRate` の値は変えない。受け入れ基準: 濃度・不純物量を参照するコードがサーバーに残っていない。換気回数が閾値以上になったtickでクラスが上がり、下回ったtickで下がる。
- R5 汚染流入の削除: 部屋の体積・壁面積・ハッチ/パイプコネクタ数・稼働機械・ハッチ搬送量から汚染を計算する仕組みを削除する。受け入れ基準: `CleanRoomPollution`・`ICleanRoomMachine.IsPolluting`・`ICleanRoomItemHatch.RecentThroughputPerSecond`・`CleanRoomMaster.Pollution`・`IsPollutionConnectorBlock` が存在しない。
- R6 マスタスキーマ: `cleanRoom.yml` から `pollution`・`thresholds[].maxConcentration`・`pollutionConnectorBlockGuids` を削除する。`blocks.yml` の清浄機パラメータ `filterCapacity` を `filterLifeSeconds`（number）へ置き換える。受け入れ基準: テスト用mod と `moorestech_master` の両方の JSON が新スキーマでロードでき、サーバーテストが通る。
- R7 セーブ互換: `WorldSaveAllInfo.CurrentVersion` を 12 にし、V11→V12 のマイグレーションステップを足す。受け入れ基準: V11 のセーブ（`cleanRoomRooms[].impurityCount` と清浄機の `wearAccumulation` を持つ）が V12 へ変換され、`impurityCount` が消え、清浄機の状態が `filterWearSeconds: 0` を持つ。
- R8 画面: 部屋の情報表示から濃度を消す。換気回数・体積・清浄機一覧（電力割合・フィルター数・残り寿命）は残す。受け入れ基準: MessagePack・クライアント DTO・webui の zod スキーマと表示文言に concentration が残っていない。webui のテストが通る。

やらないこと:
- 必要換気回数（`requiredAirChangeRate`）・層失敗率・ダイ損失・ダイ表・研究コストの数値変更。
- 清浄機の `requiredPower`・`removalVolumePerSecond` の変更。
- `CleanRoom.SurfaceArea` の削除（`CleanRoomDetectionService` が部屋の同一判定に使っているので残す）。
- `ICleanRoomBoundaryComponent`・密閉検出・ハッチの `maxItemsPerSecond` 上限の変更。

## Global Constraints

- AGENTS.md（リポジトリ直下）の規約を全タスクで守る。特に: try-catch 禁止、partial 禁止、`Func<>` 禁止、デフォルト引数禁止、1ファイル200行以下、日本語→英語の2行セットコメント、スキーマの `optional: true` や `?? Default` フォールバックで欠損を吸収しない（全JSONを一括更新する）。
- サーバーの経過時間は `Core.Update.GameUpdater` のtickだけで測る。秒との換算は `GameUpdater.SecondsToTicks(double)`（戻り値 `uint`）。
- `.meta` を手で作らない。Unity が生成したものはコミットしてよい。
- YAML スキーマを触るときは `.agents/skills/edit-schema/SKILL.md` を読んでから行う。SourceGenerator の再生成の起こし方もそこにある。
- セーブ形式の変更は `.agents/skills/moorestech-save-migration/SKILL.md` に従う。
- サーバーテストは `.agents/skills/creating-server-tests/SKILL.md` の流儀に従う。
- コンパイル: `uloop compile --project-path ./moorestech_client`。テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。「Unity is reloading」が出たら45秒待って再実行する。
- 作業場所はメインcheckout `/Users/katsumi/moorestech` のブランチ `feature/cleanroom-purifier-count-class`。マスタデータは `/Users/katsumi/moorestech_master` を直接編集する。
- **触ってはいけない未コミット変更**: メインcheckoutの `.moorestech-external-revisions.json` と `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` には作業前からユーザーの未コミット変更がある。`_CompileRequester.cs` は再生成のために書き換えてよいが、この2ファイルはコミットに含めない。`moorestech_master` の `blocks.json`・`machineRecipes.json`・`research.json`・`nodeGraph.v1.json` にも作業前からユーザーの未コミット変更がある。**`moorestech_master` 側では今回コミットしない**（編集だけ行い、作業ツリーに残す。コミット・push・PR・ピン更新は後段の閉じタスクで行う）。
- `Saves/`（`~/Library/Application Support/moorestech/Saves`）のワールドを書き換えない。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反1件・修正1件 / 清浄機の摩耗をtickで保存していたのを、前例（燃料発電機の remainingFuelSeconds）に合わせて秒で保存する形へ直した。死活表の漏れは無し
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強1・弱3・第3バケツ0 / 強=過去裁定2本（濃度表示・汚染で大部屋有利）との矛盾で、D2・D3 のユーザー裁定が上書き済み（D15）。弱=閾値行の順序検証を換気回数へ付け替え、マイグレーションの明示nullを許容、の2件を反映。換気回数の導出が判定と表示の2か所にある点は既存どおり残す（D16）

## 配置と前例

| # | 項目 | 配置先 | 機構・前例 |
|---|---|---|---|
| 1 | フィルター経過tickの加算 | `Game.Block/Blocks/CleanRoom/CleanRoomAirFilterComponent.cs` に `IUpdatableBlockComponent.Update()` を足す | 同ディレクトリの `CleanRoomItemHatchComponent`・`Machine/CleanRoomMachineProcessorComponent` が `IUpdatableBlockComponent` で毎tick進む前例。清浄機自身の持ち物（フィルタースロット・経過tick）を自分で進める |
| 2 | クラス判定 | `Game.CleanRoom/Util/CleanRoomPurityLogic.cs` の `DecideThresholdIndex` を換気回数だけの形に変える | 既存の純関数をそのまま縮める |
| 3 | 毎tickの部屋更新 | `Game.CleanRoom/CleanRoomDatastore.cs` の `UpdatePurity` | 既存。積分と摩耗配分を消し、閾値行更新だけ残す |
| 4 | マイグレーション | `Game.SaveLoad/Migration/Steps/SaveMigrationStepV11ToV12.cs` + `Steps/V11ToV12/` の部品 | `SaveMigrationStepV10ToV11.cs` + `Steps/V10ToV11/` の前例（`ISaveMigrationPart` を並べて `MigrationSaveJson.RunParts`） |
| 5 | 清浄機パラメータ | `VanillaSchema/blocks.yml` の `filterCapacity` → `filterLifeSeconds` | レシピの `time`（秒）と同じ単位。tickへの換算はコンポーネントのコンストラクタで1回 |

### データフロー

```
（電力網tick）→ 清浄機: 電力割合を確定 ─┐
（ブロックUpdate）→ 清浄機: 経過tick加算・フィルター消費（自己完結）
                                          ├→［清浄機の RemovalVolumePerSecond］→ CleanRoomDatastore.UpdatePurity: 換気回数 → 閾値行 → CleanRoomMachineEffectService → 機械の歩留まり
```

清浄機は「書き手」（自分の除去能力を公開するだけ）、Datastore は「読み手」。部屋から清浄機へ摩耗を押し込む逆向きの矢印（`ApplyRemovedImpurity`）は消える。

### 死活表（現在ユーザーが使える操作）

| 操作・表示 | 計画後 | 根拠 |
|---|---|---|
| 清浄機にフィルターを入れる・ベルトで補充する | 生きる | `FilterSlot`・`inventoryConnectors` は不変 |
| 清浄機UIの電力割合・フィルター数・残り寿命 | 生きる | `CleanRoomAirFilterStatus` は不変。残り寿命の算出元が経過tickに変わるだけ |
| 部屋情報のクラス・換気回数・体積 | 生きる | `CleanRoomRoomSummary` に残す |
| 部屋情報の濃度 | **消える** | ユーザー裁定で廃止（判断記録 D2） |
| ハッチ・パイプ・稼働機械・搬送量による汚染 | **消える** | ユーザー裁定で廃止（判断記録 D3） |
| 機械の歩留まりが部屋クラスで変わる | 生きる | `CleanRoomMachineEffectService` と閾値行は不変 |
| ハッチの毎秒搬送上限 | 生きる | `maxItemsPerSecond` の上限は汚染と別機構。触らない |

## 反証検査で確定した追加・訂正（各タスクの記述と食い違うときはこの節が優先）

リポジトリを Grep して確かめた事実に基づく。各タスクを始める前にこの節を読むこと。

**Task 1 への追加**
- `filterLifeSeconds` が有限で 0 より大きいことを、清浄機ブロックのパラメータ検証（`BlockParamValidator.cs` の132〜140行目あたりにある清浄機ブロックの検証。書式の前例は `CleanRoomItemHatchThroughputValidator.cs:22` の "that is not positive"）へ足す。**Task 2 のコンストラクタの `Math.Max(1u, ...)` による丸めは書かない**（マスタの誤りを黙って補正せず起動時に止める）。コンストラクタは `_filterLifeTicks = GameUpdater.SecondsToTicks(param.FilterLifeSeconds);` とする。
- `Core.Master/Validator/CleanRoom/CleanRoomBoundaryBlockSetValidator.cs` は `CleanRoomMasterUtil.cs:23` からしか呼ばれていない。汚染接続点集合の検証だけをしているなら、呼び出しごと `git rm`（`.meta` も）する。境界ブロックの他の役割も検証しているなら、汚染接続点の部分だけ削る。
- `moorestech_master/.../machineRecipes.json` の対象は1609〜1611行目あたりの `01a17349-9327-763a-9219-5a23c8d8956b`（time 4・出力1個）の1件だけ。

**Task 3 への補足（確認済みの事実）**
- 清浄機は `VanillaCleanRoomAirFilterTemplate.cs:45-51` で部品一覧に入っており、`BlockSystem.cs:48` が `IUpdatableBlockComponent` を集めて回す。テンプレートの修正は要らない。
- 1tick の順序は 電力tick（`MasterTickUpdater.cs:91`）→ ブロック更新（:114）→ 部屋判定（:127）。初回tickから電力割合は確定している。
- ハッチの窓変数（`_pushedPerTick`・`_windowSum`・`_windowIndex`、94〜99行目あたり）は汚染用レートだけのもので、毎秒搬送上限は `_pushAllowance`（69〜90行目あたり）だけで決まる。窓変数は全部消してよい。`Update()` は `_pushAllowance` の補充に使われているなら残す。

**Task 4 への追加（Files に足す）**
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/CleanRoom/GetCleanRoomInfoProtocolTest.cs`（97行目 `CreateOut(0, 0, 1, …)` → 濃度の引数を外して `CreateOut(0, 1, …)`。ほかに `Concentration` を見ている箇所があれば削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BlockDetail/WireContract/WireContractCleanRoomTest.cs`（42行目 `CreateOut(1.5d, 0.25d, 74, …)`・85行目 `CreateClassified("A", 0, 0.1d, 2d, 26, …)` から濃度の double を外す。期待 JSON に concentration があればそれも外す）。Step 6 でクライアント側に出るコンパイルエラーは `CleanRoomDetailDtoBuilder.cs` とこのテストの2か所。
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/Usability/CleanRoomPanelOrientationTest.cs`（38〜48行目。フィルター5個・満電で2000tick回してからクラスAを期待している。新しい消費では 5個×240tick=1200tick で尽きて Out になる。入れるフィルターを20個にする）
- 同じ理由で、清浄機にフィルターを入れて長く tick を回してからクラスを期待しているテストが他に無いか、`CleanRoom` テスト全体を実行して落ちたものを見る。落ちた原因が「フィルターが尽きた」ならフィルター数を増やす（期待するクラスは変えない）。
- `CleanRoomHatchTest.cs` の `ThroughputWindowDecaysToZeroTest`（102〜121行目）はメソッドごと削除する。`CleanRoomItemHatchThroughputCapTest.cs:46` は `RecentThroughputPerSecond` を見る補助の assert 1行だけ削除し、通過数の検証（44〜45行目）は残す。
- テスト用mod の値: 閾値は A 0.017・B 0.0083・C 0.0042・D 0.0014、清浄機は requiredPower 100・removalVolumePerSecond 5、tick は毎秒20（寿命12秒=240tick）。Step 3 のテストの配列 `RequiredAirChangeRates` はこのままでよい（純関数のテストでマスタを読まない）。

**Task 5 への訂正**
- `Game.SaveLoad/Migration/Steps/` には `.cs` がちょうど10個あり、1ディレクトリ10ファイルの上限（`checks_static.py` の dir-file-limit）に達している。**`SaveMigrationStepV11ToV12.cs` は `Steps/V11ToV12/` の中に置く**（名前空間は他のステップと同じ `Game.SaveLoad.Migration.Steps` のまま。部品2つは `Game.SaveLoad.Migration.Steps.V11ToV12`）。
- マイグレーションのテストで組む V11 セーブには、前例 `SaveMigrationStepV10ToV11Test.cs:46` と同じく必ず `["world"] = new JArray()`（清浄機を入れるテストではその中にブロック）を入れる。`TryCollectBlockStates` は world が配列でないと失敗を返すため。テスト3（`cleanRoomRooms` が文字列 → Failed）では、失敗理由の文字列に `cleanRoomRooms` が含まれることも確かめる（world が無いせいで別の理由で Failed になるのを防ぐ）。

**Task 6 への訂正と追加**
- 表示文言の定義元は `/Users/katsumi/moorestech/Localization/localization.csv` の454行目あたり（キー `ui.blockInventory.cleanRoomAir`）。列は Source・english・japanese・german・korean の5つで、全部に `{concentration}` が入っている。**5列すべて**から濃度の句を消し、換気回数だけの文にする。
- この CSV は `moorestech_client/Assets/Scripts/Client.Localization/csc.rsp` 経由で SourceGenerator に読まれる。再生成のために `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` の印を更新し、**CSV と一緒にコミットする**（Core.Master 側の `_CompileRequester.cs` とは別ファイル。こちらはコミットしてよい）。webui の `generated/localizationKeys.ts` はキー名だけなので再生成は要らない。
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures/blocks/cleanRoomFixtures.ts`（23行目 `concentration: 1.2` を削除。`.strict()` のスキーマで型検査されるので残すと型エラー）
- Step 3 の検証コマンドは CI と同じ形にする: `cd moorestech_web/webui && pnpm install --frozen-lockfile && pnpm exec tsc -b --force && pnpm exec tsc -p e2e/tsconfig.json --noEmit && pnpm exec vitest run`
- Step 3 の Unity テストの正規表現は `"CleanRoom|WebUi|LocalizationPlaceholderParity"`（全言語のプレースホルダ一致を見る `Client.Tests/Localization/Resolution/LocalizationPlaceholderParityTest.cs` を必ず走らせる）。
- Step 4 の `git add` に `Localization/localization.csv` と `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` を足す。

**Task 7 への追加**
- Step 2 の正規表現 `"^Tests\."` はクライアント側のテストに当たらない。別に `"^Client\.Tests\.(WebUi|Localization)"` も実行する。

---

## Task 1: マスタスキーマとマスタJSONの更新

**Files:**
- Modify: `VanillaSchema/cleanRoom.yml`
- Modify: `VanillaSchema/blocks.yml`（`CleanRoomAirFilter` の `filterCapacity` 付近。1060行目あたり）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/cleanRoom.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`（清浄機ブロックの `filterCapacity`）
- Modify: `/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/cleanRoom.json`
- Modify: `/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`
- Modify: `/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/machineRecipes.json`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/CleanRoomMaster.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/CleanRoom/CleanRoomMasterUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`（再生成のトリガーとして。コミットしない）

**Interfaces:**
- Produces: 生成型 `ThresholdsElement` から `MaxConcentration` が消える。`CleanRoomMasterElement` から `Pollution`・`PollutionConnectorBlockGuids` が消える。`CleanRoomAirFilterBlockParam.FilterLifeSeconds`（数値。生成型の実際の型は再生成後に確認する）が生え、`FilterCapacity` が消える。`CleanRoomMaster` から `Pollution` プロパティと `IsPollutionConnectorBlock(Guid)` が消える。

- [ ] **Step 1: `.agents/skills/edit-schema/SKILL.md` を読む**

- [ ] **Step 2: `VanillaSchema/cleanRoom.yml` を編集する**

`data.items.properties` から `key: pollution` のブロック全体（`aVolume`〜`kHatch`）を削除する。`thresholds.items.properties` から `key: maxConcentration` の2行を削除する。`key: pollutionConnectorBlockGuids` のプロパティ定義全体を削除する。ファイル冒頭のコメント（日本語・英語とも）から「汚染係数・」「pollution coefficients, 」を消す。

- [ ] **Step 3: `VanillaSchema/blocks.yml` を編集する**

清浄機パラメータの

```yaml
        - key: filterCapacity
          type: number
          default: 5000
```

を

```yaml
        - key: filterLifeSeconds
          type: number
          default: 12
```

に置き換える。

- [ ] **Step 4: テスト用mod の JSON を更新する**

`forUnitTest/master/cleanRoom.json`: `pollution` オブジェクト、各 threshold の `maxConcentration`、`pollutionConnectorBlockGuids` を削除する。
`forUnitTest/master/blocks.json`: 清浄機ブロックの `"filterCapacity": <値>` を `"filterLifeSeconds": 12` に置き換える。

- [ ] **Step 5: `moorestech_master` の JSON を更新する**

`cleanRoom.json`: `pollution`、各 threshold の `maxConcentration`、`pollutionConnectorBlockGuids` を削除する。
`blocks.json`: 「クリーンルーム空気清浄機」の `"filterCapacity": 20000` を `"filterLifeSeconds": 12` に置き換える。
`machineRecipes.json`: クリーンルームフィルター（itemGuid `f4431bb1-35b9-4934-be95-de1641c65fd7`）を出力する電気精密組立機のレシピの `"time": 4` を `"time": 12` にする。
この3ファイルにはユーザーの未コミット変更が混ざっている。該当キー以外は1文字も変えない（整形し直さない。JSON を読み書きし直すツールで全体を書き出さず、Edit ツールで該当箇所だけ置換する）。**コミットしない。**

- [ ] **Step 6: `CleanRoomMaster.cs` と `CleanRoomMasterUtil.cs` から汚染を外す**

`CleanRoomMaster.cs`: `Pollution` プロパティ、両コンストラクタの `Pollution = ...` 行、`_pollutionConnectorBlockGuids` フィールドとその `new`・`UnionWith`（直前の2行コメントごと）、`IsPollutionConnectorBlock` メソッド（直前の2行コメントごと）を削除する。
`CleanRoomMasterUtil.cs`: `Pollution`・`PollutionConnectorBlockGuids` を検証している箇所を削除する。37〜42行目あたりの「閾値行が良い順に並んでいること」の検証は、今は `MaxConcentration` の昇順で見ている。これを **`RequiredAirChangeRate` が行順に厳密に減っていること**（前の行より大きいか等しい行があればエラー。エラーメッセージの書式は既存の行に合わせる）の検証へ書き換える。クラス判定は良い行から順に走査するので、この順序が崩れると上のクラスに届かなくなるため。テスト用mod と `moorestech_master` の値（0.0167 > 0.0083 > 0.0042 > 0.0014）はこの検証を通る。`CleanRoomMasterTest.cs` に、順序が崩れたマスタで `Validate` が false を返すテストを1本足す（既存の濃度順の検証テストがあればそれを置き換える）。

- [ ] **Step 7: SourceGenerator を再生成してコンパイルする**

edit-schema スキルの手順どおり `_CompileRequester.cs` を書き換えて再生成を起こす。

Run: `uloop compile --project-path ./moorestech_client`
Expected: この時点では `MaxConcentration`・`Pollution`・`FilterCapacity`・`IsPollutionConnectorBlock` を参照している箇所（Task 2〜4 で直す）のコンパイルエラーだけが残る。それ以外のエラーが出たらこのタスク内で直す。

- [ ] **Step 8: コミットしない**

コンパイルが通らない中間状態なので、Task 4 の終わりにまとめてコミットする。

## Task 2: 清浄機のフィルター定率消費

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomAirFilterComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/CleanRoom/ICleanRoomAirFilter.cs`

**Interfaces:**
- Consumes: Task 1 の `CleanRoomAirFilterBlockParam.FilterLifeSeconds`
- Produces: `ICleanRoomAirFilter` は `double RemovalVolumePerSecond { get; }` と `CleanRoomAirFilterStatus GetStatus()` だけになる（`ApplyRemovedImpurity` は消える）。`CleanRoomAirFilterComponent` は `IUpdatableBlockComponent` を実装する。セーブ状態 `CleanRoomAirFilterSaveJsonObject` は `[JsonProperty("filterWearSeconds")] public double FilterWearSeconds`（実行中は tick で持ち、セーブは秒。前例: `Game.Block/Blocks/PowerGenerator/VanillaElectricGeneratorSaveJsonObject.cs` の `remainingFuelSeconds`） と既存の `[JsonProperty("inventory")] Items` を持つ（`wearAccumulation` は消える）。

- [ ] **Step 1: `ICleanRoomAirFilter.cs` から `ApplyRemovedImpurity` とその2行コメントを削除する**

- [ ] **Step 2: `CleanRoomAirFilterComponent.cs` を書き換える**

クラス宣言に `IUpdatableBlockComponent` を足す（`CleanRoomItemHatchComponent` の実装を見て、`Update()` の形と `CheckDestroy` の呼び方を合わせる）。フィールドと該当メソッドを次のように置き換える。

```csharp
        private readonly float _requiredPower;
        private readonly double _removalVolumePerSecond;
        private readonly uint _filterLifeTicks;
        private readonly ItemId _filterItemId;

        // 電力tickの後処理で確定した現在電力
        // The current power settled by the electric tick post-process
        private float _currentPower;

        // 装填中のフィルター1個が使われたtick数
        // Ticks the loaded filter has been in use
        private uint _filterWearTicks;
```

コンストラクタ（`_filterCapacity = param.FilterCapacity;` の行を置き換える）:

```csharp
            // 寿命が正であることはマスタ検証で保証済み
            // The master validation guarantees a positive life
            _filterLifeTicks = GameUpdater.SecondsToTicks(param.FilterLifeSeconds);
```

ロード用コンストラクタ（`_wearAccumulation = saveData.WearAccumulation;` を置き換える）:

```csharp
            _filterWearTicks = GameUpdater.SecondsToTicks(saveData.FilterWearSeconds);
```

`ApplyRemovedImpurity` を削除し、代わりに `Update()` を足す:

```csharp
        public void Update()
        {
            CheckDestroy(this);

            // 通電していてフィルターがある間だけ寿命を進める。部屋やクラスには依存しない
            // Advance the life only while powered with a filter loaded; independent of rooms and classes
            if (PowerRatio <= 0 || !HasFilterItem) return;
            _filterWearTicks++;

            // 寿命に達したらフィルターを1個消費して数え直す
            // Consume one filter when the life is reached and start counting again
            if (_filterWearTicks < _filterLifeTicks) return;
            _filterWearTicks = 0;
            FilterSlot.SetItem(0, FilterSlot.GetItem(0).SubItem(1));
        }
```

`GetStatus()` の残り寿命:

```csharp
            var filterCount = HasFilterItem ? FilterSlot.GetItem(0).Count : 0;
            var remainingRatio = filterCount == 0 ? 0d : Math.Max(0d, 1d - (double)_filterWearTicks / _filterLifeTicks);
            return new CleanRoomAirFilterStatus(PowerRatio, RemovalVolumePerSecond, filterCount, remainingRatio);
```

`GetSaveState()` とセーブ用クラス:

```csharp
            var saveData = new CleanRoomAirFilterSaveJsonObject
            {
                FilterWearSeconds = GameUpdater.TicksToSeconds(_filterWearTicks),
                Items = FilterSlot.InventoryItems.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
            };
```

```csharp
    public class CleanRoomAirFilterSaveJsonObject
    {
        [JsonProperty("filterWearSeconds")]
        public double FilterWearSeconds;

        [JsonProperty("inventory")]
        public List<ItemStackSaveJsonObject> Items;
    }
```

`using Core.Update;` を足す。`param.FilterLifeSeconds` の生成型が `float`/`double`/`int` のどれでも `SecondsToTicks(double)` に渡せる。

`IUpdatableBlockComponent` を足したことで、このコンポーネントの `Update()` が毎tick呼ばれることを、清浄機ブロックを組み立てているテンプレート（`CleanRoomAirFilter` で Grep して見つかる `Vanilla...Template`）と、`IUpdatableBlockComponent` を集めて回している側のコードを読んで確かめる。コンポーネント一覧に入っていれば自動で回る。入っていなければテンプレート側で一覧へ足す。

## Task 3: クラス判定を換気回数だけにし、汚染流入を削除する

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/Util/CleanRoomPurityLogic.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoom.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/Util/CleanRoomCarryOver.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/Info/CleanRoomRoomSummary.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/Save/CleanRoomSaveData.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/Save/CleanRoomSaveRestore.cs`
- Delete: `moorestech_server/Assets/Scripts/Game.CleanRoom/Util/CleanRoomPollution.cs`（`.meta` も一緒に `git rm`）
- Delete: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/CleanRoom/ICleanRoomItemHatch.cs`（`.meta` も一緒に `git rm`）
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/CleanRoom/ICleanRoomMachine.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomItemHatchComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/CleanRoomMachineComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/CleanRoomMachineProcessorComponent.cs`

**Interfaces:**
- Consumes: Task 1 の生成型、Task 2 の `ICleanRoomAirFilter`
- Produces:
  - `CleanRoomPurityLogic.AirChangeRate(double removalVolumePerSecond, int volume)`（不変）
  - `CleanRoomPurityLogic.DecideThresholdIndex(int currentIndex, double airChangeRate, IReadOnlyList<double> requiredAirChangeRates)` → `int`
  - `CleanRoomPurityLogic.PromoteAirChangeFactor`（不変・1.25）
  - `CleanRoom` から `ImpurityCount`・`SetImpurity` が消える
  - `CleanRoomRoomSummary` から `Concentration` が消える（`Class`・`AirChangeRate`・`Volume`・`AirFilters` は残る）
  - `CleanRoomSaveData` から `ImpurityCount` が消える（`className`・`cells` は残る）
  - `ICleanRoomMachine` は `void SetCleanRoomEffect(CleanRoomEffect effect);` だけになる

- [ ] **Step 1: `CleanRoomPurityLogic.cs` を次の中身にする**

```csharp
using System.Collections.Generic;

namespace Game.CleanRoom.Util
{
    /// <summary>
    ///     部屋クラス判定の純関数群（換気回数と閾値行判定）
    ///     Pure functions for the room class decision (air change rate and threshold row decision)
    /// </summary>
    public static class CleanRoomPurityLogic
    {
        // 昇格時のみ課すヒステリシス係数（換気を多めに要求）
        // Hysteresis factor imposed only on promotion (more air change required)
        public const double PromoteAirChangeFactor = 1.25;

        // 換気回数 nq/V。体積0の部屋は0とする。クラス判定と表示の両方がここを呼ぶ
        // Air change rate nq/V; zero for a zero-volume room, shared by the class decision and the display
        public static double AirChangeRate(double removalVolumePerSecond, int volume)
        {
            return volume <= 0 ? 0 : removalVolumePerSecond / volume;
        }

        public static int DecideThresholdIndex(int currentIndex, double airChangeRate, IReadOnlyList<double> requiredAirChangeRates)
        {
            // 良い行から順に判定し、現在行より上を狙うときだけ昇格マージンを課す
            // Scan rows best-first, imposing the promotion margin only when aiming above the current row
            for (var i = 0; i < requiredAirChangeRates.Count; i++)
            {
                var isPromotion = i < currentIndex;
                var requiredAirChange = isPromotion ? requiredAirChangeRates[i] * PromoteAirChangeFactor : requiredAirChangeRates[i];
                if (airChangeRate >= requiredAirChange) return i;
            }

            // どの行も満たさなければ Out（行数）を返す
            // Return Out (the row count) when no row is satisfied
            return requiredAirChangeRates.Count;
        }
    }
}
```

`CleanRoomThresholdRow` 構造体・`IntegrateTick`・`Concentration`・`PromoteConcentrationFactor` は削除する。

- [ ] **Step 2: `CleanRoomDatastore.cs` の `UpdatePurity` と `BuildThresholdRows` を置き換える**

フィールド `_thresholdRows` を `private readonly List<double> _requiredAirChangeRates;` に変え、コンストラクタの代入も `_requiredAirChangeRates = BuildRequiredAirChangeRates();` にする。`_world` は `CleanRoomAirFilterCollector.Collect` が使うので残す。

```csharp
        private void UpdatePurity()
        {
            // 閾値マスタが空ならクラス判定全体を無効化する
            // With no threshold master, the whole class decision is disabled
            if (_requiredAirChangeRates.Count == 0) return;

            foreach (var room in _detectionService.Rooms)
            {
                // 清浄機の除去能力合計と空きセル数から、毎tick閾値行を決める
                // Decide the threshold row each tick from the purifiers' total removal volume and the empty cell count
                var filters = CleanRoomAirFilterCollector.Collect(room, _world);
                var removalVolume = CleanRoomAirFilterCollector.SumRemovalVolume(filters);
                var airChangeRate = CleanRoomPurityLogic.AirChangeRate(removalVolume, room.Volume);
                room.SetThresholdIndex(CleanRoomPurityLogic.DecideThresholdIndex(room.ThresholdIndex, airChangeRate, _requiredAirChangeRates));
            }
        }

        private static List<double> BuildRequiredAirChangeRates()
        {
            // マスタの閾値行から必要換気回数だけを初期化時に1回取り出す
            // Extract only the required air change rates from the master threshold rows once at initialization
            var rates = new List<double>();
            foreach (var threshold in MasterHolder.CleanRoomMaster.Thresholds) rates.Add(threshold.RequiredAirChangeRate);
            return rates;
        }
```

`Update()` 上の2行コメントの「純度シミュレーション」「purity simulation」を「クラス判定」「class decision」に直す。使わなくなった `using`（`Core.Update`・`System` など）はコンパイル警告を見て消す。

- [ ] **Step 3: `CleanRoom.cs` から不純物を消す**

`ImpurityCount` プロパティ、コンストラクタの `ImpurityCount = 0;`、`SetImpurity` メソッド（直前の2行コメントごと）を削除する。`SurfaceArea` は残す。`using System;` が不要になれば消す。

- [ ] **Step 4: `CleanRoomCarryOver.cs` を閾値行の引き継ぎだけにする**

`Apply` の中身を次にする（`CountOverlap` と `#region Internal` は残す）。クラスの `<summary>` も「閾値行を引き継ぐ」「Carries the threshold row」に直す。

```csharp
            foreach (var newRoom in newRooms)
            {
                // 最も多くセルが重なる旧部屋の閾値行を引き継ぐ
                // Carry the threshold row from the old room with the largest cell overlap
                CleanRoom maxOverlapRoom = null;
                var maxOverlap = 0;
                foreach (var oldRoom in oldRooms)
                {
                    var overlap = CountOverlap(newRoom, oldRoom);
                    if (overlap <= maxOverlap) continue;
                    maxOverlap = overlap;
                    maxOverlapRoom = oldRoom;
                }

                // 重なる旧部屋が無ければ初期状態（行=Out）のまま
                // With no overlapping old room, keep the fresh state (row=Out)
                if (maxOverlapRoom == null) continue;
                newRoom.SetThresholdIndex(maxOverlapRoom.ThresholdIndex);
            }
```

- [ ] **Step 5: `CleanRoomRoomSummary.cs` から濃度を消す**

`Concentration` プロパティ、コンストラクタ引数 `double concentration` と代入、`Create` 内の濃度計算を削除する。`<summary>` の「濃度・」「concentration, 」を消す。`Create` は次で終わる:

```csharp
            // 換気回数はクラス判定と同じ関数で出す（体積0の部屋は0）
            // The air change rate comes from the same function as the class decision (zero for a zero-volume room)
            var airChangeRate = CleanRoomPurityLogic.AirChangeRate(removalVolume, room.Volume);
            return new CleanRoomRoomSummary(CleanRoomClassStanding.Of(room), airChangeRate, room.Volume, airFilters);
```

- [ ] **Step 6: セーブから不純物を消す**

`CleanRoomSaveData.cs`: `[JsonProperty("impurityCount")] public double ImpurityCount;` の行を削除する。
`CleanRoomSaveRestore.cs`: `BuildSaveData` の `ImpurityCount = room.ImpurityCount,` を削除する。`Restore` は「同じ現行部屋へ集約された保存レコードのうち、重なりが最大のレコードのクラス名を採る」だけにする。`RestoreSelection` から `ImpurityCount` を消し（コンストラクタは `(string className, int bestOverlap)`）、合算の行と `selection.Key.SetImpurity(...)` を削除する。合算に触れている2行コメントは「同じ現行部屋へ集約された保存レコードは、重なりが最大のもののクラスを採る」「Among saved records mapped to the same current room, take the class of the largest overlap」に直す。

- [ ] **Step 7: 汚染流入を削除する**

- `git rm` で `Game.CleanRoom/Util/CleanRoomPollution.cs` と `.meta` を消す。
- `ICleanRoomMachine.cs`: `IsPolluting` とその2行コメントを削除する。
- `CleanRoomMachineComponent.cs`: `public bool IsPolluting => _processor.IsPolluting;` を削除する。
- `CleanRoomMachineProcessorComponent.cs`: `public bool IsPolluting => CurrentState == ProcessState.Processing;` を削除する（他に参照が無いことを Grep で確かめる）。
- `CleanRoomItemHatchComponent.cs`: `ICleanRoomItemHatch` の実装宣言、`ThroughputWindowTicks`、`RecentThroughputPerSecond`、`_pushedPerTick`・`_windowSum`・`_windowIndex` と、それらを更新している行を削除する。**`maxItemsPerSecond` による毎秒搬送上限の仕組みが同じ窓変数を使っている場合は、上限の仕組みが使う分は残す**（`CleanRoomItemHatchThroughputCapTest` が通り続けること）。窓変数を消した結果 `Update()` が空になるなら、`IUpdatableBlockComponent` の実装ごと外す。
- `git rm` で `ICleanRoomItemHatch.cs` と `.meta` を消す。先に `ICleanRoomItemHatch` を Grep し、汚染以外で使っている箇所（テストを含む）があればそこを `CleanRoomItemHatchComponent` の直接参照へ直す。

## Task 4: プロトコル・サーバーテスト・コンパイル

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/CleanRoom/CleanRoomSummaryMessagePack.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/CleanRoom/CleanRoomInfoMessagePackFactory.cs`
- Delete: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomHatchPollutionTest.cs`（`.meta` も）
- Delete: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/Usability/CleanRoomDoorPollutionTest.cs`（`.meta` も）
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/CleanRoomPurityLogicTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomPurityTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomAirFilterTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomMasterTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomBoundaryBlockSetMasterTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomIncrementalDetectionTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomHatchTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomSaveLoadTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/Usability/CleanRoomInfoQueryTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/Usability/CleanRoomItemHatchThroughputCapTest.cs`

**Interfaces:**
- Consumes: Task 2・3 の全シグネチャ
- Produces: `CleanRoomSummaryMessagePack.CreateClassified(string className, int thresholdIndex, double airChangeRate, int volume, List<CleanRoomAirFilterMessagePack> airFilters)`、`CreateOut(double airChangeRate, int volume, List<CleanRoomAirFilterMessagePack> airFilters)`。`Concentration` プロパティは無い。

- [ ] **Step 1: `CleanRoomSummaryMessagePack.cs` から `Concentration` を消す**

`[Key(3)] public double Concentration` を削除し、後ろのキー番号を詰める（このファイルの他の `[Key(n)]` が連番になるようにする）。コンストラクタと2つのファクトリから `double concentration` 引数を削除する。`CleanRoomInfoMessagePackFactory.cs` の呼び出し2箇所から `room.Concentration` を削除する。サーバーとクライアントは同じ型定義を共有しているので、キー番号を詰めても不整合にならない（クライアント側の参照は Task 6 で直す）。

- [ ] **Step 2: 汚染のテスト2本を `git rm` する**

`CleanRoomHatchPollutionTest.cs`・`CleanRoomDoorPollutionTest.cs` と各 `.meta`。汚染という検証対象そのものが無くなるため。

- [ ] **Step 3: `CleanRoomPurityLogicTest.cs` をクラス判定のテストに書き換える**

既存のテストメソッドのうち `IntegrateTick`・`Concentration` を検証するものは削除し、`DecideThresholdIndex` を次の4本で検証する（既存ファイルの名前空間・`using` に合わせる）。

```csharp
        private static readonly double[] RequiredAirChangeRates = { 0.0167, 0.0083, 0.0042, 0.0014 };

        [Test]
        public void 換気回数が足りなければOutになる()
        {
            var index = CleanRoomPurityLogic.DecideThresholdIndex(4, 0.0010, RequiredAirChangeRates);
            Assert.AreEqual(4, index);
        }

        [Test]
        public void 昇格には必要換気回数の1_25倍が要る()
        {
            // Out(4) から B(1) へ: 0.0083 ちょうどでは届かず C(2) 止まり、0.0083×1.25 以上で B になる
            // From Out(4) to B(1): exactly 0.0083 stops at C(2); 0.0083 x 1.25 or more reaches B
            Assert.AreEqual(2, CleanRoomPurityLogic.DecideThresholdIndex(4, 0.0083, RequiredAirChangeRates));
            Assert.AreEqual(1, CleanRoomPurityLogic.DecideThresholdIndex(4, 0.0083 * 1.25, RequiredAirChangeRates));
        }

        [Test]
        public void 現在のクラスは必要換気回数ちょうどで維持できる()
        {
            Assert.AreEqual(1, CleanRoomPurityLogic.DecideThresholdIndex(1, 0.0083, RequiredAirChangeRates));
        }

        [Test]
        public void 換気回数が下がればその場で降格する()
        {
            Assert.AreEqual(2, CleanRoomPurityLogic.DecideThresholdIndex(1, 0.0082, RequiredAirChangeRates));
        }

        [Test]
        public void 体積0の部屋の換気回数は0()
        {
            Assert.AreEqual(0, CleanRoomPurityLogic.AirChangeRate(5, 0));
        }
```

- [ ] **Step 4: `CleanRoomAirFilterTest.cs` を定率消費のテストに書き換える**

既存テストのうち「除去した不純物量で摩耗する」前提のものは削除する。既存の部屋・清浄機の組み立てヘルパー（同ファイルと `Helpers/` にある）をそのまま使い、次の4つの挙動を検証するテストを置く。tick の進め方・電力の与え方は同ファイルの既存テストの方法に合わせる。`FilterLifeTicks` はテスト用mod の `filterLifeSeconds: 12` から `GameUpdater.SecondsToTicks(12)` で求める。

1. 満電力・フィルター10個の清浄機を `SecondsToTicks(60)` tick 進めるとフィルターが5個になる。
2. 無通電（電力割合0）の清浄機を `SecondsToTicks(60)` tick 進めてもフィルターは10個のままで、`GetStatus().CurrentFilterRemainingRatio` は 1。
3. 同じ部屋に清浄機を2台置いて各10個入れ、満電力で `SecondsToTicks(60)` tick 進めると両方とも5個になる（台数で消費が薄まらない）。
4. フィルター1個・満電力で寿命のtick数だけ進めるとフィルターが0個になり、`RemovalVolumePerSecond` が 0 になる。

- [ ] **Step 5: 残りのテストを新しい形に合わせる**

- `CleanRoomPurityTest.cs`: 不純物の積分を検証しているテストは削除する。「清浄機が足りる部屋はクラスが付き、清浄機を止める（無通電にする・フィルターを抜く）と次のtickで Out になる」を検証する1本にする。
- `CleanRoomMasterTest.cs`・`CleanRoomBoundaryBlockSetMasterTest.cs`: `Pollution`・`MaxConcentration`・`IsPollutionConnectorBlock`・`PollutionConnectorBlockGuids` を検証している箇所を削除する。汚染接続点集合だけを検証していたテストメソッドはメソッドごと削除する。
- `CleanRoomSaveLoadTest.cs`: `ImpurityCount`・`SetImpurity` を使っている箇所を、クラス名（`ClassName`）の保存復元の検証に置き換える。清浄機の `wearAccumulation` を見ている箇所は `filterWearSeconds` に直す。
- `CleanRoomIncrementalDetectionTest.cs`: 不純物の引き継ぎを検証している箇所を、閾値行の引き継ぎの検証に置き換える。
- `CleanRoomHatchTest.cs`・`CleanRoomItemHatchThroughputCapTest.cs`: `RecentThroughputPerSecond`・`ICleanRoomItemHatch` を見ている箇所を削除または直接参照に直す。毎秒搬送上限の検証は残す。
- `CleanRoomInfoQueryTest.cs`: `Concentration` の検証を削除する。

削除と置換だけで足り、検証対象が残っているテストは意図を保つ。**テストを通すためにアサーションを弱めない**（検証対象が今回の裁定で無くなったものだけを消す）。

- [ ] **Step 6: コンパイルしてサーバーのクリーンルーム系テストを通す**

Run: `uloop compile --project-path ./moorestech_client`
Expected: クライアント側（`Client.WebUiHost` の `CleanRoomDetailDtoBuilder.cs`）で `Concentration` 参照のエラーが出る場合は、Task 6 の Step 1（クライアント DTO）を先に行ってから再コンパイルし ErrorCount 0 にする。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"`
Expected: 全件 PASS

- [ ] **Step 7: コミットする**

```bash
git add VanillaSchema/cleanRoom.yml VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Tests.Module moorestech_server/Assets/Scripts/Core.Master/CleanRoomMaster.cs moorestech_server/Assets/Scripts/Core.Master/Validator moorestech_server/Assets/Scripts/Game.CleanRoom moorestech_server/Assets/Scripts/Game.Block moorestech_server/Assets/Scripts/Game.Block.Interface moorestech_server/Assets/Scripts/Server.Protocol moorestech_server/Assets/Scripts/Tests
git status --short   # _CompileRequester.cs と .moorestech-external-revisions.json が staged に入っていないことを確かめる
git commit -m "feat(cleanroom): クラスを換気回数だけで決め、フィルターを清浄機ごとの定率消費にする"
```

## Task 5: セーブマイグレーション V11→V12

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V11ToV12/SaveMigrationStepV11ToV12.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V11ToV12/CleanRoomImpurityRemover.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V11ToV12/CleanRoomAirFilterWearConverter.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs:26`（`CurrentVersion = 12`）
- Modify: `SaveMigrationStepV10ToV11` を登録している箇所全部（`Server.Boot/Composition/SaveAndEventServiceRegistration.cs`、`Tests/CombinedTest/Game/SaveLoadPreparerTestFixture.cs`、`Tests/UnitTest/Game/SaveLoad/SaveMigrationChainTest.cs`、`Tests/UnitTest/Game/SaveLoad/Migration/SaveMigrationStepV7ToV8Test.cs`、`Tests/UnitTest/Game/SaveLoad/Migration/SaveMigrationStepV8ToV9Test.cs` の計5か所）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/Migration/V11ToV12/SaveMigrationStepV11ToV12Test.cs`

**Interfaces:**
- Consumes: `ISaveMigrationStep`・`ISaveMigrationPart`・`MigrationSaveJson.RunParts`・`SaveMigrationStepResult`（`Steps/Shared/` と `SaveMigrationStepV10ToV11.cs` の前例）
- Produces: `public sealed class SaveMigrationStepV11ToV12 : ISaveMigrationStep`（`FromVersion => 11`）

- [ ] **Step 1: `.agents/skills/moorestech-save-migration/SKILL.md` と前例を読む**

前例: `Steps/SaveMigrationStepV10ToV11.cs`、`Steps/V10ToV11/CleanRoomPanelWorldRemover.cs`（world 配列のブロックを走査する `ISaveMigrationPart` の書き方）、`Tests/UnitTest/Game/SaveLoad/Migration/V10ToV11/SaveMigrationStepV10ToV11Test.cs`。

- [ ] **Step 2: ステップと部品2つを書く**

`SaveMigrationStepV11ToV12.cs`:

```csharp
using Game.SaveLoad.Migration.Steps.Shared;
using Game.SaveLoad.Migration.Steps.V11ToV12;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps
{
    // V11→V12: クリーンルームの濃度を廃止しフィルターを定率消費にしたため、部屋の不純物量を消し、清浄機の摩耗を経過tickへ置き換える
    // V11->V12: clean-room concentration was removed and filters now wear at a fixed rate, so drop room impurity and replace purifier wear with elapsed ticks
    public sealed class SaveMigrationStepV11ToV12 : ISaveMigrationStep
    {
        public int FromVersion => 11;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            var parts = new ISaveMigrationPart[]
            {
                new CleanRoomImpurityRemover(),
                new CleanRoomAirFilterWearConverter(),
            };
            return MigrationSaveJson.RunParts(FromVersion, parts, save);
        }
    }
}
```

部品の仕様（`ISaveMigrationPart` のメソッド形と失敗の返し方は `CleanRoomPanelWorldRemover.cs` に合わせる）:

- `CleanRoomImpurityRemover`: `save["cleanRoomRooms"]` が配列なら、各要素（オブジェクト）から `impurityCount` プロパティを削除する。`cleanRoomRooms` キーが無い、または値が明示 null のセーブは何もせず成功（ローダー `WorldSaveAllInfo` が null を空リストとして受け入れているため。判定は `Steps/Shared/MigrationSaveJson.IsAbsentOrNull` を使う）。キーがあり null でもないのに配列でない、または要素がオブジェクトでない場合は `Failed(理由)` を返す。
- `CleanRoomAirFilterWearConverter`: `save["world"]` の各ブロックの `state` オブジェクトのうち、キー `Game.Block.Blocks.CleanRoom.CleanRoomAirFilterComponent` を持つものについて、その値オブジェクトから `wearAccumulation` を削除し `filterWearSeconds: 0` を入れる。旧値は「吸い取った不純物の量」で新しい経過tickへ換算する根拠が無いため、装填中のフィルターは新品扱いにする。状態の値が文字列で入っている形式かオブジェクトで入っている形式かは、`CleanRoomPanelWorldRemover.cs` と `Steps/Shared/` の既存ヘルパーが world のブロック状態をどう読んでいるかに合わせる（実セーブではオブジェクト: `"state": {"Game.Block.Blocks.CleanRoom.CleanRoomAirFilterComponent": {"wearAccumulation": 14704.55, "inventory": [...]}}`）。world のブロック状態の走査は `Steps/Shared/MigrationSaveJson.TryCollectBlockStates` を使う。キーを持たないブロック、キーの値が明示 null のブロックは触らない（`BlockComponentStateReader.TryRead` が明示 null を読み飛ばすのに合わせる。前例: `CleanRoomMachineFluidSlotFiller`）。値が null でもオブジェクトでもない場合だけ `Failed(理由)`。

- [ ] **Step 3: `CurrentVersion` を 12 にし、連鎖へ登録する**

`WorldSaveAllInfo.CurrentVersion = 12`。`new SaveMigrationStepV10ToV11()` を並べている全箇所（Files 節の5ファイル。念のため `SaveMigrationStepV10ToV11` を Grep して全件）で、その直後に `new SaveMigrationStepV11ToV12()` を足す。`SaveMigrationChainTest.cs` に「最新版は11」のような版数の期待値があれば12へ直す。

- [ ] **Step 4: テストを書く**

`SaveMigrationStepV11ToV12Test.cs`（`SaveMigrationStepV10ToV11Test.cs` の組み立て方に合わせる）。検証:

1. `cleanRoomRooms: [{ "impurityCount": 18757.79, "className": "C", "cells": [{"x":1,"y":2,"z":3}] }]` と、world に `"state": {"Game.Block.Blocks.CleanRoom.CleanRoomAirFilterComponent": {"wearAccumulation": 14704.55, "inventory": [{"itemGuid": "f4431bb1-35b9-4934-be95-de1641c65fd7", "count": 100}]}}` を持つブロック1個を含む V11 セーブを渡すと成功し、部屋に `impurityCount` が無く `className` と `cells` は元のまま、清浄機の状態に `wearAccumulation` が無く `filterWearSeconds` が 0、`inventory` は元のまま。
2. `cleanRoomRooms` キーが無く、清浄機も無い V11 セーブは成功し、内容が変わらない。
3. `cleanRoomRooms` が配列でない（例: 文字列）セーブは `Failed` を返す。
4. `"cleanRoomRooms": null` のセーブ、清浄機の状態の値が null のブロックを持つセーブは成功し、その箇所は変わらない。

- [ ] **Step 5: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveMigration|SaveLoad|CleanRoom"`
Expected: 全件 PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.SaveLoad moorestech_server/Assets/Scripts/Server.Boot moorestech_server/Assets/Scripts/Tests
git commit -m "feat(save): V11→V12 でクリーンルームの不純物量と清浄機の摩耗量を移行する"
```

## Task 6: クライアント DTO と webui から濃度を消す

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/CleanRoom/CleanRoomDetailDtos.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/CleanRoom/CleanRoomDetailDtoBuilder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_clean_room_machine.json`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/cleanRoom/index.ts`
- Modify: `moorestech_web/webui/src/features/blockInventory/details/cleanRoom/cleanRoomLogic.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/__tests__/inventory.test.ts`
- Modify: `moorestech_web/webui/src/features/blockInventory/details/cleanRoom/__tests__/cleanRoomLogic.test.ts`
- Modify: `moorestech_web/webui/src/features/blockInventory/details/cleanRoom/__tests__/CleanRoomSection.test.ts`
- Modify: `moorestech_web/webui/src/features/blockInventory/views/__tests__/SectionStackView.test.tsx`
- Modify: 表示文言 `ui.blockInventory.cleanRoomAir` の定義元（`cleanRoomAir` を Grep して見つかる翻訳ソース。`moorestech_web/webui/src/shared/i18n/generated/` は生成物なので手で直さず、生成元を直して再生成する）

**Interfaces:**
- Consumes: Task 4 の `CleanRoomSummaryMessagePack`（`Concentration` 無し）
- Produces: `CleanRoomSummaryDto.CreateClass(string className, double airChangeRate, int volume, <airFilters>)`、`CreateClassOut(double airChangeRate, int volume, <airFilters>)`。webui の `CleanRoomSummaryData` に `concentration` は無い。

- [ ] **Step 1: クライアント DTO から濃度を消す**

`CleanRoomDetailDtos.cs`: `Concentration` フィールド、private コンストラクタの `double concentration` 引数と代入、`CreateClass`・`CreateClassOut` の `double concentration` 引数を削除する。
`CleanRoomDetailDtoBuilder.cs`（128〜129行目）: 2つの呼び出しから `room.Concentration` を削除する。
`block_inventory_clean_room_machine.json`（14行目）: `"concentration": 1.5,` の行を削除する。

- [ ] **Step 2: webui から濃度を消す**

`schemas/cleanRoom/index.ts`（16行目）: `concentration: z.number().nonnegative(),` を削除する。
`cleanRoomLogic.ts`（47行目）: `air` の `params` から `concentration` を削除する。
表示文言 `cleanRoomAir` の各言語の文から濃度の部分（`{concentration}` を含む句）を削除し、換気回数だけの文にする。生成元を直したら、リポジトリに書いてある i18n 生成コマンド（`moorestech_web/webui/package.json` の scripts を見る）で `generated/` を再生成する。
テスト4ファイル: フィクスチャから `concentration: ...` を削除し、`cleanRoomLogic.test.ts`（20行目）の期待値の `params` から `concentration: "0.123"` を削除する。

- [ ] **Step 3: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom|WebUi"` → 全件 PASS
Run: `cd moorestech_web/webui && pnpm install --frozen-lockfile && pnpm test -- cleanRoom inventory SectionStackView && pnpm exec tsc --noEmit`（スクリプト名は `package.json` に合わせる）
Expected: 全件 PASS・型エラー0

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests moorestech_web/webui
git commit -m "feat(webui): クリーンルームの部屋情報から濃度を外す"
```

## Task 7: 全体確認（第1段階の終わり）

- [ ] **Step 1: 残骸の検索**

Run（リポジトリ直下）:
```bash
grep -rnE "ImpurityCount|SetImpurity|MaxConcentration|maxConcentration|IsPolluting|RecentThroughputPerSecond|IsPollutionConnectorBlock|pollutionConnectorBlockGuids|FilterCapacity|filterCapacity|wearAccumulation|ApplyRemovedImpurity|CleanRoomPollution" moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts moorestech_web/webui/src VanillaSchema ../moorestech_master/server_v8 --include="*.cs" --include="*.ts" --include="*.tsx" --include="*.yml" --include="*.json"
```
Expected: ヒットは `Game.SaveLoad/Migration/Steps/V11ToV12/` とそのテスト（旧キー名 `impurityCount`・`wearAccumulation` を扱う箇所）だけ。

- [ ] **Step 1.5: 汚染・不純物に触れたコメントを直す**

`ICleanRoomAirFilter.cs`・`CleanRoomAirFilterComponent.cs` の `<summary>`（不純物を除去）、`CleanRoomItemHatchComponent.cs` の `<summary>`（汚染計算へ公開）、`Game.Block/Factory/BlockTemplate/CleanRoom/VanillaCleanRoomMachineTemplate.cs` の pollution reporting に触れたコメントを、今の役割（換気でクラスを上げる清浄機／境界のアイテム搬入口）に合う文へ直す。`grep -rnE "不純物|汚染|impurit|pollut" moorestech_server/Assets/Scripts --include="*.cs" -i` で、マイグレーション以外に残っていないことを確かめる。

- [ ] **Step 2: サーバーテスト全体**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\." --timeout-seconds 1500`
Expected: 全件 PASS。今回の変更と無関係に元から落ちているテストがあれば、`git stash` はせず、master での結果を知る手段（テスト名で `bd` を検索・既知の失敗一覧）で確かめ、報告に「元から失敗」と証拠付きで分けて書く。

- [ ] **Step 3: 実マスタでのロード確認**

`/Users/katsumi/moorestech_master/server_v8` を読むサーバー起動経路のテスト（マスタ検証を走らせるもの。`MasterJsonFileContainer`・`moorestechAlphaMod` を Grep して既存の「実modをロードして検証する」テストがあればそれ）を実行する。無ければ、`uloop execute-dynamic-code` でマスタロードと `CleanRoomMaster.Validate` を実行し、エラーが出ないことを確かめる。

- [ ] **Step 4: 報告**

変更したファイル、テスト結果（件数・失敗があればログ）、`moorestech_master` 側で未コミットのまま残した変更（ファイルと該当キー）、plan から外れた箇所と理由を報告する。**ここで止まる。Task 8 以降は第2段階であり、実行しない。**

---

## Task 8（第2段階）: unityプレイ録画テスト

ユーザーのテストプレイと並行して行う。`unity-playmode-recorded-playtest` スキルで、清浄機を置いた密閉部屋でクラスが付くこと、フィルターが12秒に1個減ること、清浄機UIに濃度が出ないことを録画で確かめる。

## Task 9（第2段階・省略不可）: moores-code-review

必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー指摘の反映がソースの判定経路・条件式・その評価時点に触れたら、Task 8 を反映後のバイナリで再実施する。

## Task 10（第2段階・省略不可）: PR 作成

セッション終了可能状態にすること: `moorestech_master` 側は、作業前からあったユーザーの未コミット変更（退避パッチ: `/private/tmp/claude-501/-Users-katsumi-moorestech/ce7e337e-8622-4508-bd86-a132c0c6c89f/scratchpad/master-user-uncommitted-before.patch`）と今回の変更を分け、今回の変更だけをブランチにコミット・push して PR を作る（分け方はユーザーに確認する）。本repoの `.moorestech-external-revisions.json` のピンをそのコミットへ更新する。pr-create スキルで本repoの PR を作成し、master とのコンフリクトがあれば master をマージして解消・コンパイル確認のうえ push する。全作業がコミット・push 済みで、セッションをそのまま閉じても PR がマージ可能な状態になっていることを確認して終える。

---

## 判断記録（ADR）

設計 ADR は作っていない（grill を省いたため）。裁定の正本は `.decisions/` の2本。

- D1 フィルターは清浄機1台につき毎分5個消費し、組立機の生産も毎分5個にする。必要台数（必要換気回数）は変えない。
  出所: ユーザー裁定 2026-11-13 原文「フィルター1台につき、毎分5個の消費量にする。また、電気精密組立機の生産量もデフォルトで毎分5個にする。」「いや、必要台数は変えなくて良いクラスAにはそれをするだけのトレードオフが十分にある」（[[2026-11-13-フィルターは清浄機1台につき毎分5個消費し組立機の生産も毎分5個にする]]）
- D2 クラスの条件から濃度を外し、部屋の広さに対する清浄機の台数だけで決める。
  出所: ユーザー裁定 2026-11-13 agent 提示「濃度の条件は外して、クラスを「部屋の広さに対する清浄機の台数」だけで決める形に単純化するのをすすめます。」→ 原文「←それでok」（[[2026-11-13-クリーンルームの濃度条件を外しクラスは部屋の広さに対する清浄機の台数だけで決める]]）
- D3 汚染流入の仕組みも消し、セーブ形式の変更込みで実装する。
  出所: ユーザー裁定 2026-11-13 agent 質問「(1) 濃度と一緒に、ハッチ数・機械の稼働・壁面積などによる汚染流入の仕組みも消してよいか。(2) フィルター消費の変更と濃度の削除を、セーブ形式の変更込みで実装に進めてよいか。」→ 原文「1,2両方ok」
- D4 進め方: grill を省き、plan を書いて Opus 5.5 に実装とテスト通過までを任せる。レビュー・unityプレイ録画テスト・PR はユーザーのテストプレイと並行する第2段階にする。実装は新規 Codex セッションではなく、このセッションから Opus の subagent に依頼する。
  出所: ユーザー裁定 2026-11-12「続けて。grill不要。どうするべきか考えて」、2026-11-13 原文「ここはmac proだからminiと環境がちがく。とりあえずwriting-planしてopous5.5に実装を依頼し、サクッと実装とテスト、テストのパスまで終わらせて、その後私がテストプレイしつつ、並行してレビューとかPr作成とかちゃんとしたことをやる、という方針で」
- D5 清浄機は「通電していてフィルターがある間」寿命を進める。部屋の外に置いた清浄機や、密閉が破れた部屋の清浄機も消費する。電力割合が1未満でも、0より大きければ同じ速さで消費する。
  出所: agent前提（ユーザー原文「フィルターの消費量は常に一定のほうが直感的」を「機械が動いていれば一定」と読んだ。部屋の内外で分けると清浄機が部屋を知る必要が出て、摩耗を部屋から押し込む今の逆向きの依存が残る）
- D6 マスタのパラメータ名は `filterLifeSeconds`（フィルター1個の寿命・秒）にし、毎分5個は 12 と書く。
  出所: agent前提（レシピの `time` が秒単位である前例に合わせた。「毎分の消費数」で持つと 60÷n の割り算と端数の扱いがコード側に要る）
- D7 V11 セーブの清浄機の摩耗量は捨て、装填中のフィルターを新品扱い（`filterWearSeconds: 0`）にする。
  出所: agent前提（旧値は吸い取った不純物の量で、経過tickへ換算する根拠が無い。差は最大でフィルター1個ぶん）
- D8 昇格が濃度の低下を待たなくなり、換気回数が昇格条件（×1.25）を満たしたtickでクラスが上がる。降格は今と同じ（今も換気回数が足りなくなったtickで下がる）。
  出所: agent前提（D2 の帰結。昇格ヒステリシスは「必要台数は変えない」を保つため残した。現行 `DecideThresholdIndex` は濃度と換気回数の両方を毎tick見ているので、降格の速さは変わらない）
- D9 `CleanRoom.SurfaceArea` は残す。
  出所: agent前提（`CleanRoomDetectionService.cs:160` が部屋の同一判定に使っている）
- D10 unityプレイ録画テスト・moores-code-review・PR 作成は第2段階（Task 8〜10）に置き、第1段階の実装者は実行しない。
  出所: ユーザー裁定（D4 と同じ原文）
- D11 `VanillaSchema/cleanRoom.yml` から `pollution`・`maxConcentration`・`pollutionConnectorBlockGuids` を削除し、`VanillaSchema/blocks.yml` の清浄機の `filterCapacity` を `filterLifeSeconds` に置き換える。optional やフォールバックで旧キーを残さず、テスト用mod と `moorestech_master` の JSON を一括更新する。再生成のために `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` を書き換えるが、コミットには含めない。
  出所: agent前提（D2・D3 の帰結。AGENTS.md「変更の波及を恐れない」）
- D12 `moorestech_server/Assets/Scripts/Core.Master/CleanRoomMaster.cs` から `Pollution`・`IsPollutionConnectorBlock` を削除し、`moorestech_server/Assets/Scripts/Core.Master/Validator/CleanRoom/CleanRoomMasterUtil.cs` から汚染・濃度の検証を削除する。
  出所: agent前提（D3 の帰結。読み手が居なくなるマスタ項目を残さない）
- D13 `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs` の毎tick更新は「清浄機の除去能力合計 ÷ 空きセル数 → 閾値行」だけにする。摩耗を部屋から清浄機へ押し込む呼び出しは消し、清浄機が自分の `Update()` で寿命を進める。
  出所: agent前提（D1・D2 の帰結。前例: 同ディレクトリの `CleanRoomItemHatchComponent` が `IUpdatableBlockComponent` で自己更新する）
- D14 `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/CleanRoom/CleanRoomSummaryMessagePack.cs` から `Concentration` を削除してキー番号を詰め、`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/CleanRoom/CleanRoomInfoMessagePackFactory.cs` の呼び出しを合わせる。新しいプロトコルやイベントは足さない（既存の「表示中だけ問い合わせる」経路の項目を1つ減らすだけ）。
  出所: agent前提（D2 の帰結。経路は [[2026-10-23-ダイ数と機械の見た目と部材と部屋の状態の取得]] の裁定どおりで変えない）
- D15 過去の裁定 [[2026-10-23-部屋の状態は機械と清浄機のUIと壁ホバーに出す]]（部屋欄に汚染濃度を出す）と [[2026-10-23-クリーンルームの抽選と建て方の細部]] (3)（ハッチの搬送量・汚染係数で大部屋への集約を有利にする）は、D2・D3 で上書きする。部屋欄から濃度が消え、ハッチの数・搬送量と壁面積はクラスに効かなくなる。
  出所: ユーザー裁定 2026-11-13（D2・D3 と同じ原文）。「大部屋への集約を有利にする」という旧裁定の狙いが汚染の分だけ失われる点は、ユーザーへ個別には確認していない（agent が最終報告で示す）
- D16 換気回数の導出（清浄機の収集 → 合計 → `AirChangeRate`）は、判定（`CleanRoomDatastore.UpdatePurity`）と表示（`CleanRoomRoomSummary.Create`）の2か所に残す。
  出所: agent前提（現行と同じ形で、どちらも同じ収集関数と同じ純関数を呼ぶため判定と表示はずれない。まとめる改修は今回の範囲に入れない）
- D17 閾値行の順序の検証を、`MaxConcentration` の昇順から `RequiredAirChangeRate` の降順へ付け替える（`moorestech_server/Assets/Scripts/Core.Master/Validator/CleanRoom/CleanRoomMasterUtil.cs`）。
  出所: agent前提（良い行から順に走査するクラス判定の前提を、マスタ検証で起動時に止めるため）
- D18 反証検査（fresh-context の opus 1体）: Critical 5件・Warning 6件をすべて plan の「反証検査で確定した追加・訂正」節へ反映した。設計判断として返ってきたのは D5（部屋の外の清浄機も消費するか）の1件で、plan 案（どこでも消費する）のまま進め、ユーザーへは報告で示している。`moorestech_server/Assets/Scripts/Core.Master/Validator/` の清浄機パラメータ検証に `filterLifeSeconds > 0` を足し、コンポーネント側の丸めはやめる。
  出所: agent前提（AGENTS.md「必須の値が無いときは既定値で埋めず、いちばん早い段階で止める」）
