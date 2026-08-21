# Task 14 レポート: ADR-0007 vein手掘り最終レビュー

## ステータス

Task 14 の必須 `moores-code-review` と全是正を完了した。最終判定は **Approved（Critical 0 / Important 0 / Minor 0）**。設計判断の保留と免責で消した指摘は0件である。

## レビュー対象

| 項目 | 値 |
|---|---|
| branch | `feature/vein-hand-mining` |
| master merge-base | `a32bd94687d50b3ba4e4c6d084b6276978e96b91` |
| 最終production修正 | `da3f13a9c93b3310feab9d8b619e8c9d2062ff3c` |
| 証跡可視化 | `26bf2b4845fd1a7a63c79b90b83cd928a77ee110` と本Task 14最終コミット |
| 外部master head | `094d242be9509565393efc5aad5b467bda247222` |

## 適用した是正

1. 採掘FSMに開始対象を保持し、照準変更でFocusへ戻す。完了送信も開始対象へ固定する。
2. veinマスタの非正 `attackSpeed` と同一vein内の重複 `ToolItemGuid` を拒否する。
3. `[Inject] Construct` を `Initialize` に統一し、単一呼び出しhelperをローカル関数へ移す。
4. 比較方向、不要なfloat cast、コメント長、未使用メンバーを規約へ揃える。
5. mutation testで完了時の誤送信を実際にRED化し、開始対象だけが攻撃される契約を固定する。
6. recorded smokeに本番focus一致後だけ表示するCollider輪郭を追加し、失敗時も入力・GameObject・Materialを `finally` で解除する。

## レビュー結果

- 12レンズ、17 reviewer、Fable、比較演算子verifier、DeadMemberAudit、18分割investigatorを統合した。
- 初回8ブロッカー群と再レビュー4指摘を是正し、最終再レビューは Approved（Critical 0 / Important 0 / Minor 0）。
- 外部Codex監査は10分でタイムアウトし、確定した追加指摘は無い。未実行として扱い、他の独立系統で補完した。
- DeadMemberAudit全体実行はMono.Cecilのstack overflowで縮退した。Client.Tests除外実行の33候補は個別照合し、残存blockerは0件だった。
- コメント候補18件はすべてload-bearing rationaleとして維持し、最新smokeコメント1件だけを機械短縮した。
- suppressed: 0件。

### Warning

- `MainGameStarter.cs` は既存374行で、今回差分による増加ではない。
- `ChallengeMasterUtil.cs` は既存395行で、今回差分による増加ではない。
- `MoorestechServerDIContainerGenerator.cs` は既存309行で、今回差分による増加ではない。
- `Server.Protocol/PacketResponse` はmasterと同じ51ファイルで、branch-neutralな既存配置である。

### Info

- `VanillaSchema/map.yml` の `optional: true` は、`none` unionで値が存在しないこと自体を表す裁定済みの正当なabsenceである。
- Unity起動時のBush 2ログは既存BrokenPrefabAsset、小石5ログは生成完了前にpinが検索する既存の一時ログで、ADR-0007差分由来ではない。scenario計測区間の `ErrorLogs` は0件。

## 検証

| 対象 | 結果 |
|---|---|
| 最終compile | Error 0 / Warning 0 |
| `MapObjectMiningEquipmentSwitchTest\|MapVeinMasterTest` | 14/14 PASS |
| mining/map広域regex | 137/137 PASS |
| `EditModeInPlayingTest` | 16/16 PASS |
| mutation RED | 2/3 PASS・開始対象assert 1件が意図どおりFAIL |
| mutation復元後GREEN | 14/14 PASS |
| cleanup修正後 recorded smoke | `PlaytestResults/20260805_024138/vein-hand-mining-smoke`、28/28 PASS、Addressables 11、露頭1772、ErrorLogs 0 |

recorded smokeでは実際のLMB保持と進捗、正面・45度の本番focus、`va:mining`応答による石x1増加を録画した。最終コミット後にも同一シナリオを再収録し、公開証跡はその新しい実行を使う。

## Beads

`bd create` は共有埋め込みDolt DBに `issue_prefix` が無いため失敗した。bootstrapはDB名衝突、再初期化はhookにより承認待ちとなるため、DBを変更せずSDD台帳へ記録した。


---

# Task 14 報告: placementNoiseテクスチャノイズ源の復元（R10 / 移植漏れ③）

BASE: `8c7089caa` / worktree: `/Users/katsumi/moorestech-worktrees/map-autogen-5x5`

**ステータス: DONE_WITH_CONCERNS**（懸念は §8。特に §5-1 の「移植元との半テクセル差」は裁定に出す価値がある）

---

## 1. 何をどう実装したか

移植元 MM が `PlacementNoise.texture`（`Texture2D` 参照）で持っていた「テクスチャを直接ノイズ源にする」機構を、
サーバー側でも成立する形（**サーバーデータディレクトリ相対の PNG パス**）で復元した。関わる箇所は5つ、いずれも移植元に1:1で対応する。

| 移植元 | moorestech | 内容 |
|---|---|---|
| `ManagedNoise.cs:139` | `ManagedNoise.SamplePlacementNoise` | `noiseType == None && texture == null` の早期 1f |
| `ManagedNoise.cs:145-151` | 同上 + `GetPixelBilinear`/`GetPixel`/`SampleTextureChannel` | テクスチャ分岐（UV = world/terrainSize → バイリニア → channel 抽出） |
| `TreePlacementGenerator.cs:335` | `TreePlacementEntry.cs:69` | `clusterNoise` のクラスタ判定ガードに `|| texture != null` |
| `TreePlacementGenerator.cs:339` | `TreePlacementEntry.cs:73` | `clusterNoise2` の同上 |
| `TreePlacementGenerator.cs:549` | `TreePlacementCommon.SampleFilterNoise` | フィルタノイズの `noiseType == None && texture == null` で 0f |

MM 側の `texture != null` / `texture == null` は**全5箇所**（`grep -rn "texture != null\|texture == null" TmpUnityPjt/MapMaking/Assets/MapGenerator/`）で、そのすべてを写した。取りこぼしは無い。

