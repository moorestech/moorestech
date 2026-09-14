# プレイテスト F: セーブ互換（マイグレーション連鎖）とマスタ欠損の除去 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** テスターのワールドが、セーブ形式の変更（worldVersion 更新）とマスタデータからの削除（ブロック・アイテム・研究ノード）をまたいでロードでき、元セーブと除去データが世代付きで残り、除去件数がプレイヤーへ1回通知される状態にする（ADR 0061 のセーブ互換2裁定）。

**Architecture:** (1) `Game.SaveLoad/Migration/` に `ISaveMigrationStep`＋`SaveMigrationChain` を置き、ロード前に JSON（`JObject`）の `worldVersion` を見て V(n)→V(n+1) を順に適用する。未知の未来版・連鎖の欠番は fail-closed（ロードせず理由ログ）。マイグレーション実行時のみ元セーブを `Saves/backup/<version>/save.json` へ退避する。(2) `Game.SaveLoad/Pruning/` の `MissingMasterPruner` が、マイグレーション後の JSON からマスタに存在しないブロック・アイテムスタック・研究ノードを取り除き、取り除いた実体を `Saves/pruned/<ISO>.json` へ残し、件数を `MissingMasterPruneReportStore` に載せる。(3) この2段を `SaveLoadPreparer` が束ね、`WorldLoaderFromJson.LoadOrInitialize` が「ファイル読み → Prepare → 既存 `Load(json)`」の順で通る。既存の `Load(string)` は 400 本超のテストが直接呼ぶ入口なので形を変えない。(4) 除去件数はサーバー既存の通知基盤（`NotificationService` の `va:event:notification`）に新カテゴリ `SaveMigration` を足し、`EventProtocolProvider.OnPlayerEventStreamRegistered`（sink 登録直後の同期push契約）で接続したプレイヤーへ1回だけ送る。表示は既存の `notification.events` トピック→`NotificationHost` を素通りし、文言は `Localization/localization.csv` の日英独で持つ。(5) 規約は AGENTS.md へ追記し、`moorestech-save-migration` スキルを「手変換の手順書」から「マイグレーションステップの書き方」へ書き換える。

**Tech Stack:** Unity C#（`moorestech_server`）、Newtonsoft.Json（`JObject`/`JToken`）、MessagePack（通知イベント）、UniRx、NUnit（UnitTest / CombinedTest）、TypeScript + React + zod + zustand + vitest（`moorestech_web/webui`）、`uloop` CLI、pnpm。

## Requirements

- R1. マイグレーション連鎖の骨格: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/` に `ISaveMigrationStep`（`int FromVersion { get; }` と `JObject Migrate(JObject save)`）と `SaveMigrationChain` を新設する。`SaveMigrationChain` は構築時にステップ列を検証（`FromVersion` の重複禁止・`1..CurrentVersion-1` を欠番なく覆うこと）し、違反時は例外で起動を止める。受入: `SaveMigrationChainTest` の構築検証テスト3本（`FromVersion` 重複で例外・欠番で例外・現在版が1の今は空連鎖が通る）が通る。
- R2. 版検出と適用順: `SaveMigrationChain.ReadWorldVersion(JObject)` が `worldVersion` を読み（キー欠落は 1 とみなし `Debug.Log` で明示）、`Migrate(JObject)` が現在版まで `FromVersion` 昇順にステップを適用し、各ステップ後に `save["worldVersion"]` を `FromVersion + 1` へ更新する。受入: 疑似ステップ2本（1→2、2→3）を積んだ v1 セーブが v3 になり、適用順が記録順と一致するテストが通る。
- R3. 未知の未来版は fail-closed: セーブの `worldVersion` が `WorldSaveAllInfoV1.CurrentVersion` より大きい、または 1 未満のとき、`SaveMigrationResult.CanLoad = false` と理由文字列を返し、`WorldLoaderFromJson.LoadOrInitialize` は新規ワールド作成へ落ちずに理由を `Debug.LogError` して例外を投げる。受入: `worldVersion = 999` のセーブで `CanLoad` が false・理由に版番号が含まれる単体テストと、`LoadOrInitialize` が `WorldInitialize()` を呼ばず例外を投げる CombinedTest が通る。
- R4. 世代付きバックアップ: マイグレーションを実際に適用するときに限り、適用前の生 JSON テキストを `Saves/backup/<元のworldVersion>/save.json` へ書く。同じパスが既に在る場合は上書きせず `Debug.Log` で残す（最初の原本を守る）。受入: v1 セーブを v2 へ上げると `backup/1/save.json` が原文一致で残り、2回目の実行では原本が書き換わらないテストが通る。
- R5. マスタ欠損の除去: `moorestech_server/Assets/Scripts/Game.SaveLoad/Pruning/MissingMasterPruner` が (a) `world[]` のうち `blockGuid` が `MasterHolder.BlockMaster` に無い要素を除去、(b) セーブ全体（`world[].state` に入っている JSON 文字列の中身を含む）を再帰走査し、`itemGuid` が `Guid.Empty` でなく `MasterHolder.ItemMaster.ExistItemId` が false のスタックを空スタック（`itemGuid = Guid.Empty`・`count = 0`）へ落とし、(c) `research.CompletedResearchGuids` から `MasterHolder.ResearchMaster.GetResearch` が null を返す guid を除去する。除去は毎回のロードで走る（版が上がっていなくてもマスタは変わるため）。受入: 実DI（`TestModDirectory.ForUnitTestModDirectory`）で作ったセーブへ未知 guid を差し込み、3種すべてが除去され件数が一致する CombinedTest が通る。
- R6. 既存の欠損 mapObject スキップとの整合: `MapObjectDatastore` の既存スキップ（`.decisions/2026-08-04-セーブの欠損mapObjectはロード時スキップにする.md`）は挙動を変えない。`MissingMasterPruner` は mapObject を触らず、その理由をコメントで明記する。受入: `MapObjectDatastore.LoadMapObject` に差分が無く、pruner のコメントに理由が書かれている。
- R7. 除去データの保持: 除去した実体（除去したブロック要素の配列・空へ落としたスタックの元値の配列・除去した研究 guid の配列）を `Saves/pruned/<UTC時刻>.json` へ書く。除去が0件なら書かない。ファイル名は Windows で使える形（コロン無し。`yyyyMMdd'T'HHmmss'Z'`）にし、同秒の衝突は `-1`, `-2` の連番で回避する。受入: 除去ありのロードでファイルが1本でき、中身が3配列と `prunedAt` を持つテストが通る。
- R8. 除去件数の保持と公開: `MissingMasterPruneReport`（3つの件数と `HasRemoval`）を `Game.SaveLoad.Interface` に置き、`MissingMasterPruneReportStore`（`Game.SaveLoad/Pruning/`）が保持する。読み取り面は `IMissingMasterPruneReportLookup`、書き込みは DI 注入した store 経由のみ。受入: 除去なしロード後に `Report.HasRemoval` が false、除去ありロード後に3件数が一致する CombinedTest が通る。
- R9. ロード経路への組み込み: `SaveLoadPreparer.Prepare(string saveJsonText)` が「版検出 → fail-closed 判定 → バックアップ → マイグレーション → 除去 → 除去データ書き出し → report 格納」を行い `PreparedSaveJson` を返す。`WorldLoaderFromJson.LoadOrInitialize` はファイルを読んで `Prepare` を通してから既存 `Load(string)` を呼ぶ。`Load(string)` のシグネチャと責務は変えない。受入: 未知ブロック guid 入りセーブが `LoadOrInitialize` 相当の経路で例外を出さずロードでき、既存の SaveLoad 系テストが全て通る。
- R10. 除去件数のプレイヤー通知: `NotificationCategory` に `SaveMigration` を追加し、`NotificationMessagePack.CreateSaveMigration(int blockCount, int itemCount, int researchCount)` を生やす。`Server.Event/Notification/MissingMasterPruneNotificationWiring`（`IBootInitializable`）が `EventProtocolProvider.OnPlayerEventStreamRegistered` を購読し、`HasRemoval` のときだけ `NotifyWithoutCooldown` でそのプレイヤーへ1回送る。受入: sink 登録で通知が1件届き、除去なしでは0件のテストが通る。
- R11. クライアント側の表示: `NotificationCategoryTable` に `SaveMigration => "saveMigration"` を足し、webui の `NotificationDataSchema` のカテゴリ列挙・`notificationMessages.ts` の対応表と `resolveNotificationText` の分岐を更新する。文言キー `ui.notification.saveMigrationMissingMasterPruned` を `Localization/localization.csv` に日本語・英語・ドイツ語で追加し、`pnpm gen:i18n` で生成物を更新する。受入: webui の `pnpm test` が通り、`localizationKeys freshness` テストが緑。
- R12. 規約の追記: `AGENTS.md` の「## 互換性とパフォーマンス」に、セーブ形式変更PRはマイグレーションステップとテストを同梱する旨を追記する。受入: 追記後の文が「後方互換不要」の原則とセーブ形式だけの例外を両方読めること。
- R13. スキルの役割変更: `.agents/skills/moorestech-save-migration/SKILL.md` を「手でセーブファイルを変換する手順」から「`ISaveMigrationStep` の書き方」へ書き換え、`references/save-migration-step-template.md` に plan A の形式変更（`currentTick`・`randomState` 追加、ブロック state のオブジェクト化）を題材にした完全なステップ実装例を置く。旧手順は「worldVersion 導入前のセーブ・開発者手元セーブ専用」として残す。受入: SKILL.md の Use When とステップ手順が新方式を指し、テンプレートがそのままコピーして書き始められる粒度。
- やらないこと: 置換・返金マイグレーション（除去データを `Saves/pruned/` に残すところまでで、後日ステップを足せる形にするだけ）／レシピ・コスト改変への補償（次回から新値。ADR 0061 の裁定どおり何もしない）／plan A の形式変更そのものの実装（本planは雛形とテンプレートのみ）／`blueprints`・`hotbarAssignments`・`itemStackLevels` の欠損 guid 掃除（既存ローダーが `Guid.Empty` 落ちや `continue` で既に耐えるため。詳細は「## 配置と前例」の備考）／セーブ以外（`map.json`・terrain）のマイグレーション／マイグレーション UI・進捗表示。

## Global Constraints

### 共有契約 §8（`shared-contracts.md` の逐語転記。変更禁止・矛盾禁止）

> ## 8. セーブ互換（plan F）
> - `WorldSaveAllInfoV1.WorldVersion` を現在版とし、`Game.SaveLoad/Migration/` に `ISaveMigrationStep { int FromVersion; JObject Migrate(JObject save); }` と `SaveMigrationChain`。ロード前に `Saves/backup/<version>/save.json` へ元セーブを退避（世代付き）。
> - マスタ欠損: `Game.SaveLoad/Pruning/` に `MissingMasterPruner`（ブロック・インベントリアイテム・研究ノード）。除去データは `Saves/pruned/<ISO>.json` に保持。除去件数はサーバー→クライアントへ既存 3点セット型（イベントパケット＋初期データ＋購読）で通知し、クライアントがトーストを1回出す。
> - AGENTS.md の規約追記（形式変更PRはマイグレーションステップ＋テスト同梱）と moorestech-save-migration スキルの役割変更は plan F の最終タスク。

契約の運用解釈（本planの実装が従う形。契約の否定ではなく具体化であり、根拠は「## 判断記録（ADR）」に置く）:

- `ISaveMigrationStep` の `FromVersion` は C# の interface で表せる形（`int FromVersion { get; }`）にする。
- `Saves/backup/` `Saves/pruned/` の `Saves` は `Game.Paths.GameSystemPaths.SaveFileDirectory`（既定ワールドは `world_1` 固定・`.decisions/2026-08-18-セーブバックアップはworld_1固定でディレクトリ丸ごと.md`）。
- `<ISO>` は Windows のファイル名に使える基本形式 `yyyyMMdd'T'HHmmss'Z'`（拡張形式のコロンは Windows で不正）。
- 「既存3点セット型」は ①既存イベント（`NotificationService` の `va:event:notification`）②接続直後の同期push（`OnPlayerEventStreamRegistered`。前例 `TrainFullSnapshotEventPacket`）③既存クライアント購読（`NotificationTopic`）で満たす。新規イベントパケットも初期ハンドシェイクへのフィールド追加も作らない。判定根拠は「## 判断記録（ADR）」の該当項。
- 「トースト」は webui の常設通知面（`notification.events` → `NotificationHost`。7秒で自動消滅）。`emitToast`（`features/toast`）は bridge の生英語文字列専用で、ローカライズ経路を持たないため使わない。

### 作業ルール

- 作業ブランチ: `feature/save-migration-chain`（`origin/master` から切る）。plan D/E/G/H とは独立にマージできる。
- `../moorestech_master` は本planでは変更しない。作業ツリーのピン `.moorestech-external-revisions.json` は Unity が実チェックアウト値へ自動書き戻しするので `git add` しない。`git add` は常にファイル指定で行う（`git add -A` 禁止）。
- `.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client`。テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。「Unity is reloading (Domain Reload in progress)」が出たら45秒待って再実行。**新規サーバー側 `.cs` を足した直後は Unity 再起動（`uloop launch ./moorestech_client --restart`）が必要**（file: パッケージのため Refresh では検出されない）。
- `Localization/localization.csv` を触ったら C# 側の生成キーは force-recompile が要る（触っていないキーの CS0117 は CSV 再生成漏れのサイン）。webui 側は `pnpm -C moorestech_web/webui gen:i18n`。
- webui の依存は `pnpm -C moorestech_web/webui install`（`cp -Rc` や `npm ci` は壊れる）。テストは `pnpm -C moorestech_web/webui test`。
- コメントは「// 日本語 → // English」の2行セット、各1行、3〜10行ごと。1ファイル200行以下、1ディレクトリ10ファイル以下。partial 禁止、`Func<>` 禁止、デフォルト引数禁止、単純 getter/setter プロパティ禁止（`{ get; private set; }` は可）、try-catch は外部境界（外部入力 JSON のパース等）のみで理由をコメント明記。
- イベント・通知は UniRx `Subject<T>` ＋ `IObservable<T>`。C# `event Action` 禁止。
- fail-closed 経路（早期 return・拒否・無視）は必ず `Debug.Log*` で理由を出す。無音の縮退は禁止。
- `#region Internal` はメソッド内ローカル関数の集約用途のみ。クラス直下の private メソッド群を囲わない。
- 経過時間は `Core.Update.GameUpdater` のティック。`DateTime` を使ってよいのは「実世界の日時そのものの記録」= 本planでは `Saves/pruned/` のファイル名と `prunedAt` だけ。
- 永続化は Newtonsoft JSON、キーは GUID（揮発 int 禁止）、マスタ由来値は保存しない。
- 命名: 型名・ファイル名は本plan記載のとおり（`ISaveMigrationStep`・`SaveMigrationChain`・`SaveMigrationResult`・`SaveArchiveDirectory`・`SaveArchiveWriter`・`SaveLoadPreparer`・`PreparedSaveJson`・`MissingMasterPruner`・`MissingMasterPruneOutcome`・`MissingMasterPruneReport`・`MissingMasterPruneReportStore`・`IMissingMasterPruneReportLookup`・`MissingMasterPruneNotificationWiring`）。
- 各タスク末尾でコミット。コミットメッセージ末尾に以下を付ける:
  ```
  Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01HtaDWGdhRRiNho39vPY5Li
  ```

