# 鉱脈露頭のVeinPrefab_*シリーズ移行 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 自動生成される鉱脈の露頭ビジュアルを、旧仮プレハブ（板状メッシュ）から `AddressableResources/Environment/Vein/Item/VeinPrefab_*`（PureNature岩ベースのVariant）シリーズへ切り替える。

**Architecture:** 露頭は `OutcropGameObjectDatastore.ResolveOutcropPrefab` がマスタ `mapVeins[].outcropAddressablePath` をAddressableアドレスとして解決し、`OutcropGameObject` はロード後に `AddComponent` される。したがってコード変更はゼロで、変更はすべて (1) プレハブのリネーム (2) マスタJSONのアドレス書き換え (3) 旧プレハブ削除 (4) 回帰を止めるテスト新設、で完結する。

**Tech Stack:** Unity 6 (C#) / Addressables + SmartAddresser / NUnit (EditMode) / 外部リポジトリ `../moorestech_master`（マスタJSON） / uloop CLI

## Requirements

設計対話（grill）で確定した要件。ADR: `docs/adr/0026-vein-outcrop-uses-veinprefab-series.md`

- R1. v8マスタ（`../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json`）のItem系鉱脈8件のうち**Tungsten以外の7参照**（銅/粘土/石/青銅/小石/原木/鉄/石炭。石と小石が同じStoneを指すため参照は9件・うちTungsten 1件を除く8件）を `Vanilla/Environment/Vein/Item/VeinPrefab_*` へ書き換える。**受け入れ基準**: `map.json` をパースしたとき、Item系veinのうち `outcropAddressablePath` が `VeinPrefab_` を含まないのは `Vanilla/Environment/Vein/Item/Tungsten` の1件だけ。
- R2. Fluid系鉱脈（水鉱脈・原油鉱脈）は変更しない。**受け入れ基準**: `Vanilla/Environment/Vein/Fluid/Water` と `.../Oil` が書き換え後も現状のまま残る。
- R3. `VeinPrefab_Bronz.prefab` を `VeinPrefab_Bronze.prefab` へリネームする。**受け入れ基準**: Addressableアドレス `Vanilla/Environment/Vein/Item/VeinPrefab_Bronze` が登録済みで、`Vanilla/Environment/Vein/Item/VeinPrefab_Bronz` は登録されていない。
- R4. 旧露頭プレハブのうち **Tungsten.prefab 以外の7件**（Bronze / Clay / Coal / Copper / Iron / Stone / Tree）を削除する。**受け入れ基準**: `AddressableResources/Environment/Vein/Item/` に残る非`VeinPrefab_`プレハブは `Tungsten.prefab` のみ。
- R5. クライアント/サーバーのテストマスタ2件の `outcropAddressablePath` を新シリーズへ更新する。**受け入れ基準**: リポジトリ全体を `Vein/Item/(Stone|Copper|Iron|Clay|Coal|Bronze|Tree)"` でgrepしてヒット0件。
- R6. マスタの `outcropAddressablePath` が実在のAddressableアドレスを指していることを検証するテストを追加する。**受け入れ基準**: 存在しないアドレスへ書き換えると失敗し、正しい状態では通る。
- R7. `.moorestech-external-revisions.json` の `moorestech_master` pin を、R1の変更を含むコミットへ更新する。**受け入れ基準**: `git show HEAD:.moorestech-external-revisions.json` の commitHash が新コミットSHAと一致する。

**やらないこと（スコープ境界）:**

- `VeinPrefab_Tungsten` は作らない。Tungstenは旧 `Tungsten.prefab` を据え置く（ADR裁定）
- `VeinPrefab_Stone` にマテリアルを割り当てない。マテリアル上書き0件は意図どおり（ADR裁定）
- Fluid系露頭（Water/Oil）のビジュアルは触らない
- `OutcropGameObjectDatastore` / `OutcropGameObject` のC#は一切変更しない
- 露頭のスケール・回転・配置ロジックは変更しない（鉱脈AABBは ADR-0023 の点中心1辺3セル固定のまま）

## Global Constraints

- **作業ブランチ**: `feature/vein-outcrop-veinprefab-series`（`origin/master` から作成済み）。**本体worktree `/Users/katsumi/moorestech` 固定**。`VeinPrefabBase` は `Assets/PersonalAssets/moorestech-client-private`（PureNature Rock08/Rock11）を親に持ち、PersonalAssetsは本体worktreeにしか無いため、別worktreeでは検証不能。
- **Unity固有ファイルの手編集禁止**: `.prefab` / `.meta` を `Write`/`Edit`/`sed` で書き換えてはならない。プレハブのリネーム・削除は `uloop execute-dynamic-code` 経由の `AssetDatabase` API でのみ行う。`.meta` は手動作成しない。
- **1ファイル200行以下**、1ディレクトリ10ファイルまで。`partial` 禁止。`Func<>` 禁止。`try-catch` は外部境界（外部プロセス起動等）に限り可でその根拠をコメントに書く。
- **コメント規約**: 主要処理に日本語1行→英語1行の2行セットを約3〜10行ごと。各言語1行に収める。
- **`#region Internal`** はメソッド内ローカル関数をまとめる用途に限定。クラス直下のprivateメソッド群を囲うのは禁止。
- **コンパイル必須**: `.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client` を実行する。
- **uloopのドメインリロード待ち**: 「Unity is reloading (Domain Reload in progress)」が出たら45秒待ってリトライする。
- **外部リポジトリ**: マスタJSONは `../moorestech_master`（別gitリポジトリ）。**そこでの変更は別コミット**であり、本体リポジトリのコミットには含まれない。pin更新コミットで結び付ける。
- **Addressableアドレス生成規則**: `moorestech_client/Assets/AddressableAssetsData/LayoutRuleData.asset:98-99` の SmartAddresser ルール `^Assets/AddressableResources/(.+?)\.[^/]+$` → `Vanilla/$1`。すなわちファイル名がそのままアドレス末尾になる。`SmartAddresserAssetPostProcessor` がインポート/リネーム時に自動適用する。

---

## 機能死活表（露頭機構にぶら下がる全操作）

置換を含む計画なので、`OutcropGameObjectDatastore` / `OutcropGameObject` にぶら下がる操作を全件並べ、本plan後も生きるかを明示する。

| 操作・機能 | 計画後 | 根拠 |
|---|---|---|
| 露頭が鉱脈AABB中心に1体立つ | 生存 | アドレス差し替えのみ。`InstantiateOutcrop` の経路は不変 |
| 露頭の見た目 | **変化**（板状→岩メッシュ） | 本planの目的そのもの |
| 露頭への手掘り（レイ→`OutcropRayTarget`→`SendAttack`） | 生存 | `OutcropGameObject.Initialize` が子Colliderへ後付けする。`VeinPrefabBase` の子 Rock08/Rock11 は MeshCollider を持つ |
| 露頭のMapObjectLayer設定 | 生存 | `InstantiateOutcrop` が全子孫へ適用（プレハブ非依存） |
| 手掘り音（tree/stone） | 生存 | `MapVeinMasterElement.SoundEffectType` 由来でプレハブ非依存 |
| 鉱脈範囲プレビュー表示（`IMapVeinRangeView`） | 生存 | 露頭プレハブと無関係 |
| スキット中の露頭表示ON/OFF（`ISkitWorldObjectControl`） | 生存 | Datastore の `SetActive` でプレハブ非依存 |
| 電気マイナーによる鉱脈採掘 | 生存 | サーバー側処理で露頭ビジュアルと無関係 |
| Fluid鉱脈（水・原油）の露頭 | 生存 | 対象外（R2） |
| Tungsten鉱脈の露頭 | 生存（旧見た目のまま） | 据え置き裁定 |
| PersonalAssets非在環境での露頭**表示** | **退化**（岩が missing prefab になり見えない） | `VeinPrefabBase` のルートはmain repo内なのでオブジェクト自体は立つ。表示だけが欠ける |
| PersonalAssets非在環境での露頭**手掘りレイ** | **退化**（MeshColliderが無く`OutcropRayTarget`が付かない） | 実レイ検証は本体worktree限定になる |

退化する2行はいずれも「PersonalAssetsを持たない環境でのみ」発生し、既存のCIテスト（`MapVeinOutcropAndRangeViewTest` は個数と座標のみ検証）を落とさない。裁定済みのトレードオフ（`.decisions/2026-08-21-鉱脈露頭の旧プレハブはTungsten以外を削除する.md`）。

---

## File Structure

| ファイル | 区分 | 責務 |
|---|---|---|
| `moorestech_client/Assets/AddressableResources/Environment/Vein/Item/VeinPrefab_Bronz.prefab` | Rename→`VeinPrefab_Bronze.prefab` | 青銅鉱脈の露頭ビジュアル |
| `moorestech_client/Assets/AddressableResources/Environment/Vein/Item/{Bronze,Clay,Coal,Copper,Iron,Stone,Tree}.prefab` | Delete | 旧仮ビジュアル（板状メッシュ） |
| `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json` | Modify | v8実マスタ。露頭アドレスの正本 |
| `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/map.json` | Modify | PlayModeテスト用マスタ |
| `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/map.json` | Modify | サーバーUnitTest用マスタ |
| `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Map/MapVeinMasterTest.cs:69` | Modify | インラインJSON fixtureの露頭アドレス文字列 |
| `moorestech_client/Assets/Scripts/Client.Tests/Support/PinnedMasterRepository.cs` | Create | pin済みコミットからマスタリポジトリのファイル内容を読む共通ヘルパ |
| `moorestech_client/Assets/Scripts/Client.Tests/Map/VeinOutcropAddressableLoadTest.cs` | Create | 露頭アドレスの実在検証テスト |
| `moorestech_client/Assets/Scripts/Client.Tests/Localization/Skit/SkitLocalizationRuntimeContentTest.cs` | Modify | 上記ヘルパへ移行（重複排除） |
| `.moorestech-external-revisions.json` | Modify | moorestech_master のpin |

---

### Task 1: VeinPrefab_Bronz を VeinPrefab_Bronze へリネームする

**Files:**
- Rename: `moorestech_client/Assets/AddressableResources/Environment/Vein/Item/VeinPrefab_Bronz.prefab` → `VeinPrefab_Bronze.prefab`

**Interfaces:**
- Consumes: なし
- Produces: Addressableアドレス `Vanilla/Environment/Vein/Item/VeinPrefab_Bronze`（Task 3のテストとTask 4のマスタ更新が参照する）

- [ ] **Step 1: リネーム前のアドレス登録状態を記録する**

Unityを起動していない場合は先に起動する（`uloop-launch` スキル、または `uloop compile --project-path ./moorestech_client` で立ち上がるのを待つ）。

```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
using System.Text;
using UnityEditor.AddressableAssets;
using UnityEditor;
using UnityEngine;
var sb = new StringBuilder();
foreach (var group in AddressableAssetSettingsDefaultObject.Settings.groups) {
    if (group == null) continue;
    foreach (var entry in group.entries) {
        if (!entry.address.StartsWith("Vanilla/Environment/Vein/")) continue;
        sb.AppendLine(entry.address + " -> " + AssetDatabase.GUIDToAssetPath(entry.guid));
    }
}
Debug.Log("[VEIN-ADDRESSES]\n" + sb.ToString());
'
```

Expected: `Vanilla/Environment/Vein/Item/VeinPrefab_Bronz` を含む行が出力される。この出力を後続比較用に控える。

- [ ] **Step 2: AssetDatabase.RenameAsset でリネームする**

`.prefab` / `.meta` の直接編集は禁止。必ずこのAPI経由で行う（guidと参照が保たれる）。

```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
using UnityEditor;
using UnityEngine;
const string path = "Assets/AddressableResources/Environment/Vein/Item/VeinPrefab_Bronz.prefab";
var error = AssetDatabase.RenameAsset(path, "VeinPrefab_Bronze");
Debug.Log("[RENAME-RESULT] " + (string.IsNullOrEmpty(error) ? "OK" : error));
AssetDatabase.SaveAssets();
AssetDatabase.Refresh();
'
```

Expected: `[RENAME-RESULT] OK`

- [ ] **Step 3: アドレスが追随したことを確認する**

Step 1と同じコマンドを再実行する。

Expected: `Vanilla/Environment/Vein/Item/VeinPrefab_Bronze -> Assets/AddressableResources/Environment/Vein/Item/VeinPrefab_Bronze.prefab` が出力され、**`VeinPrefab_Bronz`（e無し）の行が消えている**。

もし旧アドレスが残っている場合は SmartAddresser の再適用が走っていないので、次を実行してからもう一度確認する:

```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
using UnityEditor;
using UnityEngine;
AssetDatabase.ImportAsset("Assets/AddressableResources/Environment/Vein/Item/VeinPrefab_Bronze.prefab", ImportAssetOptions.ForceUpdate);
AssetDatabase.SaveAssets();
Debug.Log("[REIMPORT] done");
'
```

- [ ] **Step 4: ファイル名とmetaを確認する**

Run: `ls moorestech_client/Assets/AddressableResources/Environment/Vein/Item/ | grep -i bronz`

Expected: `VeinPrefab_Bronze.prefab` と `VeinPrefab_Bronze.prefab.meta` のみ（旧 `Bronze.prefab`/`Bronze.prefab.meta` は Task 6 まで残るので、それらも一緒に出る。`VeinPrefab_Bronz.prefab`（e無し）が無いことを確認する）。

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/AddressableResources/Environment/Vein/Item/ moorestech_client/Assets/AddressableAssetsData
git commit -m "refactor(env): VeinPrefab_Bronz を VeinPrefab_Bronze へリネーム"
```

---

### Task 2: pin済みマスタを読む共通ヘルパを切り出す

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Support/PinnedMasterRepository.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Localization/Skit/SkitLocalizationRuntimeContentTest.cs`

**Interfaces:**
- Consumes: なし
- Produces: `public static class Client.Tests.Support.PinnedMasterRepository` のメンバ
  - `public static string ReadPinnedFile(string pathInMasterRepository)` — `.moorestech-external-revisions.json`（コミット済みの値）の `moorestech_master` pinコミットから、指定パスのファイル内容を文字列で返す。見つからない場合は `Assert.Fail` する

**なぜ共通化するか**: 「コミット済みpinを読む → 本体repoルートを求める → マスタrepoで `git show <pin>:<path>`」という手順は `SkitLocalizationRuntimeContentTest` に既にあり、Task 3のテストが同じ手順を必要とする。コピーすると2箇所で壊れる。

- [ ] **Step 1: ディレクトリとヘルパを作成する**

`moorestech_client/Assets/Scripts/Client.Tests/Support/PinnedMasterRepository.cs` を新規作成する（`Support` ディレクトリも新規）:

```csharp
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Support
{
    /// <summary>
    ///     コミット済みpinが指すmoorestech_masterのコミットからファイルを読む
    ///     Reads files from the moorestech_master commit named by the committed pin
    /// </summary>
    public static class PinnedMasterRepository
    {
        private const string MasterRepositoryKey = "moorestech_master";
        private const int GitTimeoutMilliseconds = 30000;

        public static string ReadPinnedFile(string pathInMasterRepository)
        {
            // 作業ツリーのピンはUnityが実チェックアウト値へ書き戻すので、コミット済みの値だけを信じる
            // Unity rewrites the working-tree pin to the resolved checkout, so only the committed value is trusted
            var repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var revisionJson = RunGit(repositoryRoot, "show HEAD:.moorestech-external-revisions.json");
            var revision = FindMasterRevision(JObject.Parse(revisionJson));

            // worktreeから起動されても本体repoの隣を見るため、共通gitディレクトリで正本を特定する
            // Locate the primary repo via the common git directory so worktrees resolve the same neighbour
            var commonGitDirectory = RunGit(repositoryRoot, "rev-parse --path-format=absolute --git-common-dir").Trim();
            var primaryRepositoryRoot = Directory.GetParent(commonGitDirectory).FullName;
            var masterRepositoryRoot = Path.GetFullPath(Path.Combine(primaryRepositoryRoot, (string)revision["relativePath"]));
            Assert.IsTrue(Directory.Exists(masterRepositoryRoot), $"Pinned master repository not found: {masterRepositoryRoot}");

            return RunGit(masterRepositoryRoot, $"show {(string)revision["commitHash"]}:{pathInMasterRepository}");
        }

        private static JObject FindMasterRevision(JObject revisionRoot)
        {
            foreach (var token in (JArray)revisionRoot["repositories"])
            {
                var revision = (JObject)token;
                if ((string)revision["key"] == MasterRepositoryKey) return revision;
            }

            Assert.Fail($"Committed external revisions do not contain {MasterRepositoryKey}");
            return null;
        }

        public static string RunGit(string workingDirectory, string arguments)
        {
            // CIコンテナはworkspace所有者が異なりdubious ownershipでexit 128になるため、プロセス限定で信頼する
            // CI containers own the workspace as another user and git exits 128 with dubious ownership, so trust it per process only
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-c safe.directory=* {arguments}",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            // 外部プロセス起動は外部境界なので、ここだけ有界時間の失敗検知に徹する
            // Launching an external process is a boundary, so this spot only bounds and reports the failure
            using var process = Process.Start(startInfo);
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(GitTimeoutMilliseconds))
            {
                process.Kill();
                Assert.Fail($"git timed out: {arguments}");
            }

            Assert.AreEqual(0, process.ExitCode, $"git failed: {arguments}\n{standardError}");
            return standardOutput;
        }
    }
}
```

- [ ] **Step 2: コンパイルを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

- [ ] **Step 3: SkitLocalizationRuntimeContentTest を新ヘルパへ移行する**

`SkitLocalizationRuntimeContentTest.cs` の以下を差し替える:

1. `using Client.Tests.Support;` を追加し、`using System.Diagnostics;` を削除する（`Process` はこのファイルから消えるため）
2. pin解決〜`git show` を行っていたブロック（`var revisionJson = RunGit(...)` から `var csvText = RunGit(masterRepositoryRoot, $"show ...localization.csv");` まで）を、次の1行に置き換える:

```csharp
            var csvText = PinnedMasterRepository.ReadPinnedFile("server_v8/mods/moorestechAlphaMod_8/localization/localization.csv");
```

3. このクラスの `private static JObject FindMasterRevision(JObject revisionRoot)` と `private static string RunGit(string workingDirectory, string arguments)` を丸ごと削除する
4. **`using Newtonsoft.Json.Linq;` と `using System;` は削除しない。** 前者は `AddressableSkitRuntimeValuesExcludeQaSentinels`（同ファイル L31 の `JObject.Parse`）が、後者は同 L37 の `StringComparison.Ordinal` が引き続き使う

- [ ] **Step 4: コンパイルとskitテストを確認する**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SkitLocalizationRuntimeContentTest"
```

Expected: コンパイル errors 0、テスト PASS（移行前と同じ結果）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/Support moorestech_client/Assets/Scripts/Client.Tests/Localization/Skit/SkitLocalizationRuntimeContentTest.cs
git commit -m "refactor(test): pin済みmasterのファイル読み出しをPinnedMasterRepositoryへ集約"
```

---

### Task 3: 露頭アドレスの実在を検証するテストを追加する（red）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Map/VeinOutcropAddressableLoadTest.cs`
- Test: 同上

**Interfaces:**
- Consumes: `Client.Tests.Support.PinnedMasterRepository.ReadPinnedFile(string)`（Task 2）
- Produces: なし（テストのみ）

**前例**: `moorestech_client/Assets/Scripts/Client.Tests/Map/MapObjectAddressableLoadTest.cs` — Addressable登録の全件検証を `AddressableAssetSettingsDefaultObject.Settings` から行う形。同じ形に揃える。

**`IgnoreCI` は付けない。** 理由は2つ:
1. CIは `.moorestech-external-revisions.json` のpinで `moorestech_master` をcheckoutする（`.github/workflows/run_test.yml:69,84-89`）ので、pin済みmap.jsonは読める
2. `VeinPrefabBase.prefab:3-19` はルートGameObject・Transform・LODGroupをmain repo内に自前で持ち、PersonalAssetsから来るのは子のRock08/Rock11 PrefabInstanceだけ。よってPersonalAssets非在でも `AssetDatabase.LoadAssetAtPath<GameObject>` は非nullを返す

**万一CIで `outcrop prefab does not exist` として落ちた場合**（=上記2の想定が外れた場合）は、`[Category("IgnoreCI")]` を付けて `.decisions/2026-08-19-有料アセット依存テストはIgnoreCIでCIから外す.md` の方針に合わせる。憶測で先回りして付けない。

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Map/VeinOutcropAddressableLoadTest.cs`:

```csharp
using System;
using System.Collections.Generic;
using Client.Tests.Support;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Client.Tests.Map
{
    /// <summary>
    ///     v8マスタの全鉱脈について露頭アドレスが実在することを検証
    ///     Verifies every vein in the v8 master resolves to a real outcrop address
    /// </summary>
    public class VeinOutcropAddressableLoadTest
    {
        private const string MapJsonPath = "server_v8/mods/moorestechAlphaMod_8/master/map.json";
        private const string ItemVeinAddressPrefix = "Vanilla/Environment/Vein/Item/";

        // VeinPrefab_Tungstenが未作成のためTungstenだけ旧プレハブを据え置く（ADR-0026）
        // Tungsten alone keeps the legacy prefab because VeinPrefab_Tungsten does not exist yet (ADR-0026)
        private static readonly string[] LegacyItemAddressAllowList = { "Vanilla/Environment/Vein/Item/Tungsten" };

        [Test]
        public void 全鉱脈の露頭アドレスがAddressablesに登録されている()
        {
            var assetPathByAddress = CollectAddressableAssetPaths();

            foreach (var vein in LoadVeins())
            {
                var address = (string)vein["outcropAddressablePath"];
                var veinName = (string)vein["veinName"];
                Assert.IsTrue(assetPathByAddress.TryGetValue(address, out var assetPath), $"address is not registered to Addressables: {address} (vein: {veinName})");

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                Assert.IsNotNull(prefab, $"outcrop prefab does not exist: {assetPath} (vein: {veinName})");
            }
        }

        [Test]
        public void Item系鉱脈の露頭がVeinPrefabシリーズを指している()
        {
            foreach (var vein in LoadVeins())
            {
                var address = (string)vein["outcropAddressablePath"];
                var veinName = (string)vein["veinName"];
                if (!address.StartsWith(ItemVeinAddressPrefix, StringComparison.Ordinal)) continue;
                if (Array.IndexOf(LegacyItemAddressAllowList, address) >= 0) continue;

                StringAssert.StartsWith($"{ItemVeinAddressPrefix}VeinPrefab_", address, $"item vein still points at a legacy outcrop: {veinName}");
            }
        }

        private static List<JObject> LoadVeins()
        {
            var mapJson = JObject.Parse(PinnedMasterRepository.ReadPinnedFile(MapJsonPath));
            var veins = new List<JObject>();
            foreach (var token in (JArray)mapJson["mapVeins"]) veins.Add((JObject)token);

            Assert.IsNotEmpty(veins, "mapVeins is empty; the test would pass vacuously");
            return veins;
        }

        private static Dictionary<string, string> CollectAddressableAssetPaths()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.IsNotNull(settings, "AddressableAssetSettings is missing");

            var assetPathByAddress = new Dictionary<string, string>();
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                foreach (var entry in group.entries) assetPathByAddress[entry.address] = AssetDatabase.GUIDToAssetPath(entry.guid);
            }

            return assetPathByAddress;
        }
    }
}
```

- [ ] **Step 2: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

- [ ] **Step 3: テストを実行して片方が失敗することを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "VeinOutcropAddressableLoadTest"`

