# 最終ブランチレビュー Critical 修正レポート

対象: `IDeleteTarget.IsRemovable(out LocalizationKey)` が「拒否だが理由キー無し」を型で許し、
`default(LocalizationKey)`（`Key == null`）が辞書引きへ流れて `ArgumentNullException` になる問題。
採用方針は指示どおり案A（`LocalizationKey?` へのnullable化・最小スコープ）。

（注: 本ファイルは 2026-07-29 の別ラウンド「最終レビュー機械的修正 適用レポート」を上書きしている。`.superpowers/sdd/` は gitignore 対象）

## 修正内容（ファイルごと）

| ファイル | 変更 |
|---|---|
| `Client.Game/InGame/UI/UIState/State/IDeleteTarget.cs` | `bool IsRemovable(out LocalizationKey reason)` → `bool IsRemovable(out LocalizationKey? deniedReason)`。docコメントを「拒否かつ表示すべき理由があるときだけ埋まり、無いときはnull」へ更新 |
| `.../UIState/State/DragDelete/DragDeleteSelection.cs` | `TryAddTarget(IDeleteTarget, out LocalizationKey?)` へ。冒頭の `denyReason = default` を `= null` へ（`_canceled` 早期returnが「理由なし拒否」として型上も正当になる） |
| `.../UIState/State/DragDelete/DeleteObjectService.cs` | `ShowDenyReason(LocalizationKey? denyReasonKey)` へ。冒頭で `if (!denyReasonKey.HasValue) return;`（理由なし拒否は無表示。変更前の `Show(null, isLocalize:false)` 相当の無害動作に戻る）。以降は `.Value` を使用 |
| `.../InGame/Train/RailGraph/DeleteTargetRail.cs` | `IsRemovable` を nullable 化し、`DeleteDeniedReason.None => null` / `DeleteDeniedReason.Removed => null` に置換。なぜnullかの根拠コメントを追加 |
| `.../InGame/Block/BlockGameObjectChild.cs` | 内部の `LocalizationKey? _removeDeniedReason` をそのまま `out` へ渡す形になり `.Value` 展開が不要に。非拒否時は `deniedReason = null` |
| `.../InGame/Entity/Object/TrainCarEntityChildrenObject.cs` | `out LocalizationKey?` へ追従、`= null` |
| `Client.Tests/UIState/FakeDeleteTarget.cs` | `out LocalizationKey?` へ追従。加えて `public LocalizationKey? DenyReason` を新設し「拒否＋理由あり」「拒否＋理由なし」を作り分け可能に（`deniedReason = Removable ? null : DenyReason;`） |
| `Client.Tests/UIState/DragDeleteSelectionCategoryTest.cs` | `Assert.AreEqual(key, denyReason)` を `HasValue` 検証 + `.Value` 比較へ（nullable の暗黙boxing比較に依存しない形へ明示化） |
| `Client.Tests/UIState/DragDeleteDenyReasonTest.cs`（新規） | 回帰テスト4本（下記） |

## 追加したテストと、修正前に赤くなることの確認

新規: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDeleteDenyReasonTest.cs`

1. `ReasonlessDenialNeverReachesDictionaryLookup`
   「拒否だが理由キー無し」の対象（`Removable=false, DenyReason=null`）を `DragDeleteSelection.TryAddTarget` に食わせ、
   **先に** `DeleteObjectService.ShowDenyReason` と同じ判定順（`HasValue` のときだけ `Localize.Get`）をなぞるヘルパで解決する。
   空キーが漏れていればここで辞書引き例外が起きるため、本番と同じ症状がそのまま失敗として現れる。続けて `HasValue == false` を固定。
2. `CanceledSelectionDenialCarriesNoReasonKey`
   `_canceled` 早期return経路（型としては同じ穴だった箇所）も理由なし拒否として安全に扱えることを固定。
3. `DenialWithReasonKeyResolvesToLocalizedText`
   理由キーがある拒否は従来どおり `Localize.Get` まで解決されること（nullable化で正常系を壊していないこと）を固定。
4. `EmptyLocalizationKeyBreaksTheDictionaryLookup`
   `Localize.Get(default(LocalizationKey))` が `ArgumentNullException` を投げることを明示的に固定し、
   「理由なし拒否をnullでしか安全に表せない」根拠をテストとして残す。

### mutation による実証（修正前なら赤くなることの確認）

修正後のコードに、修正前と同じ「理由なし拒否を空キーに潰す」挙動を一時的に再導入した:

```csharp
// DragDeleteSelection.TryAddTarget（mutation・検証後にrevert済み）
if (!target.IsRemovable(out denyReason))
{
    denyReason ??= default(LocalizationKey);
    return false;
}
```

結果: `DragDeleteDenyReasonTest` 4件中1件失敗（`ReasonlessDenialNeverReachesDictionaryLookup`）。
失敗内容は本番と同一の例外で、スタックトレースも本番経路と一致:

```
System.ArgumentNullException : Value cannot be null.
Parameter name: key
  at System.Collections.Generic.Dictionary`2.FindEntry (TKey key)
  at System.Collections.ObjectModel.ReadOnlyDictionary`2.TryGetValue (TKey key, TValue& value)
  at Client.Localization.LocalizationTextResolver.<Resolve>g__TryGetText|0_0 (... LocalizationTextResolver.cs:27)
  at Client.Localization.LocalizationTextResolver.Resolve (...)
