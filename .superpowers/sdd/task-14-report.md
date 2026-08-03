# Task 14: MasterSourceTextCollectorのpush化（D5案B） — 報告

**ステータス: DONE**
**コミット: `45cdaee58` refactor: 原文収集をClient.Game側へ移しLocalize基盤からドメイン語彙とServerContext依存を除去**

---

## 何を実装したか

ユーザー裁定 D5=案B（push形）に沿って、ローカライズ**基盤** `Client.Localization` からドメイン語彙と
`ServerContext` 依存を切り離した。基盤は「渡された辞書primitiveを合成するだけ」の存在になった。

1. **`MasterSourceTextCollector` をGame層へ移設**
   `Client.Localization/MasterSourceTextCollector.cs` → `Client.Game/Localization/MasterSourceTextCollector.cs`
   （`git mv` で `.cs` と `.cs.meta` をペア移動。GUIDは保存されている）。namespace は `Client.Localization` →
   `Client.Game.Localization`。ディレクトリ `.meta`（`Client.Game/Localization.meta`）はUnity自動生成のものをコミット。

2. **`Localize.MergeGameDictionaries` を3引数push形へ一本化**
   - 削除: `public static void MergeGameDictionaries(ModsResource)`（pull形。内部で
     `ServerContext.GetService<MasterJsonFileContainer>()` と `MasterSourceTextCollector.Collect()` を引いていた）
   - 削除: `internal static void MergeGameDictionaries(ModsResource, IReadOnlyList<ModId>)`
   - 新: `public static void MergeGameDictionaries(ModsResource modsResource, IReadOnlyList<ModId> orderedModIds, IReadOnlyDictionary<string, string> masterSourceTexts)`
   既存の `OverlayMasterSourceTexts(candidate, IReadOnlyDictionary<string,string>)` が持っていたpush口の前例に
   シグネチャを揃えた形。

3. **`Client.Localization.asmdef` から `Game.Context` 参照を削除**（`Core.Master` は `ModId` 用に残置）。
   `Localize.cs` の `using Game.Context;` も削除。**この状態で Error 0 でコンパイルが通ることが依存断ちの証明**。

4. **合成の組み立てを composition root（`InitializeScenePipeline`）へ移動**
   ```csharp
   var masterContainer = ServerContext.GetService<MasterJsonFileContainer>();
   Localize.MergeGameDictionaries(ServerContext.GetService<global::Mod.Loader.ModsResource>(), masterContainer.SortedModIds, MasterSourceTextCollector.Collect());
   ```

5. **`GetTutorialDisplayText` の公開面を落とした**
   ブリーフは `private` 化を指示していたが、呼び出し元が `Collect()` 1箇所だけであるため、
   global-constraints #7（「単一呼び出し元のprivateヘルパーは呼び出し元メソッド末尾の単一 `#region Internal` 内
   ローカル関数へ移す」／reviewer core-cs-region-internal基準4）に従い、`Collect()` 末尾の `#region Internal` 内
   **ローカル関数**にした。`private static` メソッドとして残すとその基準に新規違反する形になるため。
   前例は同アセンブリの `ModLocalizationMerger.Merge` の `ValidateModOrder`（`return` 後に `#region Internal`）。
   公開面から消える点は指示どおり（むしろより強い）。

---

## TDDの証拠

### RED（Step 1: テストだけ先に3引数形＋新namespaceへ追従）

テスト4本を先に書き換えた（実装は未変更）:
`GameDictionaryRecompositionTest.cs` / `LocalizeContentTest.cs` /
`MasterSource/MasterSourceTextCollectorTest.cs` / `MasterSource/MasterSourceCoverageTest.cs`

```
$ uloop compile --project-path ./moorestech_client
ErrorCount: 4
- Assets/Scripts/Client.Tests/Localization/GameDictionaryRecompositionTest.cs(3,19): error CS0234: The type or namespace name 'Localization' does not exist in the namespace 'Client.Game' (are you missing an assembly reference?)
- Assets/Scripts/Client.Tests/Localization/LocalizeContentTest.cs(4,19): error CS0234: The type or namespace name 'Localization' does not exist in the namespace 'Client.Game' (are you missing an assembly reference?)
- Assets/Scripts/Client.Tests/Localization/MasterSource/MasterSourceCoverageTest.cs(5,19): error CS0234: The type or namespace name 'Localization' does not exist in the namespace 'Client.Game' (are you missing an assembly reference?)
- Assets/Scripts/Client.Tests/Localization/MasterSource/MasterSourceTextCollectorTest.cs(2,19): error CS0234: The type or namespace name 'Localization' does not exist in the namespace 'Client.Game' (are you missing an assembly reference?)
```