Expected:
- `全鉱脈の露頭アドレスがAddressablesに登録されている` は **PASS**（旧プレハブがまだ存在するため）
- `Item系鉱脈の露頭がVeinPrefabシリーズを指している` は **FAIL**、メッセージ `item vein still points at a legacy outcrop: 銅の鉱石鉱脈`（マスタがまだ旧パスを指しているため）

このredがTask 4で解消される。

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/Map/VeinOutcropAddressableLoadTest.cs
git commit -m "test(map): 鉱脈露頭のAddressableアドレス実在検証を追加"
```

---

### Task 4: v8マスタの露頭アドレスを新シリーズへ切り替える（green）

**Files:**
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json`（**別リポジトリ**）
- Modify: `.moorestech-external-revisions.json`

**Interfaces:**
- Consumes: Task 1で確定したアドレス `Vanilla/Environment/Vein/Item/VeinPrefab_Bronze`
- Produces: 新pinコミットSHA（`.moorestech-external-revisions.json` に載る）

**なぜ共有チェックアウトで作業するか**: Unityは作業ツリーの `.moorestech-external-revisions.json` を「`../moorestech_master` の実HEAD」へ常時書き戻す（`unity-playmode-recorded-playtest/scripts/preflight.sh:17-19` のコメント参照）。したがって新pinを保つには**共有チェックアウト `/Users/katsumi/moorestech_master` 自身のHEADを新コミットへ進める**必要がある。旧pin `fc6aa33` を必要とする他ブランチのために、先に旧pinのworktreeを1本用意して退避させる。