### (a) スキーマとマスタ

- `VanillaSchema/mapGenerate/placementNoise.yml` に `texturePngPath`（`type: string` / `default: ""`）を**末尾プロパティ**として追加（生成コンストラクタの既存引数順を崩さないため）
- ヘッダのコメントも「texture は未使用なので削除した」→「Texture2D 参照をサーバーデータ相対の PNG パスへ置換した」に書き換え
- `mooresmaster/mooresmaster.SandBox/schema/ref/placementNoise.yml` も**同内容へ同期**した（着手時点で VanillaSchema 版と byte-identical だったため、片方だけ置いていくとドリフトする）
- `optional: true` は使っていない。実データ側へ `"texturePngPath": ""` を180箇所一括投入（§3）

### (b) 実行時型

- 新規 `Pipeline/Config/Placement/TextureChannel.cs` = `enum TextureChannel { R, G, B, A }`（移植元 `Config/TextureChannel.cs` の写経）
- `PlacementNoise` に5フィールド追加
  - マスタ由来: `string texturePngPath` / `TextureChannel channel`
  - 実行時展開: `Color32[] texturePixels` / `int textureWidth` / `int textureHeight`（`texturePixels == null` が「テクスチャ源なし」の唯一の表明）
- `PlacementRefConvert.ToPlacementNoise` が `texturePngPath` と `channel` を写す（`channel` はスキーマ既存キーだったが実行時へ渡っていなかった）
- `RuntimeConvert.ToTextureChannel` を追加（既存 `ParseEnum` に乗せた。不正名は例外）

### (c) PNG の解決（新規 `Pipeline/Runtime/PlacementNoiseTextureResolver.cs`, 67行）

`MapGenerationPipeline.Generate(selected, seed, serverDataDirectory)` が生成器を呼ぶ**直前**に一度だけ走る。

- `BiomePlacementHelper` 経由で全 `BiomeType` の `TreePlacementConfig.prototypes` を舐め、各プロトタイプの
  **4つの `PlacementNoise` すべて**（`clusterNoise` / `clusterNoise2` / `slopeFilter.noise` / `curvatureFilter.noise`）を解決する
- 空文字は読み込まない（`texturePixels` は null のまま）
- パスは `Path.Combine(serverDataDirectory, texturePngPath)`。ファイルが無い / PNG としてデコードできない場合は
  **フォールバックせず `InvalidOperationException`**（マスタ不備を無言で「テクスチャ無し」に化けさせない）
- `Texture2D` は `GetPixels32()` した直後に `DestroyImmediate` で捨て、以後の配置処理はマネージド配列だけを見る

`PlacementNoise` は struct だが `TreePrototypeEntry` は class なので、`ref entry.clusterNoise` / `ref entry.slopeFilter.noise` の
**フィールド参照渡し**で書き戻している（コピーの取り回しが要らない）。

### (d) 呼び出し側

- `MapGenerationPipeline.Generate` に第3引数 `string serverDataDirectory` を追加（**デフォルト引数は付けていない**）
- `WorldProvisioner.BuildGenerated` は `settings.ServerDataDirectory` を渡す
- テスト側13箇所は `TestGenerationConfigFactory.ServerDataDirectory`（= `TestModDirectory.ForUnitTestModDirectory`）を渡すよう更新

### (e) InternalsVisibleTo

`moorestech_server/Assets/Scripts/Game.MapGeneration/AssemblyInfo.cs` を新設し `[assembly: InternalsVisibleTo("Server.Tests")]` を置いた。
`TreePlacementCommon` / `TreePlacementEntry` は internal のままで、ガードをテストから直接検証するため（public 化はしない）。
クライアント側に同形の前例がある（`Client.Playtest/AssemblyInfo.cs`, `Client.Localization/AssemblyInfo.cs`）。

> **【Fix ラウンド1 で撤回】** この IVT はレビュー指摘 I-1 で削除した。テストは公開経路
> `TreePlacementGenerator.GenerateForBiome` 経由へ書き換えてある。詳細は末尾の「Fix ラウンド1」参照。

---

## 2. スキーマ変更と SourceGenerator 再実行の手順と結果

edit-schema スキルの手順どおり。

1. `VanillaSchema/mapGenerate/placementNoise.yml` を編集（実体は**リポジトリルート直下**。`moorestech_server/` の下ではない）
2. **csc.rsp は変更不要**。`placementNoise.yml` は既に `moorestech_server/Assets/Scripts/Core.Master/csc.rsp:25` に `additionalfile` として載っている（新規スキーマ追加ではないため）
3. `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` を
   `C6-7C-91-0D-49-0C-F7-0D-32-34-32-2A-29-15-7F-EE` へ更新（再コンパイル印）
   ※初稿では値を `06-DC-04-...` と誤記していた。実際にコミットされているのは上記（Fix ラウンド1 M-4 で訂正）
4. Unity のコンパイルで SourceGenerator が走り、`Mooresmaster.Model.PlacementNoiseModule.PlacementNoise` に `TexturePngPath` が生えた

**結果の確認**: `PlacementRefConvert.cs` が `gen.TexturePngPath` を参照した状態で **RED 実行がコンパイルエラー0件で通った**（`grep "error CS" /tmp/t14_red.log` = 0件、テストは22本走って5本がアサーション失敗）。
生成物が出ていなければ `CS1061`（`PlacementNoise' does not contain a definition for 'TexturePngPath'`）で落ちるので、これが再生成成功の証拠になっている。

`Mooresmaster.Model.*` は一切手で触っていない。

### foreignKey の有無

`texturePngPath` は `foreignKey` を持たない単なる文字列なので、`validate-schema` の対象外（C# バリデーション追加は不要）。

---

## 3. master 側の変更・非破壊性の検証・pin 更新

### 変更内容

