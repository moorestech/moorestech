---
name: moorestech-save-migration
description: moorestech のセーブ形式を変えるとき、ロード時マイグレーションのステップ（ISaveMigrationStep）を書いて既存ワールドを保つ。旧版セーブの手変換（worldVersion 導入前・開発者手元セーブ）も扱う。Use When — 「セーブ形式を変える」「WorldSaveAllInfoにフィールドを足す」「セーブが旧形式でロードできない」「マイグレーションステップを書いて」「ロード時にNRE/JSONパースエラーが出る」と言われた場合。
---

# moorestech Save Migration

## 方針（2026-09-13 に反転した）

テスター配布が始まったため、**セーブ形式を変えるPRはロード時マイグレーションのステップを同梱する**（ADR 0058、
`.decisions/2026-09-13-テスターのセーブ互換はゲーム内ロード時マイグレーション連鎖で保つ.md`、AGENTS.md
「互換性とパフォーマンス」の例外項）。以前の「コード側に互換を書かずセーブファイルを手で変換する」方針は、
worldVersion 導入前のセーブと開発者手元のセーブに限った補助手順へ降格した。

セーブ実体は `<GameSystemPaths.SaveFileDirectory>/world_1/save.json`（mac なら
`~/Library/Application Support/moorestech/Saves/world_1/save.json`）。
マイグレーション前の原本はその隣の `backup/<元のworldVersion>/save.json`、マスタ欠損で除去した実体は
`pruned/<UTC時刻>.json` に残る（退避先は `SaveArchiveDirectory.FromWorldDataDirectory` がセーブの隣へ導出する）。

## 主手順: マイグレーションステップを書く

1. **`WorldSaveAllInfo.CurrentVersion` を1つ上げる**（`Game.SaveLoad/Json/WorldVersions/`）。
2. **`Game.SaveLoad/Migration/Steps/SaveMigrationStepV<n>ToV<n+1>.cs` を作る。**
   `references/save-migration-step-template.md` の骨格と、実装済みの `SaveMigrationStepV1ToV2.cs` を写して書き始める。
   変換は `JObject` の上だけで行い、`worldVersion` は触らない（連鎖が書く）。
3. **`MoorestechServerDIContainerGenerator` の `new SaveMigrationChain(new ISaveMigrationStep[] {...}, WorldSaveAllInfo.CurrentVersion)` へ足す。**
   `FromVersion` が `1..CurrentVersion-1` を欠番・重複なく覆っていないと、構築時（起動時）に `ArgumentException` で止まる。
4. **単体テストを同じPRに入れる**（`Tests/UnitTest/Game/SaveLoad/SaveMigrationStepV<n>ToV<n+1>Test.cs`）。
   テンプレートの「3. テスト」と既存 `SaveMigrationStepV1ToV2Test.cs` がそのまま雛形。
5. **実ロードで確かめる**（下の「実ロード検証」）。デシリアライズ単体の確認では足りない。

### 連鎖の契約（`Game.SaveLoad/Migration/`）

| 型 | 役割 |
| --- | --- |
| `ISaveMigrationStep` | `int FromVersion { get; }` と `SaveMigrationStepResult Migrate(JObject save)` の1手 |
| `SaveMigrationChain` | ctor `(IReadOnlyList<ISaveMigrationStep> steps, int currentVersion)`。構築時に欠番・重複を検証し、`Migrate` で昇順適用。未来版・版0以下・ステップの `Failed` は `SaveMigrationResult.CanLoad = false` |
| `SaveMigrationStepResult` | 1手の結果。`Converted(JObject)` か `Failed(string reason)`。`Failed` を受けた連鎖は版を刻まず `Blocked` を返す |
| `SaveArchiveWriter` | 変換が走るときだけ原本を `backup/<version>/save.json` へ退避（既存は上書きしない） |
| `MissingMasterPruner` | 連鎖の**後段**。マスタから消えたブロック・アイテム・研究を除去する（`Game.SaveLoad/Pruning/`） |
| `SaveLoadPreparer` | 上を束ねた `Prepare(string saveJsonText)`。`WorldLoaderFromJson.LoadOrInitialize` から呼ばれる |

ロード経路: `LoadOrInitialize` がファイルを読み → `SaveLoadPreparer.Prepare`（版検出→退避→変換→除去→件数記録）
→ 既存 `Load(string)`。`Load(string)` は無改変で、整った形だけを受ける契約のまま。

**マスタからの削除はマイグレーションではない。** ブロック・アイテム・研究ノードを消しただけなら
ステップは要らない。`MissingMasterPruner` が毎回のロードで除去し、件数がプレイヤーへ通知される。