- [ ] **Step 1: 旧pinを退避するworktreeを作る**

```bash
git -C ../moorestech_master worktree add /Users/katsumi/moorestech-master-worktrees/objectconfig-parity-pin fc6aa33e64dd9b1e1c8ede0a71b19465031caafd --detach
git -C ../moorestech_master worktree list
```

Expected: 新しい行 `/Users/katsumi/moorestech-master-worktrees/objectconfig-parity-pin  fc6aa33 (detached HEAD)` が出る。

- [ ] **Step 2: 共有チェックアウトで作業ブランチを切る**

```bash
git -C ../moorestech_master status --short
git -C ../moorestech_master checkout -b feat/vein-outcrop-veinprefab-series
git -C ../moorestech_master branch --show-current
```

Expected: `status --short` が空（未コミット変更なし）、`branch --show-current` が `feat/vein-outcrop-veinprefab-series`。
**status が空でなければ止めてユーザーに報告する**（他作業の変更を巻き込む）。

- [ ] **Step 3: map.json の Item系露頭アドレスを書き換える**

`map.json` は純粋なJSONデータ（Unity固有ファイルではない）ので、スクリプトでの一括置換が正規ルート。

```bash
python3 - <<'EOF'
import json, collections
p = "../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json"
with open(p, encoding="utf-8") as f:
    data = json.load(f, object_pairs_hook=collections.OrderedDict)

# Tungstenは対応Variant未作成のため据え置く（ADR-0026）
# Tungsten stays on the legacy prefab because its variant does not exist yet (ADR-0026)
mapping = {
    "Vanilla/Environment/Vein/Item/Copper": "Vanilla/Environment/Vein/Item/VeinPrefab_Copper",
    "Vanilla/Environment/Vein/Item/Clay":   "Vanilla/Environment/Vein/Item/VeinPrefab_Clay",
    "Vanilla/Environment/Vein/Item/Stone":  "Vanilla/Environment/Vein/Item/VeinPrefab_Stone",
    "Vanilla/Environment/Vein/Item/Bronze": "Vanilla/Environment/Vein/Item/VeinPrefab_Bronze",
    "Vanilla/Environment/Vein/Item/Tree":   "Vanilla/Environment/Vein/Item/VeinPrefab_Tree",
    "Vanilla/Environment/Vein/Item/Iron":   "Vanilla/Environment/Vein/Item/VeinPrefab_Iron",
    "Vanilla/Environment/Vein/Item/Coal":   "Vanilla/Environment/Vein/Item/VeinPrefab_Coal",
}
changed = 0
for vein in data["mapVeins"]:
    new = mapping.get(vein["outcropAddressablePath"])
    if new:
        vein["outcropAddressablePath"] = new
        changed += 1

with open(p, "w", encoding="utf-8") as f:
    json.dump(data, f, ensure_ascii=False, indent=2)
    f.write("\n")
print("changed:", changed)
EOF
```