- worktree: `/Users/katsumi/moorestech-master-worktrees/mapobject-scale-cluster-keys`（ブランチ `feat/mapobject-scale-cluster-keys`）
- ベース: **`b3d543fb28f91369a94381d337e7530aca106462`**（Task 13 の `generateDetail` true 化）
- 変更ファイル: `server_v8/mods/moorestechAlphaMod_8/master/generation.json` **1本のみ**（v4〜v7 は触っていない）
- コミット: **`3651ecd5ab1df28c772d701641e74014984a5f25`** `feat(v8): placementNoise に texturePngPath を必須キーとして投入`
- 共有checkout `/Users/katsumi/moorestech_master` は**一切動かしていない**

### 一括追加のやり方と非破壊性の検証

機械的な行単位の正規表現置換で行った（JSON を再シリアライズしていない ＝ 既存フィールドの表記は1バイトも動かない）。

```python
pat = re.compile(r'^([ \t]*)"channel": "R"$', re.M)
new = pat.sub(lambda m: f'{m.group(1)}"channel": "R",\n{m.group(1)}"texturePngPath": ""', orig)
```

検証4点:

| 検証 | 結果 |
|---|---|
| 対象件数の突合 | `"channel"` の出現 = **180**、うち値は全て `"R"`、全て所属オブジェクトの末尾キー |
| 構造スキャンでの対象件数 | `noiseType` と `channel` を併せ持つオブジェクト = **180**（= 樹木プロトタイプ45件 × 4枠: slopeFilter.noise / curvatureFilter.noise / clusterNoise / clusterNoise2） |
| **逆変換で原本一致** | 挿入行と付け足したカンマを機械的に剥がすと元テキストと **`back == orig` が True**（＝既存フィールドは byte-identical） |
| git diff の形 | `360 insertions(+), 180 deletions(-)` ＝ 180行の `"channel"` 行書き換え + 180行の新規行。それ以外の行に差分なし |

投入後の再パースで **180件すべて `texturePngPath == ""`**。

なお、コントローラ追記2にある「約135箇所」は実測と食い違い、**正しくは180箇所**だった（`detailConfig` 配下の `noiseStack`（192件）は
`channel` を持たない別スキーマ `detailNoiseLayer` なので対象外。この2つを混同すると件数が合わなくなる）。

### 更新対象JSONの網羅性

edit-schema スキルは「`../moorestech_master/` 配下全体・TestMod・EditModeInPlayingTest ServerData・SandBox」を挙げている。
実際に `placementNoise` を含むファイルを全探索した結果、**対象は server_v8 の1本だけ**だった。

```
$ find . -name generation.json           # コードrepo
  moorestech_client/.../EditModeInPlayingTestMod/master/generation.json   → placementNoise 0件
  moorestech_server/.../TestMod/ForUnitTest/mods/forUnitTest/master/generation.json → placementNoise 0件
$ find /Users/katsumi/moorestech-master-worktrees/... -name generation.json
  server_v8/mods/moorestechAlphaMod_8/master/generation.json              → placementNoise 180件
```

（v4〜v7 には `generation.json` 自体が存在しない。SandBox にも generation の JSON データは無い＝スキーマだけ）
テスト用 mod 2本は樹木プロトタイプを1件も持たないため placementNoise オブジェクトがゼロで、追記の必要が無い。

### pin 更新

`.moorestech-external-revisions.json` の `moorestech_master` を
`b3d543fb28f91369a94381d337e7530aca106462` → **`3651ecd5ab1df28c772d701641e74014984a5f25`** へ更新。
`git diff --cached` で値を目視確認してからコミットした。

---

## 4. 実データにテクスチャ使用箇所がゼロであることの確認（追記3）

ブリーフの「実データにテクスチャ使用箇所は現状ゼロ」を鵜呑みにせず、**実ファイルを読んで**確認した。

**確認1: 現行の実データ（`../moorestech_master/server_v8/.../generation.json`）**
投入後の180件すべてが `texturePngPath == ""`（`Counter({"''": 180})`）。非空はゼロ。

**確認2: 移植前の MM プリセット（`TmpUnityPjt/MapMaking/Assets/MapGenerator/Presets/migration_backup.json`）**
placementNoise 相当のオブジェクト40件すべてが `"texture": {"instanceID": 0}` ＝ **Unity の null 参照**。
`Counter({'{"instanceID": 0}': 40})`。移植時に「全プリセット未使用」としてスキーマから落とした経緯は事実だった。

**結論**: 実データの生成結果は本タスクで1ピクセルも変わらない。したがって
**見た目キャッシュの `TerrainVisualCacheFormat.FormatVersion` は 7 のまま据え置き**（見た目の導出を変えていないため上げない）。

ただし `generation.json` の原文が変わった（180行の挿入）ので、`TerrainVisualCacheKey.Compute` の第1引数
`MasterHolder.GenerationMaster.SourceJsonText` が変わり、**既存キャッシュは結果的に全ワールドで作り直しになる**（Task 13 と同じ性質。正しさには影響しない）。

---

## 5. ブリーフ／移植元からの逸脱と理由

### 5-1.【要裁定】バイリニアのテクセル原点を移植元（Unity `GetPixelBilinear`）から半テクセルずらした

**これが唯一の実質的な逸脱で、実測で見つけたもの。**

ブリーフの Step 1 のテストは「2x2 の R=(0,1,0,1) の中央をサンプルすると 0.5」と書いてある。これは GPU 規約
（テクセル中心基準・`pixelCoord = uv * size - 0.5`）を要求する。一方 **移植元が呼んでいる `Texture2D.GetPixelBilinear` は
原点を `uv * size` に取る**ことが、Unity 実機との突き合わせテストで判明した:

```
--- 手書きバイリニアはUnityのGetPixelBilinearと一致する -> Failed
    channel=R u=0 v=0.0625
    Expected: 0.078431375d      ← Unity: py = 0.0625*2 = 0.125 → lerp(10,90,0.125) = 20/255
    But was:  0.039215687d      ← 本実装: py = 0.0625*2-0.5 = -0.375 → clamp → 10/255
```