---

### Task 1: 版定数・保管先パス・マイグレーション連鎖

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs:22`（`WorldVersion` の初期値を新設の定数に変える）
- Create: `moorestech_server/Assets/Scripts/Game.Paths/SaveArchiveDirectory.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/ISaveMigrationStep.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/SaveMigrationResult.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/SaveMigrationChain.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/SaveArchiveWriter.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationChainTest.cs`

**Interfaces:**
- Consumes: 既存 `Game.Paths.GameSystemPaths.SaveFileDirectory`（`string`）。
- Produces:
  - `public const int Game.SaveLoad.Json.WorldVersions.WorldSaveAllInfoV1.CurrentVersion = 1;`
  - `public class Game.Paths.SaveArchiveDirectory { public string BackupRoot { get; } public string PrunedRoot { get; } public string BackupSaveJsonPath(int worldVersion); public string PrunedJsonPath(DateTime utcNow, int collisionIndex); public static SaveArchiveDirectory FromSaveFileDirectory(string saveFileDirectory); public static SaveArchiveDirectory Default(); }`
  - `public interface Game.SaveLoad.Migration.ISaveMigrationStep { int FromVersion { get; } JObject Migrate(JObject save); }`
  - `public sealed class Game.SaveLoad.Migration.SaveMigrationResult { public bool CanLoad { get; } public string BlockedReason { get; } public int FromVersion { get; } public int ToVersion { get; } public bool Migrated { get; } public JObject Save { get; } public static SaveMigrationResult Blocked(int fromVersion, string reason); public static SaveMigrationResult Completed(int fromVersion, int toVersion, bool migrated, JObject save); }`
  - `public sealed class Game.SaveLoad.Migration.SaveMigrationChain { public SaveMigrationChain(IReadOnlyList<ISaveMigrationStep> steps); public static int ReadWorldVersion(JObject save); public SaveMigrationResult Migrate(JObject save); }`
  - `public sealed class Game.SaveLoad.Migration.SaveArchiveWriter { public SaveArchiveWriter(SaveArchiveDirectory directory); public void WriteBackup(int worldVersion, string saveJsonText); public void WritePruned(JObject prunedJson, DateTime utcNow); }`

- [ ] **Step 1: `Game.SaveLoad.asmdef` に `Core.Master` 参照を足す**

`moorestech_server/Assets/Scripts/Game.SaveLoad/Game.SaveLoad.asmdef` の `references` 配列、`"Core.Item",` の直後に1行足す（Task 2 の pruner が `MasterHolder` を読むため。ここで足しておくと Unity 再起動が1回で済む）:

```json
    "Core.Item",
    "Core.Master",
```

- [ ] **Step 2: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationChainTest.cs` を新規作成する:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Game.Paths;
using Game.SaveLoad.Json.WorldVersions;
using Game.SaveLoad.Migration;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class SaveMigrationChainTest
    {
        // 記録順ではなくFromVersion昇順で適用されることを見るため、登録順をわざと逆にする
        // Register out of order so the test proves ordering comes from FromVersion, not registration
        [Test]
        public void 版を跨いだ連鎖が昇順に適用されるTest()
        {
            var applied = new List<int>();
            var chain = new SaveMigrationChain(new ISaveMigrationStep[]
            {
                new RecordingStep(2, applied),
                new RecordingStep(1, applied),
            });

            var result = chain.Migrate(JObject.Parse("{\"worldVersion\":1}"));

            Assert.IsTrue(result.CanLoad, result.BlockedReason);
            Assert.IsTrue(result.Migrated);
            Assert.AreEqual(new[] { 1, 2 }, applied.ToArray());
            Assert.AreEqual(3, result.ToVersion);
            Assert.AreEqual(3, result.Save["worldVersion"].Value<int>());
            Assert.AreEqual("1:2:", result.Save["trace"].Value<string>());
        }

        // worldVersionが無いセーブは最古の版とみなす。将来版と取り違えると原本を壊すので固定する
        // A save without worldVersion is treated as the oldest version; mistaking it for a future one would destroy the original
        [Test]
        public void 版キーが無いセーブは版1として扱うTest()
        {
            Assert.AreEqual(1, SaveMigrationChain.ReadWorldVersion(JObject.Parse("{}")));
        }

        // 現在版ちょうどのセーブはステップ0本でそのまま通る（今日の唯一の実構成）
        // A save already at the current version passes through with zero steps (today's only real configuration)
        [Test]
        public void 現在版のセーブはステップ無しでそのまま通るTest()
        {
            var chain = new SaveMigrationChain(Array.Empty<ISaveMigrationStep>());
            var save = JObject.Parse($"{{\"worldVersion\":{WorldSaveAllInfoV1.CurrentVersion}}}");

            var result = chain.Migrate(save);

            Assert.IsTrue(result.CanLoad, result.BlockedReason);
            Assert.IsFalse(result.Migrated);
            Assert.AreEqual(WorldSaveAllInfoV1.CurrentVersion, result.ToVersion);
        }

        [Test]
        public void 未来版のセーブはロードを拒否し理由に版番号を含むTest()
        {
            var chain = new SaveMigrationChain(Array.Empty<ISaveMigrationStep>());

            var result = chain.Migrate(JObject.Parse("{\"worldVersion\":999}"));

            Assert.IsFalse(result.CanLoad);
            StringAssert.Contains("999", result.BlockedReason);
        }

        [Test]
        public void 版0以下のセーブはロードを拒否するTest()
        {
            var chain = new SaveMigrationChain(Array.Empty<ISaveMigrationStep>());

            Assert.IsFalse(chain.Migrate(JObject.Parse("{\"worldVersion\":0}")).CanLoad);
        }

        [Test]
        public void FromVersionが重複した連鎖は構築時に落ちるTest()
        {
            var applied = new List<int>();
            Assert.Throws<ArgumentException>(() => new SaveMigrationChain(new ISaveMigrationStep[]
            {
                new RecordingStep(1, applied),
                new RecordingStep(1, applied),
            }));
        }

        // 欠番は「その版のセーブが永久にロードできない」という恒久封鎖なので構築時に落とす
        // A gap permanently blocks that version's saves, so it fails at construction instead of at load time
        [Test]
        public void FromVersionに欠番がある連鎖は構築時に落ちるTest()
        {
            var applied = new List<int>();
            Assert.Throws<ArgumentException>(() => new SaveMigrationChain(new ISaveMigrationStep[]
            {
                new RecordingStep(1, applied),
                new RecordingStep(3, applied),
            }));
        }

        [Test]
        public void バックアップは既存の原本を上書きしないTest()
        {
            var root = Path.Combine(Path.GetTempPath(), "moorestech-save-archive-" + Guid.NewGuid().ToString("N"));
            var writer = new SaveArchiveWriter(SaveArchiveDirectory.FromSaveFileDirectory(root));

            writer.WriteBackup(1, "{\"first\":true}");
            writer.WriteBackup(1, "{\"second\":true}");

            var path = SaveArchiveDirectory.FromSaveFileDirectory(root).BackupSaveJsonPath(1);
            Assert.AreEqual("{\"first\":true}", File.ReadAllText(path));
            Directory.Delete(root, true);
        }

        // 同秒に2回除去が起きても片方が消えないことを見る
        // Two prunes in the same second must not overwrite each other
        [Test]
        public void 除去データは同秒でも連番で別ファイルになるTest()
        {
            var root = Path.Combine(Path.GetTempPath(), "moorestech-save-archive-" + Guid.NewGuid().ToString("N"));
            var directory = SaveArchiveDirectory.FromSaveFileDirectory(root);
            var writer = new SaveArchiveWriter(directory);
            var at = new DateTime(2026, 9, 13, 8, 30, 0, DateTimeKind.Utc);

            writer.WritePruned(JObject.Parse("{\"a\":1}"), at);
            writer.WritePruned(JObject.Parse("{\"a\":2}"), at);

            Assert.AreEqual(2, Directory.GetFiles(directory.PrunedRoot, "*.json").Length);
            Assert.IsTrue(File.Exists(directory.PrunedJsonPath(at, 0)));
            Assert.IsTrue(File.Exists(directory.PrunedJsonPath(at, 1)));
            Directory.Delete(root, true);
        }

        // テスト専用の疑似ステップ。適用順の記録とJSONへの痕跡付けだけを行う
        // A test-only fake step that records the order and leaves a trace in the JSON
        private sealed class RecordingStep : ISaveMigrationStep
        {
            private readonly List<int> _applied;

            public RecordingStep(int fromVersion, List<int> applied)
            {
                FromVersion = fromVersion;
                _applied = applied;
            }

            public int FromVersion { get; }

            public JObject Migrate(JObject save)
            {
                _applied.Add(FromVersion);
                save["trace"] = save["trace"] == null ? $"{FromVersion}:" : save["trace"].Value<string>() + $"{FromVersion}:";
                return save;
            }
        }
    }
}
```

- [ ] **Step 3: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: FAIL。`SaveMigrationChain`・`ISaveMigrationStep`・`SaveArchiveDirectory`・`SaveArchiveWriter`・`WorldSaveAllInfoV1.CurrentVersion` が存在しないコンパイルエラー。

- [ ] **Step 4: `WorldSaveAllInfoV1` に現在版の定数を置く**

`moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs` の `[JsonProperty("worldVersion")] public int WorldVersion = 1;`（22行目）を次の2行に差し替える:

```csharp
        // セーブ形式の現在版。マイグレーション連鎖の終点であり、形式を変えるPRがここを上げる
        // The current save format version; the migration chain's destination, raised by any PR that changes the format
        public const int CurrentVersion = 1;

        [JsonProperty("worldVersion")] public int WorldVersion = CurrentVersion;
```

- [ ] **Step 5: `SaveArchiveDirectory` を実装する**

`moorestech_server/Assets/Scripts/Game.Paths/SaveArchiveDirectory.cs`:

```csharp
using System;
using System.IO;

namespace Game.Paths
{
    /// <summary>セーブと並べて置く保管領域（マイグレーション前の原本・除去データ）のパスを一元定義する</summary>
    /// <summary>Owns the paths of the archives kept next to the save: pre-migration originals and pruned data</summary>
    public class SaveArchiveDirectory
    {
        // ディレクトリはここでは作らない。書き込み側(SaveArchiveWriter)が要るときだけ作る
        // No directory is created here; the writer creates one only when it actually writes
        public string BackupRoot { get; }
        public string PrunedRoot { get; }

        private SaveArchiveDirectory(string backupRoot, string prunedRoot)
        {
            BackupRoot = backupRoot;
            PrunedRoot = prunedRoot;
        }

        // 版ごとに1本だけ原本を残す。後から置換・返金のマイグレーションを足すとき遡れる単位
        // Keeps one original per version, the unit a later replace/refund migration can go back to
        public string BackupSaveJsonPath(int worldVersion)
        {
            return Path.Combine(BackupRoot, worldVersion.ToString(), "save.json");
        }

        // コロンを含む拡張ISO形式はWindowsのファイル名に使えないため基本形式で綴る
        // The extended ISO form contains colons, which Windows filenames reject, so the basic form is used
        public string PrunedJsonPath(DateTime utcNow, int collisionIndex)
        {
            var stamp = utcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
            var name = collisionIndex == 0 ? $"{stamp}.json" : $"{stamp}-{collisionIndex}.json";
            return Path.Combine(PrunedRoot, name);
        }

        public static SaveArchiveDirectory FromSaveFileDirectory(string saveFileDirectory)
        {
            return new SaveArchiveDirectory(
                Path.Combine(saveFileDirectory, "backup"),
                Path.Combine(saveFileDirectory, "pruned"));
        }