Expected: `changed: 8`（銅・粘土・石・青銅・小石・原木・鉄・石炭。石と小石が同じStoneを指すので参照は8件）

- [ ] **Step 4: 差分が意図どおりか確認する**

```bash
git -C ../moorestech_master diff --stat
git -C ../moorestech_master diff -U0 -- server_v8/mods/moorestechAlphaMod_8/master/map.json | grep -E "^[+-].*outcropAddressablePath"
```

Expected: `outcropAddressablePath` の行だけが8対（-/+）現れる。**他のキーの行が差分に出ていたら止める**（`json.dump` の整形が既存フォーマットと違うと全行差分になる）。全行差分になった場合はワーキングツリーを戻して、`sed` による行単位置換に切り替える:

```bash
git -C ../moorestech_master checkout -- server_v8/mods/moorestechAlphaMod_8/master/map.json
cd ../moorestech_master
for pair in "Copper" "Clay" "Stone" "Tree" "Iron" "Coal"; do
  sed -i '' "s|\"Vanilla/Environment/Vein/Item/${pair}\"|\"Vanilla/Environment/Vein/Item/VeinPrefab_${pair}\"|g" server_v8/mods/moorestechAlphaMod_8/master/map.json
done
sed -i '' "s|\"Vanilla/Environment/Vein/Item/Bronze\"|\"Vanilla/Environment/Vein/Item/VeinPrefab_Bronze\"|g" server_v8/mods/moorestechAlphaMod_8/master/map.json
cd -
```