`uv*size` 規約だと、**ブリーフのフィクスチャ（2x2 の UV 中央）は `px=1.0, py=1.0` ちょうどに落ちて `tx=ty=0` になり、
補間が一切効かない**（＝「バイリニアが最近傍に退化する」変異を検出できないフィクスチャになる）。
コントローラが検出必須に挙げた変異の1つがそれなので、意図は GPU 規約側だと判断した。

**採った判断**: GPU 規約（`uv*size-0.5`・端は Clamp）を採用し、ブリーフのテスト値をそのまま使う。
**代わりに、補間核そのものは Unity と同一であることを実測で固定した**:

```csharp
// Mine(u + 0.5/W, v + 0.5/H) == Unity.GetPixelBilinear(u, v)  をテクセル内部の 13x13 格子 × 4チャンネルで照合
```

これが通る（§6 GREEN）ので、**差は原点の半テクセルだけ**であり、加重平均の重み・近傍の取り方・チャンネル抽出は移植元と完全に一致する。
実用上の影響は「マスク画像が半テクセルぶんずれる」だけ（512px マスクなら 1/1024 の平行移動）。
`ManagedNoise.GetPixelBilinear` の直上コメントにこの逸脱と理由を明記してある。

**裁定に出したい点**: 「移植の忠実性最優先」を厳格に取るなら `uv*size` 側へ寄せる選択もある（その場合ブリーフの Step 1 のテスト値を
0.5 → 1.0 に書き換える必要があり、同時に「最近傍への退化」を検出する能力を別フィクスチャで作り直すことになる）。

### 5-2. 端の扱いは Clamp（移植元は Texture2D の wrapMode 依存＝ `LoadImage` 既定の Repeat）

移植元は `Texture2D` の wrapMode に従うため、`ImageConversion.LoadImage` で作った既定の **Repeat** になる。
Repeat だと UV が 1 に近づいた縁で反対側の画素が混ざり、地形の端に**継ぎ目**が出る。マスクとしては明らかに不本意な挙動なので Clamp を採った。
影響範囲は最外の半テクセルのみ。テストで固定してある（`UV=(0,0)` と `UV=(1,1)` が隅のテクセルそのものになる）。

### 5-3. 解決対象を `clusterNoise` だけでなく **4枠すべて**にした

ブリーフは「全 `clusterNoise` の `texturePngPath` を解決」と書いているが、移植元は `slopeFilter.noise` / `curvatureFilter.noise`
でもテクスチャ源を見る（`TreePlacementGenerator.cs:549` の `noise.texture == null`）。
`clusterNoise` だけ解決すると、フィルタ側にパスを書いても**無言で無視される**（`texturePixels` が null のままなので 0f 扱い）。
移植元に合わせて4枠すべてを解決するのが正しい。追加コストはゼロ（同じループ内の2行）。

### 5-4. ブリーフが指す `TreePlacementGenerator.cs` の該当箇所は、moorestech では2ファイルに分かれている

ブリーフの「Modify: `Generators/Tree/TreePlacementGenerator.cs`」は、実際には
`Generators/Tree/TreePlacementEntry.cs`（クラスタ判定・MM:335,339 相当）と
`Generators/Tree/TreePlacementCommon.cs`（`SampleFilterNoise`・MM:549 相当）にある。両方を直した。

### 5-5. テストは `Tests/UnitTest/Game/MapGeneration/Placement/` サブディレクトリへ置いた（追記5）

着手時点で `Tests/UnitTest/Game/MapGeneration/` は **.cs がちょうど10本**（上限）だったので、1本でも足すと超える。
既存の `Spawn/` `Tiling/` `Transfer/` と同じ流儀で `Placement/` を切り、3本を置いた（名前空間は既存に合わせて `Tests.UnitTest.Game.MapGeneration` のまま）。

### 5-6. ブリーフに無いテストを2本追加した

ブリーフのテストは2本だが、コントローラが検出必須に挙げた変異（チャンネル固定・上下反転・軸入れ替え）を
ブリーフの2本では**まったく検出できない**（2x2 の R が x 方向にしか変化しないフィクスチャなので、v を反転しても軸を入れ替えても 0.5 のまま）。
そのため R が x のみ・G が y のみに依存する非対称フィクスチャを足し、非正方の地形（1000×500）で読む向き検証を追加した。
さらに樹木配置側のガード2箇所を検証するテストも足した（§1(e) の InternalsVisibleTo はそのため）。

---

## 6. 実行したコマンドと出力

### 実行手段について（環境）

着手時点で macOS のログイン画面がロック中（`ioreg -n Root -d1 -a | grep -A1 CGSSessionScreenIsLocked` → `<true/>`）だったため
**Unity batchmode** で開始した。作業途中でコントローラからロック解除の連絡があったが、**そのまま batchmode を使い続けた**。理由:

- この worktree には Unity Editor が立っておらず（起動中の Editor は本体 `/Users/katsumi/moorestech` のもの）、`uloop` を使うには
  worktree 用に**2本目の Editor を起こす**ことになる。EditMode テストの結果は batchmode と同等で、2本目を起こす副作用（別 worktree との
  ポート/一時ディレクトリ競合）だけが増える
- 既に batchmode の RED/GREEN が走り終わっており、ミューテーション実行との条件を揃えたかった

すべてバックグラウンド起動＋ポーリングで待ち、単一の長時間ブロッキング呼び出しは作っていない。

```bash
/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/katsumi/moorestech-worktrees/map-autogen-5x5/moorestech_client \
  -runTests -testPlatform EditMode -testFilter "<フィルタ>" \
  -testResults /tmp/t14_xxx.xml -logFile /tmp/t14_xxx.log
```

結果XMLは `encoding="utf-8"` 宣言が実体とずれて `ElementTree.parse` が落ちるため、宣言を落として `fromstring` に食わせている（Task 12/13 と同じ回避）。
`-batchmode -runTests` は失敗があっても exit 0 を返しうるので、判定は必ず結果XMLで行っている。

### 段階ごとの結果

指定フィルタ `"PlacementNoise|ManagedNoise|TreePlacement|MapGenerationPipeline|WorldProvisioner"`（22本）:

| 段階 | 結果 |
|---|---|
| **RED**（型と解決器だけ入れ、`ManagedNoise` のテクスチャ分岐とガードは未実装） | total=20 passed=15 **failed=5（全てアサーション失敗・コンパイルエラー0件）** |
| **GREEN**（分岐とガードを実装） | total=22 passed=21 failed=1 ← §5-1 の半テクセル差が発覚 |
| **GREEN（最終）**（§5-1 の逸脱を明示してテストを書き換え、ガード検証2本を追加） | total=22 **passed=22 failed=0** |
| **最終回帰** フィルタ `Tests.UnitTest.Game.MapGeneration`（サーバー側の生成系ユニットテスト全部） | total=86 **passed=86 failed=0** |

最終回帰は最初 `...|Spawn|FluidVein|Terrain` というフィルタで走らせたが、`Terrain` が
クライアント側の EditModeInPlayingTest（PlayMode 遷移）まで拾ってしまい、本体worktreeの Unity が CEF ロックを握っている状態で
**永久ハングした**（既知の落とし穴。10分待って進捗ゼロだったので kill）。名前空間指定 `Tests.UnitTest.Game.MapGeneration` に絞り直して完走させている。

RED の内訳（すべてアサーション失敗）:

```
--- テクスチャノイズはチャンネル指定のバイリニア補間値を返す   Expected: 0.5   But was: 1.0
--- チャンネル指定ごとに異なる成分を読む                       Expected: 0.5   But was: 1.0
--- UVは横がworldXで縦がworldZに対応する                       Expected: 0.75  But was: 1.0
--- offsetとbalanceを足してamplitudeを掛けた値を返す           Expected: 1.7   But was: 1.0
--- 手書きバイリニアはUnityのGetPixelBilinearと一致する         Expected: 0.039 But was: 1.0
```

（`1.0` は「テクスチャ分岐が無いので `noiseType == None` で早期 1f」＝本タスク着手前の挙動そのもの）

### 途中で潰した「通るが何も守っていないテスト」

`テクスチャがあればノイズタイプより優先される` は当初 `frequency=10` + `GenerateOffsets`(最大10000)で FBM を回していたが、
**`Mathf.PerlinNoise` は座標が大きいと 0.5 に潰れる**ため、テクスチャ側の期待値 0.5 と偶然一致して RED でも通ってしまっていた。
小さい offsets と低周波（0.01）に替え、さらに「テクスチャを外した経路の値が期待値と 1e-2 以上離れていること」を**先に assert** して、
偶然の一致でテストが無力化しない形にした。

---

## 7. ミューテーション注入の観測結果

フィルタ `"PlacementNoiseTexture|TreePlacementTextureNoise"`（12本）。GREEN は 12/12。

コントローラが検出必須に挙げた3種（最近傍への退化 / チャンネル固定 / UVの上下反転・軸入れ替え）を含む**5種**を実際に注入し、
毎回 Unity を回して落ちることを観測した。注入は production ファイルへの直接改変で行い、各回のあとバックアップから復元している。

### MUT-A: バイリニアが最近傍に退化する（4近傍加重平均 → 1点サンプル）

```csharp
- Color bottom = Color.Lerp(GetPixel(noise, x0, y0), GetPixel(noise, x0 + 1, y0), tx);
- Color top    = Color.Lerp(GetPixel(noise, x0, y0 + 1), GetPixel(noise, x0 + 1, y0 + 1), tx);
- return Color.Lerp(bottom, top, ty);
+ return GetPixel(noise, x0 + Mathf.RoundToInt(tx), y0 + Mathf.RoundToInt(ty));
```

```
total=12 passed=6 failed=6
--- テクスチャノイズはチャンネル指定のバイリニア補間値を返す   Expected: 0.5   But was: 0.0
--- チャンネル指定ごとに異なる成分を読む                       Expected: 0.5   But was: 0.0
--- UVは横がworldXで縦がworldZに対応する                       Expected: 0.75  But was: 1.0
--- テクスチャがあればノイズタイプより優先される               Expected: 0.75  But was: 1.0
--- offsetとbalanceを足してamplitudeを掛けた値を返す           Expected: 1.7   But was: 0.7
--- 手書きバイリニアはUnity…と半テクセルずれを除いて一致する   Expected: 0.0653 But was: 0.0392
```

**6/12 が落ちる。** ブリーフのテスト1（2x2中央）が真の4方向平均になっているので、退化すると 0.5 → 0.0 と大きく外れる。

### MUT-B: チャンネル選択が固定になる（`channel` を無視して常に R）

```csharp
  static float SampleTextureChannel(Color pixel, TextureChannel channel)
  {
      switch (channel)
      {
-         case TextureChannel.R: return pixel.r;
-         case TextureChannel.G: return pixel.g;
-         case TextureChannel.B: return pixel.b;
-         case TextureChannel.A: return pixel.a;
          default: return pixel.r;
      }
  }
```

```
total=12 passed=9 failed=3
--- チャンネル指定ごとに異なる成分を読む                       Expected: 0.2509 (G)  But was: 0.5
--- UVは横がworldXで縦がworldZに対応する                       Expected: 0.1254 (G)  But was: 0.75
--- 手書きバイリニアはUnity…と半テクセルずれを除いて一致する   channel=G Expected: 0.0784 But was: 0.0392
```

**3/12 が落ちる。** R だけを見るフィクスチャでは検出できないので、4成分が別々の値を持つフィクスチャを用意した意味がここで出ている。

### MUT-C1: UV → ピクセル空間の写像が上下反転する

```csharp
- float py = v * noise.textureHeight - 0.5f;
+ float py = (1f - v) * noise.textureHeight - 0.5f;
```

```
total=12 passed=10 failed=2
--- UVは横がworldXで縦がworldZに対応する                       Expected: 0.1254 But was: 0.3764
--- 手書きバイリニアはUnity…と半テクセルずれを除いて一致する   Expected: 0.0392 But was: 0.3529
```