        public static SaveArchiveDirectory Default()
        {
            return FromSaveFileDirectory(GameSystemPaths.SaveFileDirectory);
        }
    }
}
```

- [ ] **Step 6: `ISaveMigrationStep` と `SaveMigrationResult` を実装する**

`moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/ISaveMigrationStep.cs`:

```csharp
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration
{
    /// <summary>セーブ形式を1つ上の版へ変換する1手。worldVersionの更新は連鎖側が行う</summary>
    /// <summary>One hop that converts the save to the next version; the chain updates worldVersion itself</summary>
    public interface ISaveMigrationStep
    {
        int FromVersion { get; }

        JObject Migrate(JObject save);
    }
}
```

`moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/SaveMigrationResult.cs`:

```csharp
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration
{
    /// <summary>連鎖の結果。ロード可否と理由を同じ値で運び、呼び出し側にbool判定を残さない</summary>
    /// <summary>The chain's outcome, carrying loadability and its reason together so callers keep no bool logic</summary>
    public sealed class SaveMigrationResult
    {
        public bool CanLoad { get; }
        public string BlockedReason { get; }
        public int FromVersion { get; }
        public int ToVersion { get; }
        public bool Migrated { get; }
        public JObject Save { get; }

        private SaveMigrationResult(bool canLoad, string blockedReason, int fromVersion, int toVersion, bool migrated, JObject save)
        {
            CanLoad = canLoad;
            BlockedReason = blockedReason;
            FromVersion = fromVersion;
            ToVersion = toVersion;
            Migrated = migrated;
            Save = save;
        }

        public static SaveMigrationResult Blocked(int fromVersion, string reason)
        {
            return new SaveMigrationResult(false, reason, fromVersion, fromVersion, false, null);
        }

        public static SaveMigrationResult Completed(int fromVersion, int toVersion, bool migrated, JObject save)
        {
            return new SaveMigrationResult(true, null, fromVersion, toVersion, migrated, save);
        }
    }
}
```

- [ ] **Step 7: `SaveMigrationChain` を実装する**

`moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/SaveMigrationChain.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Game.SaveLoad.Json.WorldVersions;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration
{
    /// <summary>セーブJSONを現在版まで順に変換する。欠番・重複は構築時に落として恒久封鎖を作らない</summary>
    /// <summary>Converts a save JSON up to the current version; duplicates and gaps fail at construction so no version is permanently unloadable</summary>
    public sealed class SaveMigrationChain
    {
        private const string WorldVersionKey = "worldVersion";

        private readonly List<ISaveMigrationStep> _steps;

        public SaveMigrationChain(IReadOnlyList<ISaveMigrationStep> steps)
        {
            _steps = steps.OrderBy(step => step.FromVersion).ToList();

            // 1..CurrentVersion-1 を欠番なく1本ずつ覆っていることを起動時に確かめる
            // Verify at boot that 1..CurrentVersion-1 is covered exactly once with no gaps
            var expected = Enumerable.Range(1, WorldSaveAllInfoV1.CurrentVersion - 1).ToArray();
            var actual = _steps.Select(step => step.FromVersion).ToArray();
            if (!expected.SequenceEqual(actual))
            {
                var expectedText = string.Join(",", expected);
                var actualText = string.Join(",", actual);
                throw new ArgumentException(
                    $"マイグレーションステップのFromVersionが不正です。期待={{{expectedText}}} 実際={{{actualText}}}（現在版={WorldSaveAllInfoV1.CurrentVersion}）");
            }
        }

        // worldVersionが無いセーブは版1。将来版と取り違えて拒否すると原本を触れなくなる
        // A save without worldVersion is version 1; mistaking it for a future one would lock the original away
        public static int ReadWorldVersion(JObject save)
        {
            var token = save[WorldVersionKey];
            if (token == null)
            {
                Debug.Log($"セーブに{WorldVersionKey}がないため版1として扱います。");
                return 1;
            }

            return token.Value<int>();
        }

        public SaveMigrationResult Migrate(JObject save)
        {
            var fromVersion = ReadWorldVersion(save);

            if (fromVersion > WorldSaveAllInfoV1.CurrentVersion)
                return SaveMigrationResult.Blocked(fromVersion,
                    $"セーブの版{fromVersion}はこのビルドが知る現在版{WorldSaveAllInfoV1.CurrentVersion}より新しいため、ロードせずに中断します。ゲームを更新してください。");

            if (fromVersion < 1)
                return SaveMigrationResult.Blocked(fromVersion,
                    $"セーブの版{fromVersion}は不正です（1以上である必要があります）。ロードせずに中断します。");

            if (fromVersion == WorldSaveAllInfoV1.CurrentVersion)
                return SaveMigrationResult.Completed(fromVersion, fromVersion, false, save);

            // 版に対応するステップだけを昇順に適用し、1手ごとにworldVersionを進める
            // Apply only the steps at or above the save's version in order, advancing worldVersion after each hop
            var migrated = save;
            foreach (var step in _steps.Where(step => step.FromVersion >= fromVersion))
            {
                migrated = step.Migrate(migrated);
                migrated[WorldVersionKey] = step.FromVersion + 1;
                Debug.Log($"セーブをV{step.FromVersion}からV{step.FromVersion + 1}へ変換しました。");
            }

            return SaveMigrationResult.Completed(fromVersion, WorldSaveAllInfoV1.CurrentVersion, true, migrated);
        }
    }
}
```

- [ ] **Step 8: `SaveArchiveWriter` を実装する**

`moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/SaveArchiveWriter.cs`:

```csharp
using System;
using System.IO;
using Game.Paths;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration
{
    /// <summary>マイグレーション前の原本と除去データを書き出す。既存の原本は決して壊さない</summary>
    /// <summary>Writes pre-migration originals and pruned data, never destroying an original that already exists</summary>
    public sealed class SaveArchiveWriter
    {
        // 同秒に何本まで別名を試すか。これを超えるほど短時間に何度もロードすることはない
        // How many same-second names to try; loads never repeat this often within one second
        private const int MaxCollisionRetry = 100;

        private readonly SaveArchiveDirectory _directory;

        public SaveArchiveWriter(SaveArchiveDirectory directory)
        {
            _directory = directory;
        }

        public void WriteBackup(int worldVersion, string saveJsonText)
        {
            var path = _directory.BackupSaveJsonPath(worldVersion);
            if (File.Exists(path))
            {
                // 2度目以降は最初の原本を残す。上書きすると遡り適用の起点が失われる
                // Keep the first original on later runs; overwriting would lose the anchor for retroactive migrations
                Debug.Log($"版{worldVersion}のバックアップが既にあるため上書きしません。 path={path}");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, saveJsonText);
            Debug.Log($"マイグレーション前のセーブを退避しました。 path={path}");
        }

        public void WritePruned(JObject prunedJson, DateTime utcNow)
        {
            Directory.CreateDirectory(_directory.PrunedRoot);

            for (var collisionIndex = 0; collisionIndex < MaxCollisionRetry; collisionIndex++)
            {
                var path = _directory.PrunedJsonPath(utcNow, collisionIndex);
                if (File.Exists(path)) continue;

                File.WriteAllText(path, prunedJson.ToString());
                Debug.Log($"マスタ欠損で除去したデータを保存しました。 path={path}");
                return;
            }

            // 書けないまま黙って捨てると「保持する」裁定が無音で破れるので理由を残す
            // Silently dropping it would break the "keep the removed data" ruling without a trace
            Debug.LogError($"除去データの保存先が{MaxCollisionRetry}件すべて埋まっていたため保存できませんでした。 dir={_directory.PrunedRoot}");
        }
    }
}
```

- [ ] **Step 9: Unity を再起動してコンパイルする**

Run: `uloop launch ./moorestech_client --restart` の後、45秒待ってから `uloop compile --project-path ./moorestech_client`
Expected: `ErrorCount 0`。

- [ ] **Step 10: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.UnitTest\.Game\.SaveLoad\.SaveMigrationChainTest$"`
Expected: 9本すべて PASS。

- [ ] **Step 11: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.SaveLoad/Game.SaveLoad.asmdef \
        moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs \
        moorestech_server/Assets/Scripts/Game.Paths/SaveArchiveDirectory.cs \
        moorestech_server/Assets/Scripts/Game.Paths/SaveArchiveDirectory.cs.meta \
        moorestech_server/Assets/Scripts/Game.SaveLoad/Migration \
        moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationChainTest.cs \
        moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationChainTest.cs.meta
git commit -m "$(cat <<'EOF'
feat(server): セーブのマイグレーション連鎖と世代付き保管先を追加

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HtaDWGdhRRiNho39vPY5Li
EOF
)"
```

---

### Task 2: マスタ欠損の除去（`MissingMasterPruner`）と件数レポート

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad.Interface/MissingMasterPruneReport.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad.Interface/IMissingMasterPruneReportLookup.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Pruning/MissingMasterPruneOutcome.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Pruning/MissingMasterPruner.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Pruning/MissingMasterPruneReportStore.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/MissingMasterPruneTest.cs`