**未来版のセーブは新規ワールド作成へ落とさず中断する。** 古いビルドで新しいセーブを開いた場合で、
解消条件は「ゲームを更新する」。この経路を「落ちるから直す」と読み替えてはいけない（意図した fail-closed）。

## 実ロード検証（必須）

`references/load_test.cs` を `uloop execute-dynamic-code --project-path ./moorestech_client` で実行し、
`MoorestechServerDIContainerGenerator.Create` → `IWorldSaveDataLoader.LoadOrInitialize()` の経路で
`LOAD OK | blocks=N` を確認する。失敗時の例外メッセージが次の未対応形式を教える。
重要データ（列車インベントリ等）の実値は `references/verify_loaded.cs` で確認する。

## 補助手順: セーブファイルの手変換（worldVersion 導入前・開発者手元セーブ限定）

**プロダクションコードはもうこの手順を案内しない**（`WorldLoaderFromJson` にあった
`scripts/save_migration/migrate_block_state_objects.py` への案内は、版1→版2ステップの実装に伴い撤去済み）。
配布済みビルドのセーブにこの手順を使ってはいけない（テスターの手元では実行できない）。
使ってよいのは、worldVersion より前に作られた自分のセーブを1回だけ救うときだけ。

1. `mb=$(git merge-base HEAD origin/master)` を取り、`git diff --name-only $mb..HEAD -- '***.cs'` から
   セーブ形式に触れた全ファイルを**最初に網羅列挙**する（クラッシュを1つずつ潰すのは禁止。ロードは
   最初の失敗ブロックで止まるので、直しても次が出るだけ）。
2. 対象セーブを Python で読み、`world[].state` のキー別件数と `trainUnits` を集計する。
3. 揮発 int → GUID の対応は `references/dump_id_maps.cs` を `uloop` で実行して取る。
   **グローバル `MasterHolder` を使わない**（エディタに別のテスト用マスタが載っていることがある）。
   `ServerDirectory.GetDirectory()` から独立にロードする。
4. `references/migrate_save_template.py` をベースに変換する。必ず backup → 変換 → 安全スキャン
   （全 `world[].state` 値が valid JSON か）→ 書き戻しの順。解決できない id があれば中断する。
5. 上の「実ロード検証」を行う。

## Gotchas

- **ステップの中で `MasterHolder` を引かない。** 削除済みマスタで変換自体が落ちる。マスタ欠損の始末は
  後段の `MissingMasterPruner` の責務。
- **ステップは冪等に書く。** バックアップから戻して再実行する運用がある。
- **`CurrentVersion` を上げ忘れるとステップが宙に浮く。** `SaveMigrationChain` の構築検証が
  「期待={...} 実際={...}」で落ちるので、起動した瞬間に気づける（意図した早期失敗）。
- **前の版のセーブクラスを参照しない。** `JObject` だけで書く。過去のステップが将来の形式変更で壊れる。
- **新規サーバー側 `.cs` の追加直後は Unity 再起動が要る**（`uloop launch ./moorestech_client --restart`）。
- **`uloop execute-dynamic-code` は `System.IO` 全面禁止**（`Path` も）。ファイル入出力は Python/bash 側。
- **C# の char リテラル `'/'` はシェルの single quote と衝突**する。スニペットは一時 `.cs` に書き
  `--code "$(cat file.cs)"` で渡す。
- **base64-MessagePack の状態値がある**（例 `RailComponentStateDetail = "kZP..."`）。JSON として読めない
  値は触らず素通しし、理由を `Debug.Log` に残す。
- **コンテナのスロット数 > master.InventorySlots だとロード時に切り詰められる。** 変換ファイル側では
  保持できてもロードで落ちるので、事前検出して master 側の修正で回避できるか確認する。
- **マスタ参照値（capacity・スロット数）は保存形式に含めない。** ロード時に master から解決される。

## Available scripts (references/)

- `references/save-migration-step-template.md` — ステップの骨格（本体・登録・テスト・落とし穴）。**主手順の入口**
- `references/dump_id_maps.cs` — 独立 v8 マスタから item/fluid の id→GUID 全マップを返す（補助手順）
- `references/migrate_save_template.py` — backup→変換→安全スキャン→書き戻しの雛形（補助手順）
- `references/load_test.cs` — DI フルロードで `LOAD OK | blocks=N` を確認（実ロード検証）
- `references/verify_loaded.cs` — ロード後の実値ダンプで欠損検査（実ロード検証）