**2/12 が落ちる。** G が y にだけ依存するフィクスチャなので、反転すると値が 1/4 位置 → 3/4 位置へ動く。

### MUT-C2: 軸が入れ替わる（画素インデックスの行優先を列優先にする）

```csharp
- return noise.texturePixels[cy * noise.textureWidth + cx];
+ return noise.texturePixels[cx * noise.textureHeight + cy];
```

```
total=12 passed=9 failed=3
--- UVは横がworldXで縦がworldZに対応する                       Expected: 0.75  But was: 0.25
--- テクスチャがあればノイズタイプより優先される               Expected: 0.75  But was: 0.25
--- 手書きバイリニアはUnity…と半テクセルずれを除いて一致する   Expected: 0.0653 But was: 0.1013
```

**3/12 が落ちる。** 非正方テクスチャ（3x2）を使う照合テストと、R が x のみ・G が y のみに依存するフィクスチャの両方が反応する。

### MUT-D: 樹木配置側のガードから `|| texturePixels != null` を落とす（3箇所同時）

`TreePlacementCommon.SampleFilterNoise` と `TreePlacementEntry` の `clusterNoise` / `clusterNoise2`。

```
total=12 passed=10 failed=2
--- ノイズタイプNoneでもテクスチャ源があればクラスタ判定が働く
      真っ黒なクラスタテクスチャは棄却されるべき  Expected: 0  But was: 1
--- ノイズタイプNoneでもテクスチャ源があればフィルタノイズを読む
      Expected: 1.0  But was: 0.0
```

**2/12 が落ちる。** ガードを落とすと「テクスチャで真っ黒に塗った領域にも木が生える」＝機構が丸ごと死ぬ形になり、それが件数の差として出る。

### MUT-E: テクスチャ機構そのものが無い（= 本タスク着手前の実装）

これは RED 実行そのものが観測になっている（§6）。指定フィルタ22本のうち **5本がアサーション失敗**し、
すべて `But was: 1.0`（`noiseType == None` の早期 1f）だった。

---

## 8. 懸念・後続への申し送り

### 【要裁定】移植元との半テクセル差（§5-1）

実装は GPU 規約（`uv*size-0.5`）、移植元 `Texture2D.GetPixelBilinear` は `uv*size`。
補間核が同一であることは実測で固定したが、**原点は意図的に違う**。
「移植の忠実性最優先」を厳格に取るなら `uv*size` 側へ寄せる判断もありうる（その場合ブリーフ Step 1 のテスト値 0.5 → 1.0）。
実データにテクスチャ利用がゼロなので**今の見た目には一切影響しない**。将来テクスチャマスクを使い始める前に決めれば足りる。

### 【要対応】master pin がまた上がった

`.moorestech-external-revisions.json` は **`3651ecd5ab1df28c772d701641e74014984a5f25`** を指す（Task 13 の `b3d543fb28...` の直上）。
Task 15 の5x5録画を含め、以降は共有checkout `/Users/katsumi/moorestech_master` をこのコミットへ移す必要がある（`feat/mapobject-scale-cluster-keys` ブランチ上）。
**移し忘れると `MooresmasterLoaderException` で world 初期化が無言死する**（`texturePngPath` が180箇所で欠ける）。

### 既存の見た目キャッシュは全部作り直しになる

`generation.json` の原文が変わったのでキー（`SourceJsonText`）が変わる。初回起動が遅くなるのは想定内で、`FormatVersion` は 7 のまま（§4）。

### `ManagedNoise.cs` が 195行（上限200）

テクスチャ経路を移植元と同じファイルへ入れた結果、残枠が5行しかない。次にこのファイルへ何か足すときは
`Pipeline/Generators/Util/` へ `PlacementTextureSampler.cs` を切り出す（Util は現在7ファイル・残3）。
今回切り出さなかったのは、移植元 `ManagedNoise.cs` が同じ構成で、ブリーフも同ファイルを指定していたため。

### テクスチャ経路は「単体テストでは通ったが、実生成では一度も通っていない」

実データが空文字なので、`PlacementNoiseTextureResolver` の PNG 読み込みは**本番経路で1度も実行されない**。
単体テストでは PNG を書き出して往復させているが、以下は未検証:

- `Texture2D` 生成がサーバー起動スレッドで安全か → `ServerStarter.Start()`（MonoBehaviour）→ `ServerInstanceManager.Start()` は
  メインスレッドなので理屈上は安全。ただし実際に PNG を置いた状態での起動は試していない
- `DestroyImmediate` を Player ビルドで呼んだときの挙動（EditMode では問題なし）
- PNG が巨大（4K 等）だった場合のメモリ／時間

### 解決のスコープ

`PlacementNoise` は現在 `TreePrototypeEntry` 配下の4枠にしか存在しない（`grep -rn "PlacementNoise" Pipeline/Config/` で確認済み）。
将来 Object/Ore 側の設定へ `PlacementNoise` を生やしたら、`PlacementNoiseTextureResolver.Resolve` のループも足す必要がある。
足し忘れると「パスを書いたのに無言で効かない」形になるので、その時は本レポートの §5-3 と同じ判断をすること。

### `InternalsVisibleTo` を Game.MapGeneration に追加した

`Server.Tests` から `TreePlacementCommon` / `TreePlacementEntry` を叩くため。production の可視性は変えていない
（`internal` のまま）が、アセンブリ属性が1つ増えたことは申し送る。

> **【Fix ラウンド1 で解消】** I-1 の指摘により `AssemblyInfo.cs` は削除済み。アセンブリ属性の増加は無くなった。

### 実行手段

コントローラからロック解除の連絡を受けたが、**最後まで Unity batchmode で回した**（理由は §6 冒頭）。`uloop` は使っていない。

---

# Fix ラウンド1（レビュー指摘 I-1 / I-3 / I-2 / M-1 / M-4 への対応）

BASE: `5b7bf88a8`（半テクセル裁定コミット）/ worktree: 同上

**ステータス: DONE**（サンプリング規約と master には一切触っていない）

## 前提（対応不要と確定した2件）