**Interfaces:**
- Consumes: Task 1 の `Game.SaveLoad.asmdef` への `Core.Master` 参照。`Core.Master.MasterHolder.BlockMaster.GetBlockIdOrNull(Guid)`（不明なら null）、`MasterHolder.ItemMaster.ExistItemId(Guid)`（bool）、`MasterHolder.ResearchMaster.GetResearch(Guid)`（不明なら null）。
- Produces:
  - `public sealed class Game.SaveLoad.Interface.MissingMasterPruneReport { public int RemovedBlockCount { get; } public int EmptiedItemStackCount { get; } public int RemovedResearchCount { get; } public bool HasRemoval { get; } public MissingMasterPruneReport(int removedBlockCount, int emptiedItemStackCount, int removedResearchCount); public static MissingMasterPruneReport None { get; } }`
  - `public interface Game.SaveLoad.Interface.IMissingMasterPruneReportLookup { MissingMasterPruneReport Report { get; } }`
  - `public sealed class Game.SaveLoad.Pruning.MissingMasterPruneOutcome { public JObject Save { get; } public MissingMasterPruneReport Report { get; } public JObject ToPrunedJson(DateTime utcNow); }`
  - `public sealed class Game.SaveLoad.Pruning.MissingMasterPruner { public MissingMasterPruneOutcome Prune(JObject save); }`
  - `public sealed class Game.SaveLoad.Pruning.MissingMasterPruneReportStore : IMissingMasterPruneReportLookup { public void SetReport(MissingMasterPruneReport report); }`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/MissingMasterPruneTest.cs` を新規作成する:

```csharp
using System;
using System.Linq;
using Game.SaveLoad.Json;
using Game.SaveLoad.Pruning;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class MissingMasterPruneTest
    {
        // マスタに絶対に無いguid。テストmodのguidと衝突しない固定値を使う
        // A guid guaranteed absent from any master; a fixed value that cannot collide with the test mod
        private const string MissingGuid = "ffffffff-ffff-ffff-ffff-ffffffffffff";

        [Test]
        public void マスタに無いブロックはセーブから除去されるTest()
        {
            var save = BuildSaveJson();
            ((JArray)save["world"]).Add(JObject.Parse($"{{\"blockGuid\":\"{MissingGuid}\",\"direction\":0,\"instanceId\":987654,\"state\":{{}},\"X\":50,\"Y\":0,\"Z\":50}}"));
            var worldCountBefore = ((JArray)save["world"]).Count;

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.AreEqual(1, outcome.Report.RemovedBlockCount);
            Assert.AreEqual(worldCountBefore - 1, ((JArray)outcome.Save["world"]).Count);
            Assert.IsFalse(outcome.Save.ToString().Contains(MissingGuid));
        }

        [Test]
        public void マスタに無いアイテムは空スタックへ落とされるTest()
        {
            var save = BuildSaveJson();
            var mainItems = (JArray)save["playerInventory"][0]["MainInventoryItems"];
            mainItems[0]["itemGuid"] = MissingGuid;
            mainItems[0]["count"] = 5;

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.AreEqual(1, outcome.Report.EmptiedItemStackCount);
            var pruned = (JArray)outcome.Save["playerInventory"][0]["MainInventoryItems"];
            Assert.AreEqual(Guid.Empty.ToString(), pruned[0]["itemGuid"].Value<string>());
            Assert.AreEqual(0, pruned[0]["count"].Value<int>());
        }

        // チェスト等の中身はworld[].stateのJSON文字列の中にある。ここを見ないとロードで例外が出る
        // Chest contents live inside the JSON string in world[].state; skipping it would still throw at load
        [Test]
        public void ブロック内部stateのアイテムも空スタックへ落とされるTest()
        {
            var save = BuildSaveJson();
            ((JArray)save["world"]).Add(JObject.Parse(
                $"{{\"blockGuid\":\"{FirstBlockGuid(save)}\",\"direction\":0,\"instanceId\":987655,\"state\":{{\"inventory\":\"{{\\\"items\\\":[{{\\\"itemGuid\\\":\\\"{MissingGuid}\\\",\\\"count\\\":3}}]}}\"}},\"X\":60,\"Y\":0,\"Z\":60}}"));

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.AreEqual(1, outcome.Report.EmptiedItemStackCount);
            var stateText = outcome.Save["world"].Last["state"]["inventory"].Value<string>();
            StringAssert.Contains(Guid.Empty.ToString(), stateText);
            Assert.IsFalse(stateText.Contains(MissingGuid));
        }

        // base64-MessagePackの状態値（例 RailComponentStateDetail）は素通しする。壊すと復元できない
        // Base64 MessagePack state values (e.g. RailComponentStateDetail) pass through untouched; corrupting them is unrecoverable
        [Test]
        public void JSONで無い状態値は素通しされるTest()
        {
            var save = BuildSaveJson();
            ((JArray)save["world"]).Add(JObject.Parse(
                $"{{\"blockGuid\":\"{FirstBlockGuid(save)}\",\"direction\":0,\"instanceId\":987657,\"state\":{{\"rail\":\"kZPAAAA=\"}},\"X\":61,\"Y\":0,\"Z\":61}}"));

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.AreEqual(0, outcome.Report.EmptiedItemStackCount);
            Assert.AreEqual("kZPAAAA=", outcome.Save["world"].Last["state"]["rail"].Value<string>());
        }

        [Test]
        public void マスタに無い研究ノードは完了一覧から除去されるTest()
        {
            var save = BuildSaveJson();
            save["research"] = JObject.Parse($"{{\"CompletedResearchGuids\":[\"{MissingGuid}\"]}}");

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.AreEqual(1, outcome.Report.RemovedResearchCount);
            Assert.AreEqual(0, ((JArray)outcome.Save["research"]["CompletedResearchGuids"]).Count);
        }

        // 除去0件のセーブでレポートが立たないこと（毎回ロードで走るので最小構成が本番の常態）
        // A save with nothing to prune must not raise the report; this is the normal case on every load
        [Test]
        public void 除去が無いセーブではレポートが立たないTest()
        {
            var outcome = new MissingMasterPruner().Prune(BuildSaveJson());

            Assert.IsFalse(outcome.Report.HasRemoval);
            Assert.AreEqual(0, outcome.Report.RemovedBlockCount);
            Assert.AreEqual(0, outcome.Report.EmptiedItemStackCount);
            Assert.AreEqual(0, outcome.Report.RemovedResearchCount);
        }

        [Test]
        public void 除去データのJSONは3つの配列と時刻を持つTest()
        {
            var save = BuildSaveJson();
            ((JArray)save["world"]).Add(JObject.Parse($"{{\"blockGuid\":\"{MissingGuid}\",\"direction\":0,\"instanceId\":987656,\"state\":{{}},\"X\":70,\"Y\":0,\"Z\":70}}"));
            var outcome = new MissingMasterPruner().Prune(save);

            var pruned = outcome.ToPrunedJson(new DateTime(2026, 9, 13, 8, 30, 0, DateTimeKind.Utc));

            Assert.AreEqual("2026-09-13T08:30:00Z", pruned["prunedAt"].Value<string>());
            Assert.AreEqual(1, ((JArray)pruned["blocks"]).Count);
            Assert.AreEqual(0, ((JArray)pruned["items"]).Count);
            Assert.AreEqual(0, ((JArray)pruned["research"]).Count);
        }

        // 実DIで作った本物のセーブを土台にする。手書きJSONだと形の食い違いに気づけない
        // Build on a real save from the DI container; a hand-written JSON would hide shape drift
        private static JObject BuildSaveJson()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return JObject.Parse(serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson());
        }

        private static string FirstBlockGuid(JObject save)
        {
            var world = (JArray)save["world"];
            return world.Count > 0 ? world[0]["blockGuid"].Value<string>() : MissingGuid;
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: FAIL。`MissingMasterPruner` が存在しないコンパイルエラー。

- [ ] **Step 3: `MissingMasterPruneReport` と Lookup を実装する**

`moorestech_server/Assets/Scripts/Game.SaveLoad.Interface/MissingMasterPruneReport.cs`:

```csharp
namespace Game.SaveLoad.Interface
{
    /// <summary>ロード時にマスタ欠損で取り除いた件数。プレイヤーへの1回きりの知らせに使う</summary>
    /// <summary>How much was removed for missing master data at load; drives the one-time player notice</summary>
    public sealed class MissingMasterPruneReport
    {
        public int RemovedBlockCount { get; }
        public int EmptiedItemStackCount { get; }
        public int RemovedResearchCount { get; }

        public MissingMasterPruneReport(int removedBlockCount, int emptiedItemStackCount, int removedResearchCount)
        {
            RemovedBlockCount = removedBlockCount;
            EmptiedItemStackCount = emptiedItemStackCount;
            RemovedResearchCount = removedResearchCount;
        }

        public bool HasRemoval => RemovedBlockCount > 0 || EmptiedItemStackCount > 0 || RemovedResearchCount > 0;

        public static MissingMasterPruneReport None { get; } = new(0, 0, 0);
    }
}
```

`moorestech_server/Assets/Scripts/Game.SaveLoad.Interface/IMissingMasterPruneReportLookup.cs`:

```csharp
namespace Game.SaveLoad.Interface
{
    /// <summary>除去件数の読み取り面。書き込みはDI注入したstoreだけが持つ</summary>
    /// <summary>Read face for the prune counts; only the DI-injected store can write them</summary>
    public interface IMissingMasterPruneReportLookup
    {
        MissingMasterPruneReport Report { get; }
    }
}
```

- [ ] **Step 4: `MissingMasterPruneReportStore` を実装する**

`moorestech_server/Assets/Scripts/Game.SaveLoad/Pruning/MissingMasterPruneReportStore.cs`:

```csharp
using Game.SaveLoad.Interface;

namespace Game.SaveLoad.Pruning
{
    /// <summary>ロード時に1度だけ書かれる除去件数の置き場。ロード前は除去なしとして読める</summary>
    /// <summary>Holds the prune counts written once at load; before the load it reads as "nothing removed"</summary>
    public sealed class MissingMasterPruneReportStore : IMissingMasterPruneReportLookup
    {
        public MissingMasterPruneReport Report { get; private set; } = MissingMasterPruneReport.None;

        public void SetReport(MissingMasterPruneReport report)
        {
            Report = report;
        }
    }
}
```

- [ ] **Step 5: `MissingMasterPruneOutcome` を実装する**

`moorestech_server/Assets/Scripts/Game.SaveLoad/Pruning/MissingMasterPruneOutcome.cs`:

```csharp
using System;
using Game.SaveLoad.Interface;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Pruning
{
    /// <summary>除去後のセーブと、取り除いた実体。実体は後から置換・返金を足すときの入力になる</summary>
    /// <summary>The pruned save plus the removed entities, which a later replace/refund migration consumes</summary>
    public sealed class MissingMasterPruneOutcome
    {
        private readonly JArray _removedBlocks;
        private readonly JArray _removedItemStacks;
        private readonly JArray _removedResearchGuids;

        public JObject Save { get; }
        public MissingMasterPruneReport Report { get; }

        public MissingMasterPruneOutcome(JObject save, JArray removedBlocks, JArray removedItemStacks, JArray removedResearchGuids)
        {
            Save = save;
            _removedBlocks = removedBlocks;
            _removedItemStacks = removedItemStacks;
            _removedResearchGuids = removedResearchGuids;
            Report = new MissingMasterPruneReport(removedBlocks.Count, removedItemStacks.Count, removedResearchGuids.Count);
        }

        // 実世界の日時そのものを記録する用途なのでDateTimeでよい（AGENTS.mdの例外）
        // Recording a real-world timestamp is the sanctioned DateTime use (AGENTS.md exception)
        public JObject ToPrunedJson(DateTime utcNow)
        {
            return new JObject
            {
                ["prunedAt"] = utcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                ["blocks"] = _removedBlocks,
                ["items"] = _removedItemStacks,
                ["research"] = _removedResearchGuids,
            };
        }
    }
}
```

- [ ] **Step 6: `MissingMasterPruner` を実装する**

`moorestech_server/Assets/Scripts/Game.SaveLoad/Pruning/MissingMasterPruner.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Pruning
{
    /// <summary>
    /// マスタから消えたブロック・アイテム・研究ノードをロード前のセーブJSONから取り除く
    /// Removes blocks, items and research nodes that vanished from the master out of the save JSON before load
    /// mapObjectは対象外。マップ側に無いinstanceIdをMapObjectDatastore.LoadMapObjectが既にスキップする
    /// Map objects are out of scope; MapObjectDatastore.LoadMapObject already skips instance ids absent from the map
    /// </summary>
    public sealed class MissingMasterPruner
    {
        private const string ItemGuidKey = "itemGuid";
        private const string CountKey = "count";

        public MissingMasterPruneOutcome Prune(JObject save)
        {
            var removedBlocks = PruneBlocks();
            var removedItemStacks = PruneItemStacks();
            var removedResearchGuids = PruneResearch();

            return new MissingMasterPruneOutcome(save, removedBlocks, removedItemStacks, removedResearchGuids);

            #region Internal

            JArray PruneBlocks()
            {
                var removed = new JArray();
                if (save["world"] is not JArray world) return removed;

                foreach (var block in world.OfType<JObject>().ToList())
                {
                    var guidText = block["blockGuid"]?.Value<string>();
                    if (!Guid.TryParse(guidText, out var guid)) continue;
                    if (MasterHolder.BlockMaster.GetBlockIdOrNull(guid) != null) continue;

                    Debug.LogWarning($"マスタに存在しないブロックをセーブから除去します。 blockGuid={guidText} instanceId={block["instanceId"]}");
                    removed.Add(block.DeepClone());
                    block.Remove();
                }

                return removed;
            }

            JArray PruneItemStacks()
            {
                var removed = new JArray();
                // ブロック除去後の木を丸ごと歩く。プレイヤー・チェスト・機械のどこにスタックがあっても拾う
                // Walk the whole tree after block removal so stacks are caught wherever they sit
                WalkForItemStacks(save, removed);
                return removed;
            }

            JArray PruneResearch()
            {
                var removed = new JArray();
                if (save["research"]?["CompletedResearchGuids"] is not JArray completed) return removed;

                foreach (var entry in completed.ToList())
                {
                    var guidText = entry.Value<string>();
                    if (!Guid.TryParse(guidText, out var guid)) continue;
                    if (MasterHolder.ResearchMaster.GetResearch(guid) != null) continue;

                    Debug.LogWarning($"マスタに存在しない研究ノードを完了一覧から除去します。 researchGuid={guidText}");
                    removed.Add(guidText);
                    entry.Remove();
                }

                return removed;
            }

            #endregion
        }

        // itemGuidを持つJObjectを空スタックへ落とし、文字列に埋め込まれたJSONの中まで降りる
        // Empties every JObject carrying an itemGuid, descending into JSON embedded inside strings
        private void WalkForItemStacks(JToken token, JArray removed)
        {
            if (token is JArray array)
            {
                foreach (var child in array.ToList()) WalkForItemStacks(child, removed);
                return;
            }

            if (token is JValue value)
            {
                RewriteEmbeddedJson(value, removed);
                return;
            }

            if (token is not JObject json) return;

            if (json[ItemGuidKey] is JValue guidValue && TryFindMissingItem(guidValue, out var guidText))
            {
                Debug.LogWarning($"マスタに存在しないアイテムを空スタックへ落とします。 itemGuid={guidText} count={json[CountKey]}");
                removed.Add(json.DeepClone());
                json[ItemGuidKey] = Guid.Empty.ToString();
                json[CountKey] = 0;
            }

            foreach (var property in json.Properties().ToList()) WalkForItemStacks(property.Value, removed);
        }

        private bool TryFindMissingItem(JValue guidValue, out string guidText)
        {
            guidText = guidValue.Value<string>();
            if (!Guid.TryParse(guidText, out var guid)) return false;
            if (guid == Guid.Empty) return false;
            return !MasterHolder.ItemMaster.ExistItemId(guid);
        }

        // ブロックのstateはJSON文字列の入れ子。読めた場合だけ中身を除去して書き戻す
        // Block state nests JSON inside a string; only parsable values are pruned and written back
        private void RewriteEmbeddedJson(JValue value, JArray removed)
        {
            if (value.Type != JTokenType.String) return;
            var text = value.Value<string>();
            if (string.IsNullOrEmpty(text)) return;

            var trimmed = text.TrimStart();
            if (!trimmed.StartsWith("{") && !trimmed.StartsWith("[")) return;

            JToken embedded;
            // 外部入力(セーブファイル)のパースなので隔離目的のtry-catchを使う。base64-MessagePack等は素通しする
            // Parsing external input (the save file), so the isolation try-catch is allowed; base64 MessagePack passes through
            try
            {
                embedded = JToken.Parse(text);
            }
            catch (JsonReaderException e)
            {
                Debug.Log($"JSONとして読めない状態値はアイテム除去の対象外です。 reason={e.Message}");
                return;
            }

            var before = removed.Count;
            WalkForItemStacks(embedded, removed);
            if (removed.Count == before) return;

            value.Value = embedded.ToString(Formatting.None);
        }
    }
}
```

- [ ] **Step 7: Unity を再起動してコンパイルする**

Run: `uloop launch ./moorestech_client --restart` の後、45秒待ってから `uloop compile --project-path ./moorestech_client`
Expected: `ErrorCount 0`。

- [ ] **Step 8: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.CombinedTest\.Game\.MissingMasterPruneTest$"`
Expected: 7本すべて PASS。

- [ ] **Step 9: 既存の欠損 mapObject スキップに手を入れていないことを確認する（R6）**

Run: `git diff --stat origin/master..HEAD -- moorestech_server/Assets/Scripts/Game.Map`
Expected: 出力が空（`MapObjectDatastore.LoadMapObject` の既存スキップは無改変）。

Run: `grep -n "mapObject" moorestech_server/Assets/Scripts/Game.SaveLoad/Pruning/MissingMasterPruner.cs`
Expected: クラスの doc コメントに「Map objects are out of scope」の理由行が出ること。

- [ ] **Step 10: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.SaveLoad.Interface \
        moorestech_server/Assets/Scripts/Game.SaveLoad/Pruning \
        moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/MissingMasterPruneTest.cs \
        moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/MissingMasterPruneTest.cs.meta
git commit -m "$(cat <<'EOF'
feat(server): マスタ欠損のブロック・アイテム・研究をロード前に除去する

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HtaDWGdhRRiNho39vPY5Li
EOF
)"
```

---

### Task 3: ロード経路への組み込み（`SaveLoadPreparer`）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/PreparedSaveJson.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/SaveLoadPreparer.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs:86-109`（`LoadOrInitialize` の中身）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs:246-254`（セーブシステム登録ブロック）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerTest.cs`