- [ ] **Step 5: Tungsten と Fluid が無傷であることを確認する**

```bash
python3 -c "
import json
d=json.load(open('../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json'))
for v in d['mapVeins']: print(v['veinName'], v['outcropAddressablePath'])
"
```

Expected: タングステン鉱石鉱脈が `Vanilla/Environment/Vein/Item/Tungsten`、水鉱脈が `Vanilla/Environment/Vein/Fluid/Water`、原油鉱脈が `Vanilla/Environment/Vein/Fluid/Oil` のまま。他8件が `VeinPrefab_` を含む。

- [ ] **Step 6: マスタリポジトリでコミットしSHAを控える**

```bash
git -C ../moorestech_master add server_v8/mods/moorestechAlphaMod_8/master/map.json
git -C ../moorestech_master commit -m "feat(v8): 鉱脈露頭をVeinPrefab_*シリーズへ切り替える(Tungstenは据え置き)"
git -C ../moorestech_master rev-parse HEAD
```

Expected: 40桁のSHAが出力される。これを次のStepで使う。

- [ ] **Step 7: pin を更新する**

`.moorestech-external-revisions.json` の `moorestech_master` の `commitHash` を Step 6 のSHAへ書き換える（`moorestech_client_private` の側は触らない）。

```bash
NEW_SHA=$(git -C ../moorestech_master rev-parse HEAD)
python3 - "$NEW_SHA" <<'EOF'
import json, sys
sha = sys.argv[1]
p = ".moorestech-external-revisions.json"
with open(p) as f: data = json.load(f)
for repo in data["repositories"]:
    if repo["key"] == "moorestech_master": repo["commitHash"] = sha
with open(p, "w") as f:
    json.dump(data, f, indent=4)
    f.write("\n")
print("pinned:", sha)
EOF
git diff -- .moorestech-external-revisions.json
```