- **半テクセル問題はユーザー裁定で現行実装が追認された**（`.decisions/2026-08-16-テクスチャノイズのサンプリング規約はGPU標準に統一する.md`）。
  `ManagedNoise.GetPixelBilinear` の `uv*size-0.5` + Clamp は**1行も変えていない**
- **master 側の一括追加はレビュアーの独立検証で非破壊確認済み**。`../moorestech_master` は**一切触っていない**（pin も据え置き）

## I-1: `InternalsVisibleTo` を削除し、テストを公開経路へ移した → **対応済み（削除した）**

指摘どおり、サーバー側アセンブリ群で初の IVT 導入だった上に、開けるのは1つの internal ではなく
`Game.MapGeneration` の internal 全部だった。1テストの都合で契約面を恒久的に広げる取引は成立していない。

- `moorestech_server/.../Game.MapGeneration/AssemblyInfo.cs` と `.meta` を**削除**
- `TreePlacementTextureNoiseTest` を、`TreePlacementEntry.TryPlaceEntry` / `TreePlacementCommon.SampleFilterNoise`
  （どちらも internal）の直叩きから、**public な `TreePlacementGenerator.GenerateForBiome` 経由の配置数観測**へ全面的に書き換えた
  - フィクスチャ: 全面 true のマスク / 全高 0 の平地 / 200m 角・resolution 16 の1タイル / プロトタイプ1件 / `new System.Random(1)`
  - 観測量は `List<PlacementEntry>.Count`（白テクスチャ経路で 311件、棄却経路で 0件）
- フィルタノイズ側は指摘のとおり `slopeFilter.enabled = true` で公開経路から観測できた。
  `range=[0.5,1.5]` / `smoothness=0` にすると、平地の傾斜 0 に足されるノイズ値が **テクスチャの 1.0 なら通過・源なしの 0 なら全棄却**になる

**IVT を消しても検出力は落ちなかった**（下の3つの個別変異がすべて落ちている）。むしろ Poisson の4パス・密度ノイズ・
共有グリッド・下層木まで含んだ実際の呼び出し経路を通るぶん、テストとして強くなっている。

削除の安全性: `grep` で `Server.Tests` 側から `Game.MapGeneration` の internal を参照している箇所が
この1ファイルだけであることを確認済み。IVT 削除後にコンパイルエラー0件で87本走ったことが裏付けになっている。

## I-3: `clusterNoise2` のガードに個別テストを足した → **対応済み**

新規テスト `クラスタノイズ2のテクスチャ源もnoise2Opの合成に加わる` を追加した。

- `clusterNoise` = 白（値 1.0）、`clusterNoise2` = **黒（値 0.0）＝別レベル**、どちらも `noiseType = None`
- `noise2Op = Multiply` → 合成 0.0 は `hardEdge`(=0.18) 未満で**全候補棄却 → 0件**
- `noise2Op = Max` → 合成 1.0 のまま**通過 → 311件**

Max 側を対にしたのは、「clusterNoise2 にテクスチャがあると常に棄却される」ではなく
**`noise2Op` の合成結果で決まっている**ことまで固定するため（`noise2Op` を Multiply 固定にする変異も落ちる）。

### ミューテーション注入の観測（**3つのガードを1つずつ**個別に落とした）

前回の MUT-D が3箇所同時だったという指摘に対応し、**1箇所ずつ**注入して毎回 Unity を回した。
フィルタは全実行で `"PlacementNoise|ManagedNoise|TreePlacement"`（13本）。GREEN は 13/13。

**MUT-1: `clusterNoise2` のガードだけを落とす**（`TreePlacementEntry.cs:73`）

```csharp
- if (entry.clusterNoise2.noiseType != MapNoiseType.None || entry.clusterNoise2.texturePixels != null)
+ if (entry.clusterNoise2.noiseType != MapNoiseType.None)
```

```
total=13 passed=12 failed=1
--- クラスタノイズ2のテクスチャ源もnoise2Opの合成に加わる -> Failed
    白と黒のMultiplyは0になり全候補が棄却されるべき |   Expected: 0 |   But was:  311
```

**指摘どおり、この変異は従来のテスト群では1本も落ちなかった。新テストがピンポイントで落としている。**

**MUT-2: `clusterNoise` のガードだけを落とす**（`TreePlacementEntry.cs:69`）

```csharp
- if (entry.clusterNoise.noiseType != MapNoiseType.None || entry.clusterNoise.texturePixels != null)
+ if (entry.clusterNoise.noiseType != MapNoiseType.None)
```

```
total=13 passed=11 failed=2
--- クラスタノイズ2のテクスチャ源もnoise2Opの合成に加わる -> Failed
    白と黒のMultiplyは0になり全候補が棄却されるべき |   Expected: 0 |   But was:  311
--- ノイズタイプNoneでもテクスチャ源があればクラスタ判定が働く -> Failed
    真っ黒なクラスタテクスチャは全候補を棄却するべき |   Expected: 0 |   But was:  311
```

（外側のガードなので clusterNoise2 のテストも巻き込んで落ちる。これは包含関係として正しい）

**MUT-3: フィルタノイズのガードだけを落とす**（`TreePlacementCommon.cs:62`）

```csharp
- if (noise.noiseType == MapNoiseType.None && noise.texturePixels == null) return 0f;
+ if (noise.noiseType == MapNoiseType.None) return 0f;
```

```
total=13 passed=12 failed=1
--- ノイズタイプNoneでもテクスチャ源があればフィルタノイズを読む -> Failed
    テクスチャの1.0が傾斜へ足されて範囲に入るべき |   Expected: greater than 0 |   But was:  0
```

3変異とも注入 → 観測 → バックアップから復元し、復元後に production ファイルの差分がゼロであることを
`git status` で確認してから最終 GREEN を回した。

## M-1: `DestroyImmediate` → `Destroy` → **試したが Unity が拒否したので `DestroyImmediate` のまま据え置き**

指摘に従って両方を `UnityEngine.Object.Destroy` に替え、実際に走らせたところ**既存テスト2本が落ちた**。