**Interfaces:**
- Consumes: Task 1 の `SaveMigrationChain`／`SaveArchiveWriter`／`SaveArchiveDirectory`、Task 2 の `MissingMasterPruner`／`MissingMasterPruneReportStore`。既存 `WorldLoaderFromJson.Load(string jsonText)`。
- Produces:
  - `public sealed class Game.SaveLoad.Migration.PreparedSaveJson { public bool CanLoad { get; } public string BlockedReason { get; } public string SaveJsonText { get; } public MissingMasterPruneReport Report { get; } public static PreparedSaveJson Blocked(string reason); public static PreparedSaveJson Ready(string saveJsonText, MissingMasterPruneReport report); }`
  - `public sealed class Game.SaveLoad.Migration.SaveLoadPreparer { public SaveLoadPreparer(SaveMigrationChain chain, MissingMasterPruner pruner, SaveArchiveWriter archiveWriter, MissingMasterPruneReportStore reportStore); public PreparedSaveJson Prepare(string saveJsonText); }`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerTest.cs` を新規作成する:

```csharp
using System;
using System.IO;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Migration;
using Game.SaveLoad.Pruning;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class SaveLoadPreparerTest
    {
        private const string MissingGuid = "ffffffff-ffff-ffff-ffff-ffffffffffff";

        private string _archiveRoot;

        [SetUp]
        public void SetUp()
        {
            _archiveRoot = Path.Combine(Path.GetTempPath(), "moorestech-preparer-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_archiveRoot)) Directory.Delete(_archiveRoot, true);
        }

        // 除去された欠損ブロックを含むセーブが、実ロード経路で例外を出さずに通ること
        // A save containing a removed block must pass the real load path without throwing
        [Test]
        public void 欠損ブロック入りのセーブが除去後に実ロードできるTest()
        {
            var save = JObject.Parse(BuildSaveJsonText());
            ((JArray)save["world"]).Add(JObject.Parse($"{{\"blockGuid\":\"{MissingGuid}\",\"direction\":0,\"instanceId\":987660,\"state\":{{}},\"X\":80,\"Y\":0,\"Z\":80}}"));

            var (reportStore, preparer) = CreatePreparer();
            var prepared = preparer.Prepare(save.ToString());

            Assert.IsTrue(prepared.CanLoad, prepared.BlockedReason);
            Assert.AreEqual(1, prepared.Report.RemovedBlockCount);
            Assert.AreEqual(1, reportStore.Report.RemovedBlockCount);

            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.DoesNotThrow(() => (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(prepared.SaveJsonText));
        }

        [Test]
        public void 除去があると除去データのファイルが1本できるTest()
        {
            var save = JObject.Parse(BuildSaveJsonText());
            ((JArray)save["world"]).Add(JObject.Parse($"{{\"blockGuid\":\"{MissingGuid}\",\"direction\":0,\"instanceId\":987661,\"state\":{{}},\"X\":81,\"Y\":0,\"Z\":81}}"));

            var (_, preparer) = CreatePreparer();
            preparer.Prepare(save.ToString());

            var prunedRoot = SaveArchiveDirectory.FromSaveFileDirectory(_archiveRoot).PrunedRoot;
            Assert.AreEqual(1, Directory.GetFiles(prunedRoot, "*.json").Length);
        }

        // 除去0件（本番の常態）でファイルもレポートも生えないこと
        // The normal case: nothing removed leaves no file and no report
        [Test]
        public void 除去が無いと除去データのファイルは作られないTest()
        {
            var (reportStore, preparer) = CreatePreparer();

            var prepared = preparer.Prepare(BuildSaveJsonText());

            Assert.IsTrue(prepared.CanLoad, prepared.BlockedReason);
            Assert.IsFalse(prepared.Report.HasRemoval);
            Assert.IsFalse(reportStore.Report.HasRemoval);
            Assert.IsFalse(Directory.Exists(SaveArchiveDirectory.FromSaveFileDirectory(_archiveRoot).PrunedRoot));
        }

        [Test]
        public void 未来版のセーブは準備段階で拒否されるTest()
        {
            var save = JObject.Parse(BuildSaveJsonText());
            save["worldVersion"] = 999;

            var (_, preparer) = CreatePreparer();
            var prepared = preparer.Prepare(save.ToString());

            Assert.IsFalse(prepared.CanLoad);
            StringAssert.Contains("999", prepared.BlockedReason);
            Assert.IsNull(prepared.SaveJsonText);
        }

        // マイグレーションが走らない現在版ではバックアップを作らない（毎回同じ原本を書き直さない）
        // No migration means no backup, so the same original is not rewritten on every boot
        [Test]
        public void 現在版のセーブではバックアップを作らないTest()
        {
            var (_, preparer) = CreatePreparer();

            preparer.Prepare(BuildSaveJsonText());

            Assert.IsFalse(Directory.Exists(SaveArchiveDirectory.FromSaveFileDirectory(_archiveRoot).BackupRoot));
        }

        private (MissingMasterPruneReportStore reportStore, SaveLoadPreparer preparer) CreatePreparer()
        {
            var reportStore = new MissingMasterPruneReportStore();
            var preparer = new SaveLoadPreparer(
                new SaveMigrationChain(Array.Empty<ISaveMigrationStep>()),
                new MissingMasterPruner(),
                new SaveArchiveWriter(SaveArchiveDirectory.FromSaveFileDirectory(_archiveRoot)),
                reportStore);
            return (reportStore, preparer);
        }

        private static string BuildSaveJsonText()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: FAIL。`SaveLoadPreparer` が存在しないコンパイルエラー。

- [ ] **Step 3: `PreparedSaveJson` を実装する**

`moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/PreparedSaveJson.cs`:

```csharp
using Game.SaveLoad.Interface;

namespace Game.SaveLoad.Migration
{
    /// <summary>ロード直前まで整えたセーブ。ロード不可のときは理由だけを運ぶ</summary>
    /// <summary>A save prepared up to the moment of load; when it cannot load it carries only the reason</summary>
    public sealed class PreparedSaveJson
    {
        public bool CanLoad { get; }
        public string BlockedReason { get; }
        public string SaveJsonText { get; }
        public MissingMasterPruneReport Report { get; }

        private PreparedSaveJson(bool canLoad, string blockedReason, string saveJsonText, MissingMasterPruneReport report)
        {
            CanLoad = canLoad;
            BlockedReason = blockedReason;
            SaveJsonText = saveJsonText;
            Report = report;
        }

        public static PreparedSaveJson Blocked(string reason)
        {
            return new PreparedSaveJson(false, reason, null, MissingMasterPruneReport.None);
        }

        public static PreparedSaveJson Ready(string saveJsonText, MissingMasterPruneReport report)
        {
            return new PreparedSaveJson(true, null, saveJsonText, report);
        }
    }
}
```

- [ ] **Step 4: `SaveLoadPreparer` を実装する**

`moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/SaveLoadPreparer.cs`:

```csharp
using System;
using Game.SaveLoad.Pruning;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration
{
    /// <summary>ロード前の準備を1本にまとめる: 版検出→退避→変換→マスタ欠損の除去→件数の記録</summary>
    /// <summary>One place for the pre-load work: detect version, archive, migrate, prune missing masters, record counts</summary>
    public sealed class SaveLoadPreparer
    {
        private readonly SaveMigrationChain _chain;
        private readonly MissingMasterPruner _pruner;
        private readonly SaveArchiveWriter _archiveWriter;
        private readonly MissingMasterPruneReportStore _reportStore;

        public SaveLoadPreparer(SaveMigrationChain chain, MissingMasterPruner pruner, SaveArchiveWriter archiveWriter, MissingMasterPruneReportStore reportStore)
        {
            _chain = chain;
            _pruner = pruner;
            _archiveWriter = archiveWriter;
            _reportStore = reportStore;
        }

        public PreparedSaveJson Prepare(string saveJsonText)
        {
            var save = JObject.Parse(saveJsonText);
            var fromVersion = SaveMigrationChain.ReadWorldVersion(save);

            // 変換を実際に行う版のときだけ原本を退避する。現在版で毎回書き直さない
            // Archive the original only when a migration will actually run, not on every boot at the current version
            if (fromVersion >= 1 && fromVersion < Json.WorldVersions.WorldSaveAllInfoV1.CurrentVersion)
                _archiveWriter.WriteBackup(fromVersion, saveJsonText);

            var migration = _chain.Migrate(save);
            if (!migration.CanLoad)
            {
                Debug.LogError($"セーブをロードできません: {migration.BlockedReason}");
                return PreparedSaveJson.Blocked(migration.BlockedReason);
            }

            // 版が上がらなくてもマスタは変わるので、除去は毎回のロードで走らせる
            // The master changes even when the version does not, so pruning runs on every load
            var outcome = _pruner.Prune(migration.Save);
            if (outcome.Report.HasRemoval)
            {
                var utcNow = DateTime.UtcNow;
                _archiveWriter.WritePruned(outcome.ToPrunedJson(utcNow), utcNow);
                Debug.Log($"マスタ欠損で除去しました。 blocks={outcome.Report.RemovedBlockCount} items={outcome.Report.EmptiedItemStackCount} research={outcome.Report.RemovedResearchCount}");
            }

            _reportStore.SetReport(outcome.Report);
            return PreparedSaveJson.Ready(outcome.Save.ToString(), outcome.Report);
        }
    }
}
```

- [ ] **Step 5: `WorldLoaderFromJson.LoadOrInitialize` を差し替える**

`moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs` に `using Game.SaveLoad.Migration;` を足し、フィールドとコンストラクタ引数の末尾へ `SaveLoadPreparer` を1つ加える:

```csharp
        private readonly CleanRoomDatastore _cleanRoomDatastore;
        private readonly SaveLoadPreparer _saveLoadPreparer;
```

コンストラクタ引数の `IPlayerInventorySlotLevelDataStore playerInventorySlotLevelDataStore, CleanRoomDatastore cleanRoomDatastore)` を
`IPlayerInventorySlotLevelDataStore playerInventorySlotLevelDataStore, CleanRoomDatastore cleanRoomDatastore, SaveLoadPreparer saveLoadPreparer)` に変え、本体末尾へ `_saveLoadPreparer = saveLoadPreparer;` を足す。

`LoadOrInitialize()`（86〜109行）を次に差し替える:

```csharp
        public void LoadOrInitialize()
        {
            if (File.Exists(_worldDataDirectory.SaveJsonFilePath))
            {
                var json = File.ReadAllText(_worldDataDirectory.SaveJsonFilePath);

                // 版の変換とマスタ欠損の除去はロードの前段で終わらせる。Loadは整った形だけを受ける
                // Version migration and missing-master pruning finish before load; Load only ever sees a prepared shape
                var prepared = _saveLoadPreparer.Prepare(json);
                if (!prepared.CanLoad)
                {
                    Debug.LogError($"セーブファイルパス {_worldDataDirectory.SaveJsonFilePath}");
                    throw new Exception($"セーブファイルをロードできないため起動を中断しました。\n Reason : {prepared.BlockedReason}");
                }

                try
                {
                    Load(prepared.SaveJsonText);
                    Debug.Log("セーブデータのロードが完了しました。");
                    return;
                }
                catch (Exception e)
                {
                    //TODO ログ基盤
                    Debug.Log("セーブデータが破損していたか古いバージョンでした。削除したら治る可能性があります。\nサポートが必要な場合はDiscordサーバー ( https://discord.gg/ekFYmY3rDP ) にて連絡をお願いします。");
                    Debug.Log($"セーブファイルパス {_worldDataDirectory.SaveJsonFilePath}");
                    throw new Exception(
                        $"セーブファイルのロードに失敗しました。セーブファイルを確認してください。\n Message : {e.Message} \n StackTrace : {e.StackTrace}");
                }
            }

            Debug.Log("セーブデータがありませんでした。新規作成します。");
            WorldInitialize();
        }
```

- [ ] **Step 6: DI へ登録する**

`moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs` の `services.AddSingleton<IWorldSaveCompletionNotifier>(...)`（254行）の直後に足す:

```csharp
            // セーブの版変換・マスタ欠損の除去・世代付き保管はロードの前段として1本で組む
            // Version migration, missing-master pruning and generational archiving form one pre-load stage
            services.AddSingleton(SaveArchiveDirectory.Default());
            services.AddSingleton<SaveArchiveWriter>();
            services.AddSingleton(new SaveMigrationChain(Array.Empty<ISaveMigrationStep>()));
            services.AddSingleton<MissingMasterPruner>();
            services.AddSingleton<MissingMasterPruneReportStore>();
            services.AddSingleton<IMissingMasterPruneReportLookup>(provider => provider.GetRequiredService<MissingMasterPruneReportStore>());
            services.AddSingleton<SaveLoadPreparer>();
```

ファイル冒頭の using に `using Game.SaveLoad.Migration;`、`using Game.SaveLoad.Pruning;` を足す（`using System;`・`using Game.Paths;`・`using Game.SaveLoad.Interface;` は既存を確認し、無ければ足す）。

- [ ] **Step 7: Unity を再起動してコンパイルする**

Run: `uloop launch ./moorestech_client --restart` の後、45秒待ってから `uloop compile --project-path ./moorestech_client`
Expected: `ErrorCount 0`。

- [ ] **Step 8: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.CombinedTest\.Game\.SaveLoadPreparerTest$"`
Expected: 5本すべて PASS。

- [ ] **Step 9: 既存のセーブ系テストが壊れていないことを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveLoad|SaveJson|WorldSaveCoordinator"`
Expected: すべて PASS（`WorldLoaderFromJson` のコンストラクタ引数が増えたが、生成は全て DI 経由なのでテスト側の変更は不要）。1本でも赤なら、DI 登録漏れか using 漏れを疑う。

- [ ] **Step 10: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.SaveLoad/Migration \
        moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs \
        moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs \
        moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerTest.cs \
        moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerTest.cs.meta
git commit -m "$(cat <<'EOF'
feat(server): ロード前段にマイグレーションとマスタ欠損除去を組み込む

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HtaDWGdhRRiNho39vPY5Li
EOF
)"
```

---

### Task 4: 除去件数のプレイヤー通知（サーバー→webui、日英独）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Event/Notification/NotificationMessagePack.cs`（enum に1値追加・static factory を1本追加）
- Create: `moorestech_server/Assets/Scripts/Server.Event/Notification/MissingMasterPruneNotificationWiring.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`（`AchievementNotificationWiring` 登録の直後）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Notification/NotificationCategoryTable.cs`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/ui.ts:103`
- Modify: `moorestech_web/webui/src/features/notification/notificationMessages.ts`
- Modify: `Localization/localization.csv`（`ui.notification.unknownMessage` の直後に1行）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/MissingMasterPruneNotificationTest.cs`
- Test: `moorestech_web/webui/src/features/notification/notificationMessages.test.ts`（ケース追加）

**Interfaces:**
- Consumes: Task 2 の `IMissingMasterPruneReportLookup`・`MissingMasterPruneReport`。既存 `Server.Event.EventProtocolProvider.OnPlayerEventStreamRegistered`（`IObservable<int>`）、`Server.Event.Notification.NotificationService.NotifyWithoutCooldown(int playerId, NotificationMessagePack notification)`。
- Produces:
  - `Server.Event.Notification.NotificationCategory.SaveMigration`（enum の末尾に追加）
  - `public static NotificationMessagePack NotificationMessagePack.CreateSaveMigrationPruned(int removedBlockCount, int emptiedItemStackCount, int removedResearchCount)` — `MessageId = "saveMigration.missingMasterPruned"`、`MessageParams = new[] { blocks, items, research }`（すべて `ToString()`）
  - `public class Server.Event.Notification.MissingMasterPruneNotificationWiring : IBootInitializable { public MissingMasterPruneNotificationWiring(NotificationService notificationService, EventProtocolProvider eventProtocolProvider, IMissingMasterPruneReportLookup reportLookup); public void Load(); }` — `NotificationService` は `EventProtocolProvider` を private に抱えていて外から取れないため、購読元として同じ provider も別引数で受ける
  - web カテゴリ名 `"saveMigration"`、文言キー `ui.notification.saveMigrationMissingMasterPruned`

- [ ] **Step 1: 失敗するテストを書く（サーバー）**

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/MissingMasterPruneNotificationTest.cs` を新規作成する:

```csharp
using Game.SaveLoad.Interface;
using Game.SaveLoad.Pruning;
using MessagePack;
using NUnit.Framework;
using Server.Event;
using Server.Event.Notification;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class MissingMasterPruneNotificationTest
    {
        // 接続したプレイヤーへ1回だけ届くこと。除去はロード時に終わっており後から変化しない
        // Arrives exactly once for a connecting player; pruning finished at load and never changes afterwards
        [Test]
        public void 除去があるとsink登録時に通知が1件届くTest()
        {
            var provider = new EventProtocolProvider();
            var reportStore = new MissingMasterPruneReportStore();
            reportStore.SetReport(new MissingMasterPruneReport(3, 4, 5));
            new MissingMasterPruneNotificationWiring(new NotificationService(provider), provider, reportStore).Load();

            var sink = new CapturedEventSink();
            provider.RegisterPlayer(1, sink);

            Assert.AreEqual(1, sink.Events.Count);
            Assert.AreEqual(NotificationService.EventTag, sink.Events[0].Tag);
            var message = MessagePackSerializer.Deserialize<NotificationMessagePack>(sink.Events[0].Payload);
            Assert.AreEqual(NotificationCategory.SaveMigration, message.Category);
            Assert.AreEqual("saveMigration.missingMasterPruned", message.MessageId);
            Assert.AreEqual(new[] { "3", "4", "5" }, message.MessageParams);
        }

        // 除去0件（本番の常態）で通知が出ないこと
        // Nothing removed, the normal case, must produce no notification
        [Test]
        public void 除去が無いと通知が出ないTest()
        {
            var provider = new EventProtocolProvider();
            var reportStore = new MissingMasterPruneReportStore();
            new MissingMasterPruneNotificationWiring(new NotificationService(provider), provider, reportStore).Load();

            var sink = new CapturedEventSink();
            provider.RegisterPlayer(1, sink);

            Assert.AreEqual(0, sink.Events.Count);
        }

        // 2人目の接続にも届くこと（ブロードキャストではなく接続ごとのpushである確認）
        // The second player also receives it, proving this is a per-connection push, not a broadcast
        [Test]
        public void 後から接続したプレイヤーにも届くTest()
        {
            var provider = new EventProtocolProvider();
            var reportStore = new MissingMasterPruneReportStore();
            reportStore.SetReport(new MissingMasterPruneReport(1, 0, 0));
            new MissingMasterPruneNotificationWiring(new NotificationService(provider), provider, reportStore).Load();

            provider.RegisterPlayer(1, new CapturedEventSink());
            var secondSink = new CapturedEventSink();
            provider.RegisterPlayer(2, secondSink);

            Assert.AreEqual(1, secondSink.Events.Count);
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: FAIL。`NotificationCategory.SaveMigration` と `MissingMasterPruneNotificationWiring` が存在しないコンパイルエラー。

- [ ] **Step 3: `NotificationMessagePack` にカテゴリと生成口を足す**

`moorestech_server/Assets/Scripts/Server.Event/Notification/NotificationMessagePack.cs` の enum に1値足す:

```csharp
    public enum NotificationCategory
    {
        Achievement,
        OperationDenied,
        ItemEarned,
        SaveMigration,
    }
```

クラス末尾（`CreateItemEarned` の後）に static factory を足す:

```csharp
        // 除去件数はロード時に確定する3つの数。Web側が文言を持つのでIDと数だけを送る
        // The three counts settle at load time; only the id and the numbers travel since the web owns the wording
        public static NotificationMessagePack CreateSaveMigrationPruned(int removedBlockCount, int emptiedItemStackCount, int removedResearchCount)
            => new(NotificationCategory.SaveMigration, "saveMigration.missingMasterPruned",
                new[] { removedBlockCount.ToString(), emptiedItemStackCount.ToString(), removedResearchCount.ToString() },
                ItemMaster.EmptyItemId, 0);
```

- [ ] **Step 4: `MissingMasterPruneNotificationWiring` を実装する**

`moorestech_server/Assets/Scripts/Server.Event/Notification/MissingMasterPruneNotificationWiring.cs`:

```csharp
using Game.Context;
using Game.SaveLoad.Interface;
using UniRx;

namespace Server.Event.Notification
{
    /// <summary>
    /// マスタ欠損で除去した件数を、接続したプレイヤーへ1回だけ知らせる
    /// Tells each connecting player once how much was removed for missing master data
    /// 除去はワールドのロード時に終わっており誰も接続していないので、broadcastではなく接続時pushで届ける
    /// Pruning finishes at world load while nobody is connected, so this pushes on connect instead of broadcasting
    /// </summary>
    public class MissingMasterPruneNotificationWiring : IBootInitializable
    {
        private readonly NotificationService _notificationService;

        // NotificationServiceはproviderをprivateに抱えるため、購読元として同じproviderを別に受ける
        // NotificationService keeps its provider private, so the same provider is injected separately to subscribe on
        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IMissingMasterPruneReportLookup _reportLookup;

        public MissingMasterPruneNotificationWiring(NotificationService notificationService, EventProtocolProvider eventProtocolProvider, IMissingMasterPruneReportLookup reportLookup)
        {
            _notificationService = notificationService;
            _eventProtocolProvider = eventProtocolProvider;
            _reportLookup = reportLookup;
        }

        public void Load()
        {
            // sink登録直後の同期push契約に乗る（前例: TrainFullSnapshotEventPacket）
            // Rides the synchronous push contract right after sink registration (precedent: TrainFullSnapshotEventPacket)
            _eventProtocolProvider.OnPlayerEventStreamRegistered.Subscribe(NotifyIfPruned);
        }

        private void NotifyIfPruned(int playerId)
        {
            var report = _reportLookup.Report;
            if (!report.HasRemoval)
            {
                // 除去0件は本番の常態。理由を残しておかないと「通知が来ない」の切り分けができない
                // Zero removals is the normal case; without this line a missing notice cannot be diagnosed
                UnityEngine.Debug.Log($"マスタ欠損の除去が無いためplayerId={playerId}への通知は出しません。");
                return;
            }

            // 再接続のたびに出し直す。クールダウンで握り潰すと接続直後の1回が消える
            // Re-sent on every reconnect; the cooldown would swallow the single post-connect notice
            _notificationService.NotifyWithoutCooldown(playerId,
                NotificationMessagePack.CreateSaveMigrationPruned(report.RemovedBlockCount, report.EmptiedItemStackCount, report.RemovedResearchCount));
        }
    }
}
```

購読は購読しっぱなしにする（`IDisposable` を持たない）。AGENTS.md「ゲームは起動後メインメニューへ戻らない」の既知の制約により、ゲーム寿命オブジェクトの dispose 漏れは考慮不要であり、前例 `TrainFullSnapshotEventPacket`・`AchievementNotificationWiring` も購読を保持しない。

- [ ] **Step 5: DI へ登録する**

`moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs` の `services.AddSingleton<AchievementNotificationWiring>();` の直後に足す:

```csharp
            services.AddSingleton<MissingMasterPruneNotificationWiring>();
```

- [ ] **Step 6: クライアントのカテゴリ表を更新する**

`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Notification/NotificationCategoryTable.cs` の switch に1行足す:

```csharp
                NotificationCategory.ItemEarned => "itemEarned",
                NotificationCategory.SaveMigration => "saveMigration",
```

- [ ] **Step 7: Unity を再起動してコンパイルし、サーバー側テストを通す**

Run: `uloop launch ./moorestech_client --restart` の後、45秒待ってから `uloop compile --project-path ./moorestech_client`
Expected: `ErrorCount 0`。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.CombinedTest\.Server\.PacketTest\.Event\.MissingMasterPruneNotificationTest$"`
Expected: 3本すべて PASS。

- [ ] **Step 8: 文言を日英独で追加する**

`Localization/localization.csv` の `ui.notification.unknownMessage,...` の行（119行目）の直後に1行足す（列は `key,Source,english,japanese,german`。`{p0}`=ブロック数、`{p1}`=アイテム数、`{p2}`=研究数）:

```csv
ui.notification.saveMigrationMissingMasterPruned,Removed data missing from this version: {p0} blocks, {p1} item stacks, {p2} research,Removed data missing from this version: {p0} blocks, {p1} item stacks, {p2} research,このバージョンに無くなったデータを取り除きました: ブロック{p0}個・アイテム{p1}枠・研究{p2}件,Nicht mehr vorhandene Daten entfernt: {p0} Blöcke, {p1} Gegenstandsstapel, {p2} Forschungen
```

- [ ] **Step 9: 生成物を更新する**

```bash
pnpm -C moorestech_web/webui install
pnpm -C moorestech_web/webui gen:i18n
```
Expected: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts` に `saveMigrationMissingMasterPruned` が入る。

C# 側の生成キー（`Mooresmaster.Localization.Generated.LocalizationKeys`）は force-recompile が要る:

Run: `uloop launch ./moorestech_client --restart` の後、45秒待ってから `uloop compile --project-path ./moorestech_client`
Expected: `ErrorCount 0`（触っていないキーの CS0117 が出たら CSV の再生成漏れ）。

- [ ] **Step 10: webui の契約とテストを更新する**

`moorestech_web/webui/src/bridge/contract/schemas/ui.ts` の103行目を差し替える:

```ts
  category: z.enum(["achievement", "operationDenied", "saveMigration"]),
```

`moorestech_web/webui/src/features/notification/notificationMessages.ts` の `notificationKeys` マップの末尾（`["denied.blueprint.NotUnlocked", ...]` の次）へ1行足す:

```ts
  ["saveMigration.missingMasterPruned", L.ui.notification.saveMigrationMissingMasterPruned],
```

同ファイルの `resolveNotificationText` の分岐に `saveMigration` を加える（`never` 網羅がコンパイルエラーで要求する）:

```ts
    case "achievement":
    case "operationDenied":
    case "saveMigration":
      return { key: resolveNotificationKey(notification.messageId), values };
```

`moorestech_web/webui/src/features/notification/notificationMessages.test.ts` の `describe("notificationMessages", ...)` にケースを足す:

```ts
  it("マスタ欠損の除去通知は3つの件数を補間する", () => {
    const translate = (key: string) => `resolved:${key}`;
    expect(resolveNotificationText(
      { category: "saveMigration", messageId: "saveMigration.missingMasterPruned", messageParams: ["3", "4", "5"], itemId: null, id: 9, lifetimeEpoch: 0 },
      translate,
      (itemId: number) => `item:${itemId}`,
    )).toEqual({
      key: L.ui.notification.saveMigrationMissingMasterPruned,
      values: { messageId: "saveMigration.missingMasterPruned", p0: "3", p1: "4", p2: "5" },
    });
  });
```

- [ ] **Step 11: webui のテストを実行して通ることを確認する**

Run: `pnpm -C moorestech_web/webui test`
Expected: すべて PASS（特に `localizationKeys freshness` と `notificationMessages`）。赤なら `gen:i18n` の実行漏れを疑う。

- [ ] **Step 12: クライアント側の wire 契約テストを実行する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.WebUi\.(WireContractNotificationTest|Localization\.LocalizationRevisionContractTest)$"`
Expected: すべて PASS。

- [ ] **Step 13: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Event/Notification \
        moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs \
        moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Notification/NotificationCategoryTable.cs \
        moorestech_web/webui/src/bridge/contract/schemas/ui.ts \
        moorestech_web/webui/src/features/notification/notificationMessages.ts \
        moorestech_web/webui/src/features/notification/notificationMessages.test.ts \
        moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts \
        Localization/localization.csv \
        moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/MissingMasterPruneNotificationTest.cs \
        moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/MissingMasterPruneNotificationTest.cs.meta
git commit -m "$(cat <<'EOF'
feat: マスタ欠損の除去件数を接続時に1回通知する(日英独)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HtaDWGdhRRiNho39vPY5Li
EOF
)"
```

---

### Task 5: 規約の追記とスキルの役割変更

**Files:**
- Modify: `AGENTS.md:7-8`（「## 互換性とパフォーマンス」）
- Modify: `.agents/skills/moorestech-save-migration/SKILL.md`
- Create: `.agents/skills/moorestech-save-migration/references/save-migration-step-template.md`

**Interfaces:**
- Consumes: Task 1 の `ISaveMigrationStep`／`SaveMigrationChain`／`WorldSaveAllInfoV1.CurrentVersion`、Task 3 の DI 登録位置。
- Produces: 文書のみ。後続タスクがコードとして参照するものは無い。

- [ ] **Step 1: `AGENTS.md` にセーブ形式の例外を追記する**

`AGENTS.md` の7〜8行目を次に差し替える:

```markdown
## 互換性とパフォーマンス
計画立案時、後方互換性・パフォーマンス最適化・将来の拡張性は考慮不要です。より良い設計と動作する実装を優先し、改善は必要に応じて後から行います。

**唯一の例外: セーブ形式。** テスターのワールドを跨いで保つ必要があるため、セーブ形式を変えるPRは (1) `WorldSaveAllInfoV1.CurrentVersion` を1つ上げ、(2) `Game.SaveLoad/Migration/` に `ISaveMigrationStep` 実装を1本足して `MoorestechServerDIContainerGenerator` の `SaveMigrationChain` へ登録し、(3) 旧版セーブが新版へ変換されることを確かめるテストを、同じPRに同梱してください。連鎖に欠番があると起動時に例外で落ちます。書き方は moorestech-save-migration スキルが正本です（裁定: ADR 0061・`.decisions/2026-09-13-テスターのセーブ互換はゲーム内ロード時マイグレーション連鎖で保つ.md`）。
```

- [ ] **Step 2: ステップのテンプレートを書く**

`.agents/skills/moorestech-save-migration/references/save-migration-step-template.md` を新規作成する:

````markdown
# ISaveMigrationStep のテンプレート

題材は plan A（`docs/superpowers/plans/2026-09-11-bug-report-a-server-foundation.md` の R3・R4）の形式変更:
`worldVersion` 1 → 2 で `currentTick`（ulong）と `randomState`（ulong[4]）が増え、`world[].state` の値が
JSON文字列からオブジェクトへ変わる。plan A がマージされたとき、このファイルをコピーして
`moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV1ToV2.cs` として置く。

## 1. ステップ本体

```csharp
using System;
using Game.SaveLoad.Migration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    /// <summary>V1→V2: currentTick と randomState を足し、ブロック状態の入れ子文字列をオブジェクトへ開く</summary>
    /// <summary>V1 to V2: adds currentTick and randomState, and unwraps nested block-state strings into objects</summary>
    public sealed class SaveMigrationStepV1ToV2 : ISaveMigrationStep
    {
        // 旧セーブには乱数状態が無い。固定seedから作り、続きの列だけを決定的にする
        // Old saves carry no random state; derive it from a fixed seed so only the continuation is deterministic
        private const ulong LegacySeed = 0x9E3779B97F4A7C15UL;

        public int FromVersion => 1;

        public JObject Migrate(JObject save)
        {
            if (save["currentTick"] == null) save["currentTick"] = 0UL;
            if (save["randomState"] == null) save["randomState"] = BuildRandomState();
            UnwrapBlockStates();

            return save;

            #region Internal

            JArray BuildRandomState()
            {
                var state = new JArray();
                var x = LegacySeed;
                for (var i = 0; i < 4; i++)
                {
                    // splitmix64。4語すべてが0になる状態を避けるためだけの初期化
                    // splitmix64, used only to avoid an all-zero four-word state
                    x += 0x9E3779B97F4A7C15UL;
                    var z = x;
                    z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                    z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                    state.Add(z ^ (z >> 31));
                }

                return state;
            }

            void UnwrapBlockStates()
            {
                if (save["world"] is not JArray world) return;

                foreach (var block in world)
                {
                    if (block["state"] is not JObject state) continue;
                    foreach (var property in state.Properties())
                    {
                        if (property.Value.Type != JTokenType.String) continue;
                        property.Value = ParseOrKeep(property.Value.Value<string>());
                    }
                }
            }

            JToken ParseOrKeep(string text)
            {
                // 外部入力(セーブファイル)のパースなので隔離目的のtry-catchを使う
                // Parsing external input (the save file), so the isolation try-catch is allowed
                try
                {
                    return JToken.Parse(text);
                }
                catch (JsonReaderException e)
                {
                    Debug.LogWarning($"V1→V2でJSONとして開けない状態値を文字列のまま残します。 reason={e.Message}");
                    return text;
                }
            }

            #endregion
        }
    }
}
```

## 2. 登録

`moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs` の
`services.AddSingleton(new SaveMigrationChain(...));` を差し替える:

```csharp
            services.AddSingleton(new SaveMigrationChain(new ISaveMigrationStep[]
            {
                new SaveMigrationStepV1ToV2(),
            }));