**なぜ想定どおりか**: この時点で `Client.Game.Localization` はまだ存在せず（Collectorは
`Client.Localization` にある）、`Localize` にも3引数オーバーロードが無い。テストが要求する新しい
配置とシグネチャに実装が追いついていないことを、コンパイラが4ファイル全部で報告している。
（3引数呼び出しのCS1501は、using解決失敗による `MasterSourceTextCollector` 未解決の
カスケードとしてRoslynに抑制されているが、REDの根拠としては同じ「未実装」を指している。）

### GREEN（Step 2: 実装後）

```
$ uloop compile --project-path ./moorestech_client
ErrorCount: 0
```
※ `Client.Localization.asmdef` から `Game.Context` を抜いた状態での Error 0。

```
$ uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value ".*(MasterSource|GameDictionary|LocalizeContent|LocalizeTest).*"
{
  "Success": true,
  "Message": "Test execution completed with status: Passed",
  "TestCount": 28,
  "PassedCount": 28,
  "FailedCount": 0,
  "SkippedCount": 0
}
```

ブリーフ指定の絞り込み（`MasterSource|GameDictionary`）でも実行済み: 11/11 pass。

### 回帰確認（広域）

```
$ uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value ".*(Localiz|Localize|Skit).*"
TestCount: 123 / PassedCount: 121 / FailedCount: 2
```
失敗2件は**既知のbranch-red**（下記「問題や懸念事項」参照）。本タスク起因ではない。

---

## 変更したファイル

| 種別 | パス |
|---|---|
| 移動(+namespace変更) | `moorestech_client/Assets/Scripts/Client.Localization/MasterSourceTextCollector.cs` → `moorestech_client/Assets/Scripts/Client.Game/Localization/MasterSourceTextCollector.cs`（`.cs.meta` も同時に `git mv`） |
| 追加(Unity生成) | `moorestech_client/Assets/Scripts/Client.Game/Localization.meta` |
| 変更 | `moorestech_client/Assets/Scripts/Client.Localization/Localize.cs`（pull形削除・3引数public一本化・`using Game.Context;` 削除） |
| 変更 | `moorestech_client/Assets/Scripts/Client.Localization/Client.Localization.asmdef`（`Game.Context` 参照削除） |
| 変更 | `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs`（3引数形で組み立て。`using Client.Game.Localization;` `using Core.Master;` 追加） |
| 変更(テスト) | `moorestech_client/Assets/Scripts/Client.Tests/Localization/GameDictionaryRecompositionTest.cs` |
| 変更(テスト) | `moorestech_client/Assets/Scripts/Client.Tests/Localization/LocalizeContentTest.cs` |
| 変更(テスト) | `moorestech_client/Assets/Scripts/Client.Tests/Localization/MasterSource/MasterSourceTextCollectorTest.cs` |
| 変更(テスト) | `moorestech_client/Assets/Scripts/Client.Tests/Localization/MasterSource/MasterSourceCoverageTest.cs` |

asmdefの追加は不要だった: `Client.Game.asmdef` は既に `Core.Master` を参照し、`Mooresmaster.Localization.Generated`
（`ContentLocalizationKeys`）と `Mooresmaster.Model.ChallengesModule` を同アセンブリ内の既存コードが使用済み。
`Client.Starter` / `Client.Tests` はいずれも `Client.Game` を参照済みで、移設後のCollectorに到達できる。

---

## 自己レビューの所見（チェックリスト実測）

- [x] **`Client.Localization.asmdef` から `Game.Context` を削除してコンパイルが通る** — Error 0。依存断ちの証明。
- [x] **`Localize` 内から `ServerContext.GetService` が消えた** —
      `grep -rn "ServerContext\|Game.Context" Client.Localization/` → 一致0件（asmdefも含めて0）。