Expected: `commitHash` の1行だけが変わる。

- [ ] **Step 8: pin更新をコミットする**

テストは `git show HEAD:.moorestech-external-revisions.json`（**コミット済みの値**）を読むため、検証より先にコミットする必要がある。

```bash
git add .moorestech-external-revisions.json
git commit -m "chore(master): 露頭VeinPrefab_*切り替えを含むmasterへpinを更新"
git show HEAD:.moorestech-external-revisions.json | grep -A1 moorestech_master
```

Expected: 表示される `commitHash` が Step 6 のSHAと一致する。

- [ ] **Step 9: テストを実行してgreenになることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "VeinOutcropAddressableLoadTest"`

Expected: 2テストとも **PASS**。Task 3 Step 3 で FAIL していた `Item系鉱脈の露頭がVeinPrefabシリーズを指している` が通ることが、このタスクの完了条件。

---

### Task 5: テストマスタとfixtureの露頭アドレスを更新する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/map.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/map.json`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Map/MapVeinMasterTest.cs:69`

**Interfaces:**
- Consumes: Task 1で確定したアドレス群
- Produces: なし

**なぜfixtureまで直すか**: Task 6で旧プレハブを削除するため、これらの文字列は存在しないアセットを指す残骸になる。`MapVeinMasterTest` はアドレスを解決しないのでテストは通り続けるが、grepでの追跡性が壊れる。

- [ ] **Step 1: 現在の参照箇所を洗い出す**

```bash
grep -rn --include="*.json" --include="*.cs" -E '"Vanilla/Environment/Vein/Item/(Stone|Copper|Iron|Clay|Coal|Bronze|Tree)"' . | grep -v "^./docs" | grep -v "^./.decisions"
```

Expected: 以下7箇所が出る
- `Client.Tests/.../EditModeInPlayingTestMod/master/map.json` の Stone ×2 / Copper ×1 / Tree ×1
- `Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/map.json` の Iron ×1 / Stone ×1
- `Tests/UnitTest/Core/Map/MapVeinMasterTest.cs:69` の Stone ×1

- [ ] **Step 2: 一括置換する**

```bash
for f in \
  moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/map.json \
  moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/map.json \
  moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Map/MapVeinMasterTest.cs ; do
  sed -i '' -E 's|Vanilla/Environment/Vein/Item/(Stone\|Copper\|Iron\|Clay\|Coal\|Tree)|Vanilla/Environment/Vein/Item/VeinPrefab_\1|g' "$f"
  sed -i '' 's|Vanilla/Environment/Vein/Item/Bronze|Vanilla/Environment/Vein/Item/VeinPrefab_Bronze|g' "$f"