```

同じPRで `WorldSaveAllInfoV1.CurrentVersion` を `2` へ上げる。上げ忘れると `SaveMigrationChain` の
構築検証が「期待={} 実際={1}」で例外を投げて起動が止まる（意図した早期失敗）。

## 3. テスト

`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationStepV1ToV2Test.cs`:

```csharp
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class SaveMigrationStepV1ToV2Test
    {
        [Test]
        public void 旧セーブにtickと乱数状態が足されるTest()
        {
            var migrated = new SaveMigrationStepV1ToV2().Migrate(JObject.Parse("{\"worldVersion\":1,\"world\":[]}"));

            Assert.AreEqual(0UL, migrated["currentTick"].Value<ulong>());
            Assert.AreEqual(4, ((JArray)migrated["randomState"]).Count);
        }

        [Test]
        public void ブロック状態の文字列がオブジェクトへ開かれるTest()
        {
            var migrated = new SaveMigrationStepV1ToV2().Migrate(
                JObject.Parse("{\"worldVersion\":1,\"world\":[{\"state\":{\"chest\":\"{\\\"count\\\":2}\"}}]}"));

            Assert.AreEqual(2, migrated["world"][0]["state"]["chest"]["count"].Value<int>());
        }

        [Test]
        public void JSONで無い状態値は文字列のまま残るTest()
        {
            var migrated = new SaveMigrationStepV1ToV2().Migrate(
                JObject.Parse("{\"worldVersion\":1,\"world\":[{\"state\":{\"rail\":\"kZPAAAA=\"}}]}"));

            Assert.AreEqual("kZPAAAA=", migrated["world"][0]["state"]["rail"].Value<string>());
        }
    }
}
```

## 4. 落とし穴

- **`worldVersion` はステップで触らない。** `SaveMigrationChain` が1手ごとに `FromVersion + 1` を書く。
- **マスタを引かない。** マスタに無い guid の始末は `MissingMasterPruner` の仕事で、連鎖の後段にある。
  ステップの中で `MasterHolder` を引くと、削除済みマスタで変換自体が落ちる。
- **前の版のクラスを参照しない。** 変換は `JObject` の上だけで行う。`WorldSaveAllInfoV1` を
  旧版形状で読もうとすると、次の形式変更のたびに過去のステップが壊れる。
- **冪等にする。** 既にキーが在るときは足さない（上のコードの `== null` 判定）。バックアップから
  戻して再実行する運用があるため。
````

- [ ] **Step 3: `SKILL.md` を書き換える**

`.agents/skills/moorestech-save-migration/SKILL.md` の全文を次に差し替える:

````markdown
---
name: moorestech-save-migration
description: moorestech のセーブ形式を変えるとき、ロード時マイグレーションのステップ（ISaveMigrationStep）を書いて既存ワールドを保つ。旧版セーブの手変換（worldVersion 導入前・開発者手元セーブ）も扱う。Use When — 「セーブ形式を変える」「WorldSaveAllInfoV1にフィールドを足す」「セーブが旧形式でロードできない」「マイグレーションステップを書いて」「ロード時にNRE/JSONパースエラーが出る」と言われた場合。
---

# moorestech Save Migration

## 方針（2026-09-13 に反転した）

テスター配布が始まったため、**セーブ形式を変えるPRはロード時マイグレーションのステップを同梱する**（ADR 0061、
`.decisions/2026-09-13-テスターのセーブ互換はゲーム内ロード時マイグレーション連鎖で保つ.md`、AGENTS.md
「互換性とパフォーマンス」の例外項）。以前の「コード側に互換を書かずセーブファイルを手で変換する」方針は、
worldVersion 導入前のセーブと開発者手元のセーブに限った補助手順へ降格した。

セーブ実体は `<GameSystemPaths.SaveFileDirectory>/world_1/save.json`。
マイグレーション前の原本は `Saves/backup/<version>/save.json`、マスタ欠損で除去した実体は
`Saves/pruned/<UTC時刻>.json` に残る。

## 主手順: マイグレーションステップを書く

1. **`WorldSaveAllInfoV1.CurrentVersion` を1つ上げる**（`Game.SaveLoad/Json/WorldVersions/`）。
2. **`Game.SaveLoad/Migration/Steps/SaveMigrationStepV<n>ToV<n+1>.cs` を作る。**
   `references/save-migration-step-template.md` をコピーして書き始める。実装は `JObject` の上だけで行い、
   `worldVersion` は触らない（連鎖が書く）。
3. **`MoorestechServerDIContainerGenerator` の `new SaveMigrationChain(...)` へ足す。**
   `FromVersion` が `1..CurrentVersion-1` を欠番なく覆っていないと、構築時に例外で起動が止まる。
4. **単体テストを同じPRに入れる**（`Tests/UnitTest/Game/SaveLoad/SaveMigrationStepV<n>ToV<n+1>Test.cs`）。
   テンプレートの「3. テスト」がそのまま雛形。
5. **実ロードで確かめる**（下の「実ロード検証」）。デシリアライズ単体の確認では足りない。

### 連鎖の契約（`Game.SaveLoad/Migration/`）

| 型 | 役割 |
| --- | --- |
| `ISaveMigrationStep` | `int FromVersion { get; }` と `JObject Migrate(JObject save)` の1手 |
| `SaveMigrationChain` | 構築時に欠番・重複を検証し、`Migrate` で昇順適用。未来版・版0以下は `CanLoad = false` |
| `SaveArchiveWriter` | 変換が走るときだけ原本を `Saves/backup/<version>/save.json` へ退避（既存は上書きしない） |
| `MissingMasterPruner` | 連鎖の**後段**。マスタから消えたブロック・アイテム・研究を除去する |
| `SaveLoadPreparer` | 上を束ねて `WorldLoaderFromJson.LoadOrInitialize` から呼ばれる |

**マスタからの削除はマイグレーションではない。** ブロック・アイテム・研究ノードを消しただけなら
ステップは要らない。`MissingMasterPruner` が毎回のロードで除去し、件数がプレイヤーへ通知される。

## 実ロード検証（必須）

`references/load_test.cs` を `uloop execute-dynamic-code --project-path ./moorestech_client` で実行し、
`MoorestechServerDIContainerGenerator.Create` → `IWorldSaveDataLoader.LoadOrInitialize()` の経路で
`LOAD OK | blocks=N` を確認する。失敗時の例外メッセージが次の未対応形式を教える。
重要データ（列車インベントリ等）の実値は `references/verify_loaded.cs` で確認する。

## 補助手順: セーブファイルの手変換（worldVersion 導入前・開発者手元セーブ限定）

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
  「期待={} 実際={1}」で落ちるので、起動した瞬間に気づける（意図した早期失敗）。
- **前の版のセーブクラスを参照しない。** `JObject` だけで書く。過去のステップが将来の形式変更で壊れる。
- **`uloop execute-dynamic-code` は `System.IO` 全面禁止**（`Path` も）。ファイル入出力は Python/bash 側。
- **C# の char リテラル `'/'` はシェルの single quote と衝突**する。スニペットは一時 `.cs` に書き
  `--code "$(cat file.cs)"` で渡す。
- **base64-MessagePack の状態値がある**（例 `RailComponentStateDetail = "kZP..."`）。JSON として読めない
  値は触らず素通しする。
- **コンテナのスロット数 > master.InventorySlots だとロード時に切り詰められる。** 変換ファイル側では
  保持できてもロードで落ちるので、事前検出して master 側の修正で回避できるか確認する。
- **マスタ参照値（capacity・スロット数）は保存形式に含めない。** ロード時に master から解決される。

## Available scripts (references/)

- `references/save-migration-step-template.md` — ステップの完全なテンプレート（本体・登録・テスト・落とし穴）。**主手順の入口**
- `references/dump_id_maps.cs` — 独立 v8 マスタから item/fluid の id→GUID 全マップを返す（補助手順）
- `references/migrate_save_template.py` — backup→変換→安全スキャン→書き戻しの雛形（補助手順）
- `references/load_test.cs` — DI フルロードで `LOAD OK | blocks=N` を確認（実ロード検証）
- `references/verify_loaded.cs` — ロード後の実値ダンプで欠損検査（実ロード検証）
````

- [ ] **Step 4: スキルのミラーが symlink のままであることを確認する**

Run: `ls -l .claude/skills/moorestech-save-migration .codex/skills/moorestech-save-migration`
Expected: どちらも `.agents/skills/moorestech-save-migration` への symlink（実体の複製が生まれていないこと）。

- [ ] **Step 5: コミットする**

```bash
git add AGENTS.md .agents/skills/moorestech-save-migration/SKILL.md \
        .agents/skills/moorestech-save-migration/references/save-migration-step-template.md