- [x] **pullオーバーロード削除・全呼び出し側3引数形へ追従** —
      `grep -rn MergeGameDictionaries` の全ヒットが3引数形。定義は `Localize.cs:63` の1本のみ。
- [x] **移設後のCollectorが `Client.Game` 側asmdefで解決できる** — コンパイル通過、テスト28本pass。
- [x] **`GetTutorialDisplayText` が公開面から消えた** — `Collect()` 内のローカル関数へ（上記5の理由で
      `private static` ではなくローカル関数を選択）。他の呼び出し元は元から存在しない（grep確認済み）。
- [x] **1ファイル200行以下** — `Localize.cs` 192行、`MasterSourceTextCollector.cs` 128行。
- [x] **1ディレクトリ10ファイル** — `Client.Game/Localization/` は1ファイル。`Client.Localization/` は5 .cs（減った）。
- [x] **.meta手動作成なし** — `.cs.meta` は `git mv` でGUID維持、ディレクトリ `.meta` はUnity生成物をコミット。
- [x] **`Func<>`・`partial`・デフォルト引数・単純getter/setter なし。** try-catch追加なし。
- [x] **コメント規約** — 日本語1行＋英語1行のセット。追加分は処理20字/メソッド30字の目安内に収めた。

### テストの意味が変わらないことの確認

pull形が内部で暗黙に引いていた2つの値（`MasterJsonFileContainer.SortedModIds` と `Collect()`）を、
テスト側で**明示的に**渡す形にした。期待挙動は不変:

- `MasterSourceOverwritesCollidingModSourceForAllCollectedContent` — Master正本がmod Sourceを上書きする検証は
  `Collect()` を明示的に渡すことで従来と同一の合成になる。
- `PublicEntryPointRejectsResourceWhoseIdsDifferFromMasterOrder` — 従来はpull形が引いていた
  `masterContainer.SortedModIds` をテストが `ServerContext` から取って渡す。空のModsResourceと突き合わせて
  `ModLocalizationMerger.ValidateModOrder` が `InvalidOperationException` を投げる経路は従来どおり。
- `StartupEntryPointUsesRegisteredModOrderAfterMasterLoad` — 起動口の組み立て
  （registered mod order + Master原文）を `InitializeScenePipeline` と同じ形で再現する契約テストとして維持。
- 純粋にmod由来キーだけを見る recomposition 系のテストも、挙動同一性を保つため `Collect()` を明示的に渡した
  （キーが衝突しないため結果は不変だが、「pull形と同じ入力」であることを明示する意図）。

---

## 問題や懸念事項

### 1. 既知のbranch-red 2件（本タスク起因ではない・触っていない）

`Client.Tests.Localization.Skit.SkitLocalizationDictionaryCompletenessTest.CommandForgeDictionaryKeepsRootFlatTranslationsAndBaselineValues`

| ケース | 期待 | 実測 |
|---|---|---|
| `("english", 139, "2d400074…")` | 139 | 143 |
| `("japanese", 204, "9fc582ef…")` | 204 | 208 |

origin/masterマージでskit台詞が増えたことによるbaseline件数のズレ。本タスクはskit辞書にも
Collectorのキー種にも触れていない（Collectorの中身は移設のみで1行も収集ロジックを変えていない）。
指示どおり**未修正のまま残置**した。

### 2. `InitializeScenePipeline.cs` が202→205行（200行規約超過の継続）

このファイルは元から200行規約を超えている（origin/masterでは214行、本ブランチで202行まで縮んでいた）。
今回 `using` 2行と `masterContainer` ローカル1行で205行になった。ブリーフが指定した組み立て位置が
このファイルなので指示どおりにしたが、**規約超過が続いている**点は記録しておく。
`Client.Starter/Initialization/`（`ServerConnectionInitializer` / `ModAssetLoader` が居る）へ
辞書合成の組み立てを切り出す案もあったが、タスク範囲外の再構築と判断して見送った。

### 3. 環境的なノイズ（作業への影響なし）

テスト実行中に一度 `uloop` が `UserSettings/UnityMcpSettings.json not found` で全コマンド不通になった
（`UnityMcpSettings.json` が `.bak` へリネームされる既知現象）。`cp UnityMcpSettings.json.bak UnityMcpSettings.json`
で復旧し、以降のコンパイル・テストは正常。`UserSettings/` は追跡対象外なのでコミットには含まれていない。