```

mutation を revert 後、同テストは 4/4 green。
なお修正前のソースそのままではテストはコンパイル不能（`out` 引数の型が異なる）ため、
「同じ不正状態を型の外側から再導入する」形の mutation で赤を実証した。

## `IsRemovable` の全列挙結果（レビュアー表との一致確認）

`grep -rn "IsRemovable" moorestech_client/Assets/Scripts --include="*.cs"` に加え、
`moorestech_server` 側と `TryAddTarget` 呼び出しも同時に掃引した。結果は**レビュアーの表と完全一致**（追加・欠落なし）。

| 箇所 | レビュアー判定 | 再確認 |
|---|---|---|
| `UI/UIState/State/IDeleteTarget.cs:27`（宣言） | — | 一致（唯一の宣言） |
| `Block/BlockGameObjectChild.cs:35-45` | 安全（`default` は `return true` 側のみ） | 一致。内部は既に `LocalizationKey?` で、nullable化により `.Value` 展開が消えた |
| `Entity/Object/TrainCarEntityChildrenObject.cs:39-43` | 安全（常に true） | 一致 |
| `Train/RailGraph/DeleteTargetRail.cs:54` | 違反・到達可能 | 一致（`Removed => default` かつ `return false`）。`None => default` も返り値trueで無害だが同時にnull化 |
| `Client.Tests/UIState/FakeDeleteTarget.cs:30` | 違反（下流未検証） | 一致。nullable化＋`DenyReason` 追加＋下流を検証する新規テストで解消 |
| `DragDelete/DragDeleteSelection.cs:42-43`（`_canceled` 早期return） | 違反・`CanCommit()` 事前ガードで遮蔽 | 一致。`= null` 化で型としても正当に |
| 呼び出し元 `DragDelete/DragDeleteSelection.cs:47` | 素通し | 一致（nullable がそのまま透過） |
| 呼び出し元 `DragDelete/DeleteObjectService.cs:114`（単体ホバー） | — | `out var reason` が `LocalizationKey?` になり `ShowDenyReason` の `HasValue` ガードに乗る |
| `moorestech_server` 配下 | — | 実装・参照なし（クライアント専用インターフェース） |

`IDeleteTarget` の実装は4クラス（`BlockGameObjectChild` / `TrainCarEntityChildrenObject` / `DeleteTargetRail` / `FakeDeleteTarget`）で全て追従済み。

## 検証結果

- `uloop compile --project-path ./moorestech_client` → `Success: true` / `ErrorCount: 0`（新規warningなし）
- `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value ".*(DragDelete|DeleteObject|DeleteTarget|Localiz).*"`
  → **137 / 137 passed, 0 failed**
- 新規テスト単独 `.*DragDeleteDenyReason.*` → 4 / 4 passed

### 検証中に観測した無関係のflake（本修正とは別件）

`Client.Tests.Localization.Skit.SkitFailureCleanupTest.DisposedResolverStopsReloadingAndToleratesRepeatedDispose` が
137件フルランのうち1回だけ失敗した。内容は
`Unhandled log message: '[Exception] InvalidOperationException: reload failed'`
（`SkitLocalizationResolver.SchedulePendingReload` で `Forget()` した UniTask の例外ログが次テストのフレームへ持ち越され `LogAssert` に拾われる）。
単独実行では 2/2 green、再フルランでも 137/137 green のため、テスト境界をまたぐ非同期ログのタイミング依存flakeと判断。
本修正の変更ファイルとは無関係（`Client.Game/Skit/Localization` 配下・本修正では未変更）。

### 運用メモ

検証中に `UserSettings/UnityMcpSettings.json` が2回 `.bak` 化して uloop が全断した。
いずれも `cp UnityMcpSettings.json.bak UnityMcpSettings.json` で復旧して継続（既知現象）。