git commit -m "$(cat <<'EOF'
docs: セーブ形式変更PRにマイグレーションステップを義務づけスキルを書き換える

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HtaDWGdhRRiNho39vPY5Li
EOF
)"
```

---

### Task 6: コードレビュースキルによる全ブランチレビュー（省略不可）

**Files:**
- Modify: レビュー指摘に応じて本planで触った全ファイル

**Interfaces:**
- Consumes: Task 1〜5 の全成果物。
- Produces: なし（レビュー通過状態のブランチ）。

- [ ] **Step 1: 未コミットが無いことを確認する**

Run: `git status --short`
Expected: 空、または `.moorestech-external-revisions.json` と `_CompileRequester.cs` の自動書き換えだけ。後者2つは `git checkout --` で戻す（本planはスキーマを変えていない）。

- [ ] **Step 2: 全ブランチレビューを実行する**

moores-code-review スキルを `origin/master..HEAD` の全差分に対して実行する。**この実行は省略できない**（ゴール文言や「小さい変更だから」を理由に飛ばさない）。

- [ ] **Step 3: 機械的指摘を反映して再検証する**

指摘を反映したら:

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ErrorCount 0`

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveMigrationChainTest|MissingMasterPruneTest|SaveLoadPreparerTest|MissingMasterPruneNotificationTest"`
Expected: すべて PASS

Run: `pnpm -C moorestech_web/webui test`
Expected: すべて PASS

レビュー反映が**判定経路**（`SaveMigrationChain.Migrate` の版判定、`MissingMasterPruner` の除去条件、
`SaveLoadPreparer.Prepare` の退避条件、`MissingMasterPruneNotificationWiring.NotifyIfPruned` の
`HasRemoval` 判定）に触れた場合は、Task 3 Step 9 の既存セーブ系テスト一括実行も反映後に再実施してから完了とする。

- [ ] **Step 4: 設計判断が要る指摘だけを裁定に出す**

AskUserQuestion でまとめて出す。特に「## 判断記録（ADR）」に新規パターンとして挙げた項目に指摘が付いた場合は、独断で直さず裁定に出す。

- [ ] **Step 5: コミットする**

```bash
git status --short
git commit -am "$(cat <<'EOF'
fix: レビュー指摘を反映する

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HtaDWGdhRRiNho39vPY5Li
EOF
)"
```
（指摘が無く差分が空なら、このコミットは行わない。）

---

## 配置と前例（spec-architecture-review）

| # | 項目（型/メンバー/ファイル） | 配置先アセンブリ・層 | 使用する機構 | 前例・根拠 |
|---|---|---|---|---|
| 1 | `WorldSaveAllInfoV1.CurrentVersion`（const） | `Game.SaveLoad`（`Json/WorldVersions/`） | 定数 | 版の所有者は既存の `worldVersion` フィールドと同じクラス。層マップ「Game.SaveLoad = セーブJSON集約・ロード順制御」 |
| 2 | `SaveArchiveDirectory` | `Game.Paths` | 値オブジェクト（パス導出のみ） | `WorldDataDirectory`（「パス連結はここ以外で行わない」）と同役割。`GameSystemPaths.SaveFileDirectory` を読むだけ |
| 3 | `ISaveMigrationStep`／`SaveMigrationChain`／`SaveMigrationResult` | `Game.SaveLoad/Migration/` | `JObject` 変換（Newtonsoft） | 共有契約 §8 の指定どおり。永続化フォーマットは Newtonsoft JSON（層マップ機構規約表） |
| 4 | `SaveArchiveWriter` | `Game.SaveLoad/Migration/` | `File.WriteAllText` | 既存 `WorldSaveCoordinator.Save`（同アセンブリでのアトミック書き込み）と同じ層。原本の退避はセーブJSONの派生であり `Game.SaveLoad` の責務 |
| 5 | `SaveLoadPreparer`／`PreparedSaveJson` | `Game.SaveLoad/Migration/` | 明示呼び出し（`LoadOrInitialize` から駆動） | ロード順の決定は `Game.SaveLoad` の責務（層マップ）。`WorldLoaderFromJson` が既に持つ「ファイル読み→Load」の間に1段挟むだけで、購読で作らない |
| 6 | `MissingMasterPruner`／`MissingMasterPruneOutcome` | `Game.SaveLoad/Pruning/` | `MasterHolder` の `*OrNull`／`Exist*` を**読むだけ** | 共有契約 §8 の指定。機構規約表「マスタ生成物へのアクセス = `MasterHolder.XxxMaster` を読むだけ」。`Core.Master` 側への追加はゼロ |
| 7 | `MissingMasterPruneReport`／`IMissingMasterPruneReportLookup` | `Game.SaveLoad.Interface` | interface＋不変値 | 層マップ「Game.Xxx.Interface = 公開契約（interface・定数・JsonObject）」。`IWorldSaveRequest`／`IWorldSaveCompletionNotifier` と同型で、`Server.Event` は Interface だけを参照する（既に asmdef 参照済み） |
| 8 | `MissingMasterPruneReportStore` | `Game.SaveLoad/Pruning/` | 読み書き面の分離 | 機構規約表「可変DataStoreのアクセス面: 読み取り `I*Lookup`（DI公開）と変更（DI注入のみ）に分離」。`IItemStackLevelLookup`/`IItemStackLevelUnlocker` と同型 |
| 9 | `NotificationCategory.SaveMigration`／`CreateSaveMigrationPruned` | `Server.Event/Notification/`（既存ファイルへ追加） | MessagePack イベント payload の static factory | 既存 `CreateAchievement`／`CreateOperationDenied`／`CreateItemEarned` と同形。カテゴリ追加は `NotificationCategoryTable` の default 無し switch が CS8509 で追随を強制する設計 |
| 10 | `MissingMasterPruneNotificationWiring` | `Server.Event/Notification/` | `IBootInitializable` ＋ UniRx 購読 | `AchievementNotificationWiring`（同ディレクトリ・同 interface・ドメインイベントを通知基盤へ配線する役割）と同型 |
| 11 | 通知の送出タイミング（`OnPlayerEventStreamRegistered`） | `Server.Event` | sink 登録直後の同期 push | `TrainFullSnapshotEventPacket.cs:40`（`OnPlayerEventStreamRegistered.Subscribe(playerId => PushFullSnapshots(playerId, true))`）。`EventProtocolProvider` 自身が「購読者は同期的にAddEventすること（初期同期の順序契約）」とコメントで宣言している |
| 12 | web 側カテゴリ名と文言表 | `Client.WebUiHost/Game/Topics/Notification/`＋`moorestech_web/webui/src/features/notification/` | 既存 `notification.events` トピック | `NotificationTopic`（サーバー通知イベントを Web へ中継）＋`notificationMessages.ts`（messageId→表示テンプレート表。文言は Web が所有しサーバーは構造化IDのみ送る） |
| 13 | 文言（日英独） | `Localization/localization.csv` | `{p0}` 位置補間 | `ui.notification.researchCompleted`（`Research completed: {p0}`）と同形。列は `key,Source,english,japanese,german` |
| 14 | DI 登録 | `Server.Boot/MoorestechServerDIContainerGenerator.cs` | `services.AddSingleton` | 同ファイル内の既存セーブシステム登録ブロック（248〜254行）と通知 wiring 登録（`AchievementNotificationWiring`） |

備考（findings ではなく注意点）: `hotbarAssignments`（`HotbarAssignmentDatastore.LoadHotbar` が未解決 guid を `Guid.Empty` へ落とす）・`itemStackLevels`（`ItemStackLevelDataStore.LoadUnlockedLevels` が `ExistItemId` false を `continue`）・`mapObjects`（`MapObjectDatastore` がマップに無い instanceId をスキップ）は既に欠損 guid に耐えるため pruner の対象外にした。今日ロードで例外を投げるのはブロック（`BlockMaster.GetBlockId`）とアイテムスタック（`ItemMaster.GetItemId`）の2経路だけで、研究は例外こそ出ないが未知 guid を完了扱いのまま残すため除去対象に含めた。