done
```

- [ ] **Step 3: 残骸が無いことを確認する**

Run: Step 1 と同じ grep コマンド
Expected: **ヒット0件**（`Tungsten` は置換対象外だが、これら3ファイルにTungstenは元々出てこない）

- [ ] **Step 4: コンパイルとテストを実行する**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapVeinMasterTest"
```

Expected: コンパイル errors 0、`MapVeinMasterTest` 全件 PASS

- [ ] **Step 5: 露頭を使うPlayModeテストを実行する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Outcrop|VeinHandMining"`

Expected: 全件 PASS（該当テストが0件なら「0 tests」で問題なし）。「Unity is reloading」が出たら45秒待ってリトライする。

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/map.json moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/map.json moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Map/MapVeinMasterTest.cs
git commit -m "test(map): テストマスタの露頭アドレスをVeinPrefab_*シリーズへ更新"
```

---

### Task 6: 旧露頭プレハブ7件を削除する

**Files:**
- Delete: `moorestech_client/Assets/AddressableResources/Environment/Vein/Item/{Bronze,Clay,Coal,Copper,Iron,Stone,Tree}.prefab`（および対応する `.meta`）

**Interfaces:**
- Consumes: Task 4・Task 5で全参照が新シリーズへ移っていること
- Produces: なし

- [ ] **Step 1: 参照が残っていないことを最終確認する**

```bash
grep -rn --include="*.json" --include="*.cs" --include="*.prefab" --include="*.unity" -E 'Vein/Item/(Stone|Copper|Iron|Clay|Coal|Bronze|Tree)"' . ../moorestech_master | grep -v "^./docs" | grep -v "^./.decisions"
```

Expected: **ヒット0件**。1件でも出たらその参照を先に直す。

- [ ] **Step 2: AssetDatabase.DeleteAsset で削除する**

`.prefab`/`.meta` の直接 `rm` は禁止（`.meta` が取り残される）。必ずこのAPI経由で行う。

```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
using UnityEditor;
using UnityEngine;
var names = new[] { "Bronze", "Clay", "Coal", "Copper", "Iron", "Stone", "Tree" };
foreach (var name in names) {
    var path = "Assets/AddressableResources/Environment/Vein/Item/" + name + ".prefab";
    Debug.Log("[DELETE] " + path + " -> " + AssetDatabase.DeleteAsset(path));
}
AssetDatabase.SaveAssets();
AssetDatabase.Refresh();
'
```

Expected: 7行すべて `-> True`

- [ ] **Step 3: ディレクトリの中身を確認する**

Run: `ls moorestech_client/Assets/AddressableResources/Environment/Vein/Item/ | grep -v meta`

Expected: 以下9件のみ
```
Tungsten.prefab
VeinPrefab_Bronze.prefab
VeinPrefab_Clay.prefab
VeinPrefab_Coal.prefab
VeinPrefab_Copper.prefab
VeinPrefab_Iron.prefab
VeinPrefab_Stone.prefab
VeinPrefab_Tree.prefab
VeinPrefabBase.prefab
```

- [ ] **Step 4: Addressableエントリが消えたことを確認する**

Task 1 Step 1 と同じ `[VEIN-ADDRESSES]` ダンプコマンドを実行する。

Expected: `Vanilla/Environment/Vein/Item/` 配下は `Tungsten` と `VeinPrefab_*` と `VeinPrefabBase` のみ。旧7件のアドレスが消えている。

- [ ] **Step 5: テストを実行する**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "VeinOutcropAddressableLoadTest|MapVeinMasterTest|MapObjectAddressableLoadTest"
```

Expected: 全件 PASS

- [ ] **Step 6: コミットする**

```bash
git add -A moorestech_client/Assets/AddressableResources/Environment/Vein/Item moorestech_client/Assets/AddressableAssetsData
git commit -m "chore(env): 旧露頭プレハブ7件を削除(Tungstenのみ据え置き)"
```

---

### Task 7: 実プレイで露頭の見た目と手掘りを確認する

**Files:**
- 変更なし（検証のみ）

**Interfaces:**
- Consumes: Task 1〜6の全変更
- Produces: 録画動画とスクリーンショット（レビュー添付用）

**なぜ必要か**: 露頭の当たり判定が `BoxCollider`（板状 3×0.28×3）から `MeshCollider`（岩メッシュ）へ変わり、鉱脈AABB（点中心1辺3セル固定・ADR-0023）に対して岩の実寸が過大／過小になっていないかは自動テストでは判定できない。また `VeinPrefab_Stone` はマテリアル未指定なので、素の岩肌が他鉱石と混同されないかを目視する必要がある。

- [ ] **Step 1: プレイテストDSLのpreflightを通す**

`unity-playmode-recorded-playtest` スキルを起動し、その手順に従う。preflight は `.moorestech-external-revisions.json` のコミット済みpinとHEADが一致する `moorestech_master` worktree を探すので、Task 4 で共有チェックアウトのHEADを新コミットへ進めてあれば自動解決される。

Run: `uloop compile --project-path ./moorestech_client`（先にコンパイル通過を確認）
Expected: errors 0

- [ ] **Step 2: 露頭が見えるシナリオを録画実行する**

`unity-playmode-recorded-playtest` の `scripts/run-scenario.sh` で、スポーン地点から周囲を見回して露頭へ近づき、手掘りを1回行うシナリオを実行する。既存の `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/vein-hand-mining-smoke.cs` が露頭手掘りのシナリオなので、これを起点に使う。