```
total=13 passed=11 failed=2
--- 全バイオームの全ノイズ枠のPNGを画素へ展開する -> Failed
    Unhandled log message: '[Error] Destroy may not be called from edit mode! Use DestroyImmediate instead.
     | Destroying an object in edit mode destroys it permanently.'
--- 手書きバイリニアはUnityのGetPixelBilinearと半テクセルずれを除いて一致する -> Failed
    同上
```

Unity は **EditMode での `Object.Destroy` を拒否し、テクスチャを破棄しない**（ネイティブ実体が残る）。
サーバーのユニットテストは EditMode で走るので、`Destroy` は「懸念が消える」どころか
**この worktree で確実に壊れ、かつリークする**選択になる。

据え置きの根拠を補強しておくと、ここで捨てるのは**シーングラフに一切入らない一時 `Texture2D`** で、
`DestroyImmediate` が警告される典型ケース（コールバック中にシーンオブジェクトを消して反復を壊す）に当たらない。
Player でも同期解放として正常に働く。**`DestroyImmediate` 固定である理由をコード直上の2行コメントに明記した**ので、
「未検証のまま放置」という §8 の申告状態は解消されている。

`#if UNITY_EDITOR` で二経路に割る案は採らなかった（挙動を分岐させる必要が実在しないうえ、
エディタ専用コードはファイル末尾に置く規約とも噛み合わない）。

## M-4: 報告 §2 の `dummyText` 誤記 → **対応済み**

§2 手順3 の値を実コミット値 `C6-7C-91-0D-49-0C-F7-0D-32-34-32-2A-29-15-7F-EE` へ訂正し、誤記した旨も併記した。
`_CompileRequester.cs:8` の実値と一致することを確認済み。

## I-2（記録のみ）: マスクの UV はタイル単位で一周する

`TreePlacementEntry.cs:22-23` の `point.x / dims.TerrainWidth` が示すとおり、`SamplePlacementNoise` に渡る
`point` は**タイルローカル座標**（0..TerrainWidth）なので、テクスチャ UV も **0..1 がタイルごとに一周する**。
移植元 MM は地形1枚だったので「マスク1枚 = マップ全体」だったが、moorestech の 5x5 では
**同じマスクが25タイルに繰り返しスタンプされる**。式は MM の写経として正しく、実データの `texturePngPath` が
180件すべて空文字なので現状は無害。**最初にマスク PNG を描く人が「世界地図として描いたのにタイルごとに
繰り返された」と踏み抜く**ので、その時点で「ワールド絶対座標 UV」へ寄せるか裁定すること（YAGNI のため今回は対応しない）。

## 変更ファイル

| ファイル | 変更 |
|---|---|
| `moorestech_server/.../Game.MapGeneration/AssemblyInfo.cs` (+ `.meta`) | **削除**（I-1） |
| `moorestech_server/.../Tests/UnitTest/Game/MapGeneration/Placement/TreePlacementTextureNoiseTest.cs` | 公開経路へ全面書き換え＋`clusterNoise2` テスト追加（I-1 / I-3）。119行 |
| `moorestech_server/.../Pipeline/Runtime/PlacementNoiseTextureResolver.cs` | `DestroyImmediate` 固定の根拠コメント2行のみ（M-1） |
| `.superpowers/sdd/task-14-report.md` | §2 の誤記訂正＋本セクション（M-4） |

**触っていないもの**: サンプリング規約（`ManagedNoise`）／スキーマ／`../moorestech_master`／
`.moorestech-external-revisions.json` の pin／`TreePlacementEntry.cs`・`TreePlacementCommon.cs` の production コード。

## 実行したコマンドと結果

Unity Editor はこの worktree では立っておらず（起動中は本体 `/Users/katsumi/moorestech` のもの）、
2本目を起こすと CEF/TMPDIR 共有ロックで永久ハングする既知事故があるため、**batchmode を継続**した。
すべてバックグラウンド起動＋ポーリングで、単一の長時間ブロッキング呼び出しは作っていない。

```bash
/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath .../map-autogen-5x5/moorestech_client \
  -runTests -testPlatform EditMode -testFilter "<フィルタ>" \
  -testResults /tmp/t14fix_xxx.xml -logFile /tmp/t14fix_xxx.log
```

| 段階 | フィルタ | 結果 |
|---|---|---|
| M-1 検証（`Destroy` 版） | `PlacementNoise\|ManagedNoise\|TreePlacement` | total=13 passed=11 **failed=2**（EditMode で Destroy 不可） |
| MUT-1 clusterNoise2 のみ | 同上 | total=13 passed=12 **failed=1** |
| MUT-2 clusterNoise のみ | 同上 | total=13 passed=11 **failed=2** |
| MUT-3 フィルタのみ | 同上 | total=13 passed=12 **failed=1** |
| **最終 GREEN / 回帰** | `Tests.UnitTest.Game.MapGeneration` | total=87 **passed=87 failed=0** |

コンパイルは最終回帰の中で全アセンブリが再ビルドされており、`grep -cE "error CS\|Compilation failed" /tmp/t14fix_final.log` = **0**。
IVT 削除で `Server.Tests` から internal 参照が残っていれば CS0122 で落ちるので、これが削除完遂の裏付けになっている。
（Task 14 初稿の86本 → 87本は、樹木ガードのテストが2本→3本に増えたぶん）

## 新たな懸念

- **配置数 311 という具体値には依存していない**（アサーションは `0` と `> 0` のみ）。ただし `System.Random(1)` と
  `Mathf.PerlinNoise` に依存した「>0」なので、密度ノイズの既定値（`denseMinThreshold` 等）を大きく動かすと
  白テクスチャ経路が 0件に転んでテストが偽陽性で落ちうる。その時はフィクスチャの地形サイズを広げること
- テストが Poisson 4パス＋下層木まで通すようになったぶん、1本あたりの実行時間は増えた（体感で数百 ms 程度・許容範囲）
- I-2 のタイル単位 UV は未対応のまま。最初のマスク PNG 投入時に必ず表面化する