データフロー地図（Phase 1.5）:

```
save.json（ファイル）→ WorldLoaderFromJson.LoadOrInitialize
  → [SaveLoadPreparer] 版検出 → SaveArchiveWriter.WriteBackup → SaveMigrationChain.Migrate
       → MissingMasterPruner.Prune → SaveArchiveWriter.WritePruned → MissingMasterPruneReportStore.SetReport
  → WorldLoaderFromJson.Load(string)（既存・無改変）→ 各DataStore
                                   ↓
  MissingMasterPruneReportStore（共有状態）
                                   ↓（読み手）
  MissingMasterPruneNotificationWiring ←購読← EventProtocolProvider.OnPlayerEventStreamRegistered
                                   → NotificationService（既存）→ va:event:notification
                                   → NotificationTopic（既存・無改変）→ notification.events → NotificationHost
```

新規コンポーネントの立ち位置: `SaveLoadPreparer`・`MissingMasterPruner`・`SaveMigrationChain` は「**書き手**」（ロード前のJSONと共有状態 `MissingMasterPruneReportStore` へ書くだけ）、`MissingMasterPruneNotificationWiring` は「**読み手**」（store の値を読んで既存の通知経路へ流すだけ）。既存フローへの分岐・逆流・並行経路（交差点）は足していない — 通知は既存の `NotificationService` 1本、web への中継は既存の `NotificationTopic` 1本のままで、2人目の書き手を作らない。

機構選択（検査4）: 「ロード前段に1段挟む」対「`WorldLoaderFromJson.Load` の中で各 DataStore のロード呼び出しごとに欠損を握り潰す」を比較した。後者は既存の16個のロード経路すべてに防御を撒く能動介入で、`Load(string)` を直接呼ぶ400本超のテストの前提も変える。前者は既存 `Load(string)` を無傷のまま動かし続け、新レイヤーは入力JSONの整形に徹する受動的統合であり、こちらを採る。通知側も同様に、「新規イベントパケット＋初期ハンドシェイクへのフィールド追加」（既存の通知面と並ぶ2本目の経路＝能動介入）ではなく「既存 `NotificationService` に1カテゴリ足す」（受動的統合）を採った。

死活表（Phase 2.5）:

| 現在ユーザーが使える操作 | 計画後も生きるか | 根拠 |
|---|---|---|
| 既存ワールドのロード（版1・欠損なし） | 生きる | `SaveMigrationChain` はステップ0本で素通し、pruner は0件で何も書かない（Task 3 Step 1 のテストで固定） |
| 新規ワールドの作成（save.json 無し） | 生きる | `LoadOrInitialize` の `File.Exists` false 経路は無改変 |
| `va:save`・オートセーブ・終了時 flush | 生きる | 保存側（`WorldSaveCoordinator`／`AssembleSaveJsonText`）に差分なし |
| 壊れたセーブのエラー表示（Discord 案内） | 生きる | `Load` を包む既存 try-catch は残す。準備段階の拒否は別メッセージで先に出る |
| ゲーム内通知（達成・拒否・獲得） | 生きる | カテゴリを1つ足すだけ。既存3カテゴリの経路に差分なし |
| webui のトースト（bridge の失敗通知） | 生きる | `features/toast` は触らない（本planは `notification.events` 側だけを使う） |
| 開発者手元の旧セーブ手変換 | 生きる | `moorestech-save-migration` スキルの補助手順として残す（references のスクリプトも削除しない） |
| 除去件数の通知（ロード前に接続したプレイヤー） | 起きない | `ServerInstanceManager.Start` は `LoadOrInitialize()` を終えてから `ServerListenAcceptor.CreateBoundListener` で待ち受けを開く。report が `None` のまま接続される順序は製品起動経路に存在しない。**リッスン開始をロードより前へ動かす変更を入れると、この通知が無音で消える**（その変更をするときは通知の再送経路を同時に設計すること） |
| 未来版セーブを持つプレイヤーの起動 | **止まる（意図した fail-closed）** | 新しいビルドのセーブを古いビルドで開いた場合。黙って新規ワールドを作ると原本を上書きするため、理由をログして中断する。解消条件は「ゲームを更新する」で、Steam の自動配信で必ず到達する（ADR 0061「更新はSteamの自動配信」）。これは既存の動作の喪失ではなく、これまで例外で落ちていた経路に理由を付けたもの |

## 判断記録（ADR）

- 設計ADR: `docs/adr/0061-steam-closed-playtest-report-receiver-and-save-compat.md`（セーブ互換2裁定と Consequences）／`docs/adr/0057-bug-report-bundle-and-isolated-auto-fix.md`（plan A の形式変更の出所）
- 裁定: `.decisions/2026-09-13-テスターのセーブ互換はゲーム内ロード時マイグレーション連鎖で保つ.md`、`.decisions/2026-09-13-マスタ削除はロード時除去で続行し除去データとマイグレーション前セーブを保持する.md`、`.decisions/2026-08-04-セーブの欠損mapObjectはロード時スキップにする.md`、`.decisions/2026-08-18-旧セーブのBPは未解放扱いでデータは保持する.md`、`.decisions/2026-08-18-セーブバックアップはworld_1固定でディレクトリ丸ごと.md`
- 共有契約: `shared-contracts.md` §8（本plan の Global Constraints へ逐語転記済み）

判断:

- **通知経路は「既存イベント＋接続時同期push＋既存購読」を選び、新規イベントパケットも初期ハンドシェイクへのフィールド追加も作らない**（agent前提。**新規パターンとしてレビュー注目点**）。役割で前例を選ぶと、除去件数は「サーバー可変状態」ではなく「ロード時に確定し二度と変わらない、プレイヤーへ1回見せるだけの知らせ」である。(a) 新規イベントパケット案は、除去がワールドロード時＝誰も接続していない時刻に完了するため `AddBroadcastEvent` の宛先が空で、発火しても必ず捨てられる恒久的な死経路になる。(b) 初期ハンドシェイクへのフィールド追加案（`ItemStackLevels`／`HotbarAssignments` の前例）は、クライアントが**保持して後から読む状態**のための形であり、表示して消える知らせには過剰で、しかも文言のローカライズ表（`notificationMessages.ts`）を通らない別経路を作る。(c) 採用案は、`EventProtocolProvider` 自身が宣言する順序契約（「sink登録完了直後に発火。購読者は同期的にAddEventすること（初期同期の順序契約）」）に乗るもので、役割同型の前例 `TrainFullSnapshotEventPacket`（接続時に蓄積済み状態をそのプレイヤーへ押し込む）がそのまま当てはまる。3点セットの①は既存 `NotificationService`、②は接続時 push、③は既存 `NotificationTopic` が担う。
- **「導出できる」とは主張しない**（agent前提）。除去件数はサーバーがロード時にしか知り得ず、クライアントが既存の同期情報から復元する手段は無い。層マップの「導出＝既存イベントが同じ情報をそのまま運んでいる場合のみ」に該当しないため、通知そのものは新設する（新設するのは payload の1カテゴリだけで、イベント・プロトコルは既存を使う）。
- **トーストは `features/toast` の `emitToast` ではなく `notification.events`→`NotificationHost` を使う**（agent前提。**契約文言の具体化としてレビュー注目点**）。`emitToast` の呼び出し側（`bridge/transport/actions.ts` 等）はすべて生の英語文字列を渡しており、ローカライズ経路を持たない。ADR 0061 は「テスター向け文言は日英独すべて必須」なので、messageId＋params でサーバーから送り Web 側の辞書で解決する既存の通知面が唯一の適合先。
- **`Saves/backup/` と `Saves/pruned/` は `GameSystemPaths.SaveFileDirectory` 直下に置く**（agent前提）。共有契約 §8 の綴りどおり。ワールドディレクトリ配下にしなかったのは、(a) 契約が `Saves/...` と書いている、(b) 実プレイのワールドは `world_1` 固定（`.decisions/2026-08-18-セーブバックアップはworld_1固定でディレクトリ丸ごと.md`）で曖昧さが無い、(c) テスト経路の `WorldDataDirectory.FromServerDataMap` は `Root` が null で、ワールド配下にすると 427 本のテスト経路で退避先が決まらない、の3点。
- **`<ISO>` はコロン無しの基本形式 `yyyyMMdd'T'HHmmss'Z'`**（agent前提）。配布対象は Windows で、拡張ISO形式のコロンはファイル名に使えない。ファイル**中身**の `prunedAt` は読みやすさを取って拡張形式（`2026-09-13T08:30:00Z`）にする。
- **同秒の除去データは `-1`, `-2` の連番で退避する**（agent前提）。1秒以内に2度ロードする経路（テスト・再起動）で片方が黙って消えると「除去データを保持する」裁定が無音で破れる。100件埋まった場合は `Debug.LogError` を出す。
- **バックアップは既存ファイルを上書きしない**（agent前提）。「マイグレーション前の元セーブ」は最初の1本が原本であり、バックアップから戻して再実行したときに上書きすると遡り適用の起点を失う。
- **除去（pruning）は版が上がらなくても毎回のロードで走らせる**（agent前提）。マスタからの削除は `worldVersion` を上げない変更なので、版に紐づけると裁定の主目的（マスタ削除への耐性）が働かない。
- **`ISaveMigrationStep.FromVersion` は `{ get; }` プロパティにする**（agent前提）。共有契約は `int FromVersion;` と書いているが、C# の interface はフィールドを宣言できないため。意味は同じ。
- **連鎖の欠番・重複は構築時（起動時）に例外で落とす**（agent前提）。ロード時の判定にすると「その版のセーブだけが永久にロードできない」恒久封鎖が、その版のセーブを持つ人が現れるまで発覚しない。起動即失敗にすれば、形式変更PRのCI・開発者の初回起動で必ず露見する。
- **未来版セーブは新規ワールド作成へ落とさず例外で中断する**（agent前提）。落とすと autosave が古いビルドの形式で原本を上書きし、テスターのワールドが失われる。解消条件は「ゲームを更新する」で、Steam の自動配信により必ず到達する（ADR 0061）。
- **`Load(string jsonText)` のシグネチャと責務は変えない**（agent前提）。400本超のテストが直接呼ぶ入口であり、ここに前段処理を混ぜると「整った形だけを受ける」契約が壊れる。前段は `SaveLoadPreparer` に分ける。
- **plan A の形式変更そのものは実装せず、テンプレートとして書き残す**（ユーザー指示）。plan A（`docs/superpowers/plans/2026-09-11-bug-report-a-server-foundation.md` R3/R4）は未実装なので、V1→V2 ステップを本planで登録すると `CurrentVersion` だけが先に上がって形式が伴わない。完全な実装例を `.agents/skills/moorestech-save-migration/references/save-migration-step-template.md` に置き、plan A がマージされるときにコピーする。連鎖の機構自体はテスト内の疑似ステップ2本で検証する（本番コードに死んだステップを置かない）。
- **plan A R11（`scripts/save_migration/migrate_block_state_objects.py` による一括手変換）は本planの規約で置き換わる**（agent前提）。plan A を実行する時点で、R11 のPython手変換ではなく上記テンプレートのステップを書くこと。plan A 側の文面は plan A 実行セッションが本planの AGENTS.md 追記に従って読み替える。
- **`blueprints` に含まれる欠損ブロック guid は対象外**（agent前提）。ロード時には落ちず、貼り付け時に初めて問題になる。対象を広げると pruner がドメイン知識（BPの内部構造）を持つことになるため、必要になった時点で `Game.Blueprint` 側の責務として扱う。
- **保留・縮退経路の解消可能性（Self-Review 4）を明示する**（agent前提）。本planが作る「先へ進まない」経路は4つあり、それぞれ解消の主体と証拠を書き出した。(1) **未来版セーブ→起動中断**: 解消はゲーム更新（Steam 自動配信）。証拠は新しいビルドの `CurrentVersion`。最小構成として版 `Current+1`（テスト値999）と版0を両方テストで固定した。(2) **連鎖の欠番・重複→構築時例外**: 解消は同じPRでのステップ追加。証拠は `SaveMigrationChain` の構築検証で、ロード時ではなく**起動時**に落ちるため、その版のセーブを持つ人が現れる前にCI・開発者の初回起動で必ず露見する。最小構成（ステップ0本・現在版1）は「そのまま通る」側をテストで固定した。(3) **JSONとして読めない状態値→素通し**: これは保留ではなく確定した無変更で、`Debug.Log` に理由を残す。base64-MessagePack の最小構成をテストで固定した。(4) **除去件数の通知**: 解消の主体（report を埋める `SaveLoadPreparer`）と待ち受ける主体（`MissingMasterPruneNotificationWiring`）は同一プロセス・同一 `ServiceProvider` 上にあり、証拠（report）が別プロセス・別ホスト・揮発領域に置かれる構図ではない。プロセスが死ねばワールドごとロードし直しになり report も作り直される。順序の到達可能性は上の死活表に書いたとおり、リッスン開始がロードより後であることに依存しており、その依存を死活表に明記した。
- **除去データの同秒連番（`-1`, `-2`）は「決定的だから良い」で閉じない**（agent前提。Self-Review 5）。連番は選択ではなく全件保存の手段であり、後から置換・返金のマイグレーションを足すときの読み手は `Saves/pruned/` の**全ファイルを時刻順に読む**ことを前提にする。どのファイルが何番になっても下流の結果は同型（除去実体の集合が同じ）になるため、番号の割り当て規則に意味を持たせない。番号で優先順位を付ける読み手を後から作らないこと。
- **除去件数の通知は再接続のたびに出す**（agent前提）。「一度知らせる」の粒度を「接続ごとに1回」と読む。前例 `TrainFullSnapshotEventPacket` も接続のたびに押し込む。プレイヤーごとの既読を永続化するのは、そのためだけにセーブ形式を1つ上げることになり釣り合わない。

## Execution Handoff

planが完成し`docs/superpowers/plans/2026-09-13-playtest-f-save-migration-chain-and-master-removal.md`に保存されました。新規セッションを開き、以下を貼り付けて実装を開始してください:

```
subagent-driven-development スキルを使って、以下の実装planを実行してください。

- plan: docs/superpowers/plans/2026-09-13-playtest-f-save-migration-chain-and-master-removal.md
- 作業場所: feature/save-migration-chain（moores-wt new feature/save-migration-chain で使い捨てworktreeを作ってからそこで作業する）
- まずplan全文を読み、`## Requirements`・`## Global Constraints`・`## 判断記録（ADR）`を全タスク共通の制約として扱ってください
- 進捗管理はsubagent-driven-developmentスキルの規定に従ってください（SDD本体はplanのチェックボックス＋進捗台帳、単一subagent実装モードは報告ファイル＋進捗台帳が正）
- planの最終タスク（moores-code-review による全ブランチレビュー）は省略不可です
```