Expected: `result.json` が成功で返り、録画動画が出力される。

- [ ] **Step 3: ログにロード失敗が無いことを確認する**

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`

Expected: `[OutcropGameObjectDatastore] 露頭プレハブをロードできません` と `Addressables Load Error` が **0件**。1件でも出たらそのアドレスがTask 4/5の書き換え漏れ。

- [ ] **Step 4: 録画を目視して3点を判定する**

1. 露頭が岩メッシュとして表示されている（板状の旧ビジュアルではない）
2. 岩のサイズが鉱脈AABB（1辺3セル）に対して極端に大きすぎ／小さすぎない
3. 手掘りのレイが岩に当たり、採掘が成立する

**問題があればユーザーへ報告して裁定を仰ぐ**（スケール調整は本planのスコープ外であり、独断で `VeinPrefabBase` を変更しない）。

- [ ] **Step 5: 録画とスクリーンショットをユーザーへ提示する**

`SendUserFile` で録画動画を送る。

---

### Task 8: 全ブランチレビューを実行する

**Files:**
- 変更なし（レビューのみ）

**Interfaces:**
- Consumes: Task 1〜7の全コミット
- Produces: レビュー指摘への対応コミット（あれば）

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

**このタスクは省略不可。** `moores-code-review` スキルを起動し、`feature/vein-outcrop-veinprefab-series` の `origin/master` からの全差分をレビューする。ゴール文言や「変更が小さいから」を理由に省略してはならない。

- [ ] **Step 2: 指摘へ対応する**

機械的修正は適用し、設計判断が必要な指摘はユーザーへ `AskUserQuestion` で諮る。

- [ ] **Step 3: 対応をコミットする**

```bash
git add -A
git commit -m "fix: moores-code-review 指摘に対応"
```

- [ ] **Step 4: 未コミットの作業が残っていないことを確認する**

Run: `git status --short`
Expected: 空（`.meta` の取り残しや未追跡ファイルが無い）

---

## 判断記録（ADR）

**設計ADR**: [docs/adr/0026-vein-outcrop-uses-veinprefab-series.md](../../adr/0026-vein-outcrop-uses-veinprefab-series.md)

**裁定台帳**:
- `.decisions/2026-08-21-Tungstenの露頭だけ旧プレハブを据え置く.md`
- `.decisions/2026-08-21-鉱脈露頭の旧プレハブはTungsten以外を削除する.md`
- 参照: `.decisions/2026-08-19-有料アセット依存テストはIgnoreCIでCIから外す.md`（Task 3 のテストがCIで落ちた場合の退避先）
- 参照: `.decisions/2026-08-15-MapObjects新必須キーの一括投入はserver_v8のみに絞る.md`（マスタリポジトリのブランチ運用とpin worktreeの前例）

**planning中に生じた判断:**

- **作業ブランチは `origin/master` から切った `feature/vein-outcrop-veinprefab-series`、本体worktree固定。**
  出所: ユーザー裁定 2026-08-21 → 選択「origin/master から新規 feature ブランチ」。本体worktree固定はPersonalAssets在処からの必然（agent前提）
- **マスタリポジトリの変更は共有チェックアウト `/Users/katsumi/moorestech_master` 上の新ブランチで行い、旧pin `fc6aa33` は退避worktreeへ逃がす。**
  出所: agent前提。`preflight.sh:17-19` が明記する「Unityが作業ツリーのピンを実チェックアウト値へ書き戻す」挙動により、新pinを保つには共有チェックアウトのHEADを進める必要がある。`.decisions/2026-08-15-MapObjects新必須キーの一括投入はserver_v8のみに絞る.md` は逆配置（新pinを専用worktreeへ）だが、あちらはUnityの書き戻しに触れておらず、本件は書き戻しと整合する向きを採る
- **露頭アドレスの実在検証テストを新設する（Task 3）。**
  出所: agent前提。現行の `MapVeinMasterUtil.OutcropAddressablePathValidation` は空文字しか弾かず、綴り違い（`Bronz` / `Bronze`）はランタイムの `Debug.LogError` にしかならない。前例は `Client.Tests/Map/MapObjectAddressableLoadTest.cs`（同じ役割＝Addressable登録の全件検証）
- **`PinnedMasterRepository` を新設し `SkitLocalizationRuntimeContentTest` を移行する（Task 2）。**
  出所: agent前提。Task 3のテストが同じpin解決手順を必要とするため、コピーではなく共通化を選んだ。前例が1件しかない段階での抽象化だが、2件目を書く時点が抽象化の適切な時期である
- **`VeinPrefab_Stone` のマテリアル未指定は「意図どおり」としてplan外に置く。**
  出所: ユーザー裁定 2026-08-21 → 選択「意図通り（石＝ベースの岩肌）」

**新規パターン（レビュー注目点）:**

- テストからマスタリポジトリの**pin済みコミット**を `git show` で読む形は `SkitLocalizationRuntimeContentTest` が唯一の前例であり、本planでそれを共通ヘルパへ昇格させる。ヘルパの置き場所 `Client.Tests/Support/` は新設ディレクトリ
- 露頭の**見た目**はPersonalAssets依存になるが、`VeinPrefabBase` のルートはmain repo内にあるため、PersonalAssets非在環境でもロードとInstantiateは成立し子の岩が欠けるだけである。既存の `MapVeinOutcropAndRangeViewTest`（個数と座標のみ検証）はCIで通り続ける。**この想定はCIの実行結果で確認すること**（Task 8のレビュー時にCI結果を見る）
- PersonalAssets非在環境では岩のMeshColliderが存在せず `OutcropRayTarget` が付かないため、露頭への手掘りレイの実検証は本体worktree（Task 7）でのみ有効
