# UniTask / Unity のキャンセル race 仕様

`await` と `GetCancellationTokenOnDestroy()` / `UniTaskCompletionSource` / Unity の `OnDestroy` 発火タイミングが絡む race 指摘を出す前に、このドキュメントを必ず読む。

> **【T8 追加・最重要】** ユーザー本人が 2026-05-07T8 で「subagent 内部に UniTask 関連の仕様についてのドキュメントを参照するように明示する」と再々々度直筆指示。本ファイル Read は **Pattern B 系の指摘を 1 件でも組み立てる前提条件** であり、Read 未実行時は Critical/Warning/Info 全レベルで出力禁止（subagent-common-rules.md Step 0.1 / Step 0.4 / unitask-pattern-reviewer.md description / criterion 4 / セルフチェック の各箇所で同義の制約をかけている。本ファイルを開いている時点でその制約は満たしている）。

## 最重要: `ct.ThrowIfCancellationRequested()` を await 後に差すだけでは race は閉じない

典型的な false-positive 指摘:

> `_cached = await SomeAsyncCall(ct);` の直後に `ct.ThrowIfCancellationRequested();` を追加して Destroy race を閉じよ

この指摘は **ほぼ常に誤り**。理由は以下。

### `GetCancellationTokenOnDestroy` の発火タイミング

Unity の `MonoBehaviour.OnDestroy()` が走った瞬間に初めて Cancel される。実装は `AsyncDestroyTrigger.cs:49-55` (UniTask 本体):

```csharp
void OnDestroy()
{
    called = true;
    cancellationTokenSource?.Cancel();
    cancellationTokenSource?.Dispose();
}
```

そして `Destroy(gameObject)` は **遅延実行**: 呼ばれた時点ではキューされるだけで、`OnDestroy` は Unity のフレーム末尾まで走らない。`DestroyImmediate` なら同期だが、UI コード中では通常使わない。

### `IObservable.ToUniTask` 系の値到着 vs Cancel の先着順

`UniTaskObservableExtensions.cs` の `ToUniTask` 系は内部で `UniTaskCompletionSource` を使い、`TrySetResult` / `TrySetCanceled` の **先着が勝つ**。両者ともメインスレッド上で原子的に処理されるので、競合した場合は「フレーム内で先に走ったほう」が確定する。値到着が先 → 結果が代入される。Cancel が先 → `OperationCanceledException` で await が再開しない。

### 仕様確認の出典まとめ（subagent はここを引用すること）

UniTask の race 系指摘を出すときは、以下の一次資料を 1 つ以上引用する。引用が無い指摘は提案として弱い:

- `AsyncDestroyTrigger.cs:49-55` — OnDestroy で初めて Cancel が走る
- `UniTaskObservableExtensions.cs` — `TrySetResult` / `TrySetCanceled` の先着順 atomic
- Unity Manual: `Object.Destroy` — 遅延破棄の挙動（同フレーム末尾の `OnDestroy` 発火）

### フレーム内で起こる典型的な race シーケンス

`GameObject` の `Update()` 中にフレーム前半で閉じる UI の例:

1. Frame N, Update 中: `OnExit()` → `DestroyUI()` → `Destroy(gameObject)` が**キューされるだけ**
2. Frame N, 同じフレームの後続 PlayerLoop: ネットワーク受信 → `UniTask.SwitchToMainThread()` → `WaitSubject.OnNext(...)` → `await` 再開 → `_cached = result` の代入が走る
3. Frame N, end-of-frame: Unity が実際に破棄 → `AsyncDestroyTrigger.OnDestroy` → ct が Cancel される（**代入が済んだ後**）

つまり `await` 再開時点では ct はまだキャンセルされておらず、`ct.ThrowIfCancellationRequested()` はスルーしてしまい、代入は走る。**post-await のガードは、このシーケンスでは race を閉じない。**

### 逆パターン（OnDestroy が先、応答が後）

フレーム間で `OnDestroy` が先に走った場合は、`UniTaskCompletionSource` の `TrySetCanceled` が値の到着より先着して `OperationCanceledException` を投げ、await は再開しない。この場合は post-await ガードは不要。

## 可視副作用の有無チェック（Critical か Warning かの分水嶺）

race が実在しても、`await` 後にやっているのが以下だけなら、**可視な不具合はない**:

- C# フィールドへの代入のみ（メモリに書いて終わり、GC で回収される）
- Unity API を呼ばない（破棄済み GameObject の Component 参照にアクセスしない）
- 別オブジェクトの state に副作用を及ぼさない

これらが満たされていれば、race は「存在するが害がない」状態なので **Critical ではない**。Info 止まりか、そもそも出さないのが正解。

逆に以下の副作用がある場合は Critical:

- 破棄済み GameObject の Transform / Renderer / Component を触る
- 静的 state や別 singleton に書き込む（例: 破棄済み View の ID がキャッシュに残り、次回 open で誤動作）
- 外部リソース（ファイル / ネットワーク / Editor API）を触る

## 正攻法で閉じたいときの fix

post-await ガードではなく、**synchronous に Cancel できる CancellationTokenSource を View 側に持たせ、Destroy 前に同期 Cancel する**:

```csharp
private CancellationTokenSource _cts = new();

public void DestroyUI()
{
    _cts.Cancel();   // 同期でキャンセル（UniTaskCompletionSource.TrySetCanceled が即座に走る）
    _cts.Dispose();
    Destroy(gameObject);
}

private async UniTask FetchAsync()
{
    _cached = await SomeAsyncCall(_cts.Token);
    // ここに到達するのは _cts が未 Cancel のケースのみ。race は閉じている。
}
```

`GetCancellationTokenOnDestroy()` は Unity の遅延 Destroy に縛られるので、本当に閉じたい race では使わない。

## サブエージェントが Critical 化するときの判定フロー

1. race が実在するか？（コードを読む。`await` + `GetCancellationTokenOnDestroy` + Unity Destroy が揃うか）
2. await 後のコードに可視副作用があるか？（フィールド代入のみなら **Info 止まり**）
3. 提案する fix が上記「正攻法」と一致しているか？（post-await `ThrowIfCancellationRequested` 単体の提案は禁止）
4. 上の「仕様確認の出典まとめ」のうち 1 件以上を **指摘文中に明示的に引用** したか？

この 4 点を満たさないと Critical に上げてはならない。引用が出来ない＝仕様未確認なので、Warning 以下で出すか、そもそも出さない。

## 過去の false-positive 事例（再発防止）

このパターンが Critical で出されたら、**即座にこの事例を引用して Warning 以下に降ろす** こと。

### Case: `SubInventoryState.OnExit` → `FetchNetworkInfo` の Destroy race

```csharp
// SubInventoryView 側
private async UniTask FetchNetworkInfo()
{
    var ct = this.GetCancellationTokenOnDestroy();
    _cachedNetworkInfo = await ClientContext.VanillaApi.Response
        .GetGearNetworkInfo(_blockGameObject.BlockInstanceId, ct);
}

// SubInventoryState 側
public override void OnExit()
{
    _loadInventoryCts.Cancel();
    DestroyUI();   // 内部で Destroy(gameObject)
}
```

**過去に Critical で出された誤指摘**: 「`await` の直後に `ct.ThrowIfCancellationRequested()` を入れて Destroy race を閉じよ」

**なぜ誤りか**:

1. `Destroy(gameObject)` は遅延実行 → `OnDestroy` はフレーム末尾まで走らない → ct はまだ Cancel されていない
2. await 再開時点で ct を見ても通る → `ThrowIfCancellationRequested()` はスルー → 代入が走る
3. つまり post-await ガードでは race を閉じない

**かつ、このケースは Critical ではない**:

- await 後は `_cachedNetworkInfo = result` の C# フィールド代入のみ
- Unity API（破棄済み GameObject の Component / Transform / Renderer 等）は触らない
- `Update()` は破棄済み GO なので呼ばれない
- 静的 state や別 singleton への副作用なし
- → race は実在するが**可視副作用ゼロ**。Info 止まりが正解

**正攻法で閉じたい場合**（ユーザーがそれを目指していると明示している場合のみ提案）: View 側に `CancellationTokenSource _cts` を持たせ、`DestroyUI()` 内で `_cts.Cancel()` → `_cts.Dispose()` → `Destroy(gameObject)` の順で同期 Cancel する。`GetCancellationTokenOnDestroy()` は使わない。

**判定**: コンテキストの「目指さない」「許容するトレードオフ」に「post-await ガードは race を閉じないことは仕様確認済み」「副作用無しの race は許容する」が書かれていれば **指摘自体を出さない**。書かれていなくても、可視副作用無しなら **Info 止まり**。**コンテキストが空の場合は subagent-common-rules.md Step 0.36 が効き、本ファイルの Frame N シーケンスが「許容するトレードオフ」の初期値として自動採用される** → やはり Critical にしない。

### 2026-05-07T9 ユーザー直筆の再々々々強調（連続 2 回「再レビューで同じ指摘を受けないように」を直筆要求）

T8 補強後にもう 1 周 Pattern B が Critical 化された。ユーザー本人の処分文（逐語）:

```
少なくとも、これについてはCriticalではない。
これはどちらかと言うとUniTaskに関連する事項なので、別途subagent内部にUniTask関連の仕様
についてのドキュメントを参照するように明示するのが良いと思われます。
再レビューでこのFB観点が反映され、同じ指摘を受けないレビューになるようにしてください。
```

T8 と T9 で 2 回連続で「再レビューで FB 観点が反映され同じ指摘を受けないように」を直筆要求していることが最重要。verdict 引用だけで Warning/Info に残す経路が T8 まで残っていたため再発した。

T9 で追加した恒久強制（subagent-common-rules.md Step 0.38 と SKILL.md Step 1.05 で実装）:

1. **Step 0.38 即時 self-skip ゲート**: Pattern B 該当の指摘を組み立てている最中に、verdict block を逐語引用しつつ「破棄済み GameObject の Component / Transform / 静的 state / 外部リソースに副作用が及ぶ証拠」を Read/Grep から逐語引用できない場合、その指摘を **Critical/Warning/Info すべて削除** し `[T9-skip: pattern B verdict applied, no new evidence]` の 1 行のみ残す
2. **SKILL.md Step 1.05 再レビュー前事前検証チェックリスト**: メインが Agent 並列発火前に「Pattern B 該当キーワードがあるなら『許容するトレードオフ』欄に Pattern B verdict block を逐語コピペ済み」を確認するゲートを通す。verdict コピペが無ければ発火しない
3. 本ファイルへの T9 直筆保存（このセクション）。Pattern B の Critical 化を試みる前に必ず本セクションを読み返し、ユーザーが「Critical ではない」を 9 周連続で直筆処分済みである事実を確認する

**運用の鉄則（T9 確定）**: Pattern B 該当指摘を組み立てる前に「verdict 引用 ＋ 新規副作用証拠」のセットが揃わなければ、出力に残してはいけない（Critical/Warning/Info すべて削除）。verdict 引用は許可ではなく削除トリガー。

### 2026-05-07T8 ユーザー直筆の再々々強調（最重要・本ファイル参照を subagent 側で恒久強制）

T7 補強後にもう 1 周 Pattern B が Critical 化された。ユーザー本人の処分文（逐語）:

```
少なくとも、これについてはCriticalではない。
これはどちらかと言うとUniTaskに関連する事項なので、別途subagent内部にUniTask関連の仕様
についてのドキュメントを参照するように明示するのが良いと思われます。
再レビューでこのFB観点が反映され、同じ指摘を受けないレビューになるようにしてください。
```

T8 で実装した恒久強制:

1. `unitask-pattern-reviewer.md` の **front-matter description 冒頭** に「`references/unitask-cancellation-semantics.md` を Read することは本 reviewer の存在条件」と明記。description は subagent 起動時に必ずロードされるので、Read 命令が起動シーケンスより前に届く
2. 同 reviewer の **mandatory pre-read チェックリスト**（criterion 4 の前段）に「本ファイルを Read していない時点で Pattern B 系の出力（Critical/Warning/Info すべて）を組み立てる行為そのものを禁止」と追記
3. `bug-fix-intent-reviewer.md` criterion 6 / `architecture-lifecycle-reviewer.md` criterion 8 の冒頭に同等の「本ファイル Read が前提」マーカーを再強化
4. 本ファイル冒頭にも「UniTask race 系指摘は本ファイル Read を前提とする」を再宣言（後述）

**運用の鉄則**: Pattern B 該当の指摘を 1 件でも組み立てる前に、本ファイルの「ユーザー直筆の仕様確認」セクション（Frame N シーケンス）を確実に読み、Step 0.4 引用ゲートで逐語引用する。引用が貼れない＝Read 未実行＝指摘出力禁止（Info 含む全レベル）。

### 2026-05-07T6 ユーザー直筆の再々強調

T5 強化後にもう 1 周 Pattern B が Critical 化された。ユーザー本人の処分文（逐語）:

```
少なくとも、これについてはCriticalではない。
これはどちらかと言うとUniTaskに関連する事項なので、別途subagent内部にUniTask関連の仕様
についてのドキュメントを参照するように明示するのが良いと思われます。
```

T6 で実装した強制構造:

1. `unitask-pattern-reviewer.md` の **description フィールド本文** に「`unitask-cancellation-semantics.md` を Read 必須」を埋め込み（subagent 起動時に最初に読まれる箇所）
2. 同 reviewer の **起動シーケンス Step 2.5** に「成果物に UniTask 系キーワードがあれば Applicability check 前に本 doc を Read」を物理ステップとして追加
3. 同 reviewer の **criterion 4** で再度 Read 必須を明記し、Pattern B 必須インライン仕様（Frame N シーケンス）を本文中にコピー

description / 起動シーケンス / criterion 4 の三箇所のいずれかで doc を Read することになる。三度全て省略しないと Pattern B の Critical 出力は物理的に組み立てられない構造。

Pattern B を Critical 化しようとした瞬間に思い出すこと: ユーザー本人が **「Critical ではない」** と直筆で確定処分済み。これは推論ではなく一次資料。Critical で出すには「破棄済み GameObject の Component / Transform / 静的 state / 外部リソースに副作用が及ぶ」という別経路の証拠を Read / Grep から逐語引用する必要があり、それが無ければ **そもそも出さない**。

### 2026-05-07T5 ユーザー直筆の再強調

T4 まででインライン抜粋・引用ゲートを入れたが、再度同形を Critical 化した実績あり。ユーザー本人の処分文（逐語）:

```
少なくとも、これについてはCriticalではない。
これはどちらかと言うとUniTaskに関連する事項なので、別途subagent内部にUniTask関連の仕様
についてのドキュメントを参照するように明示するのが良いと思われます。
```

このメタ指示が示すのは「subagent の本文中で UniTask 仕様 doc への参照を **明示的に強制** せよ」。reviewer 側の対応:

- `unitask-pattern-reviewer.md` の起動シーケンスで Section 0 通過後・Applicability check より前に `unitask-cancellation-semantics.md` を Read することを **mandatory pre-read** として明記済み
- `bug-fix-intent-reviewer.md` criterion 6 / `architecture-lifecycle-reviewer.md` criterion 8 でも同 doc への参照を必須化済み
- 本 doc の **「ユーザー直筆の仕様確認」** セクション（Frame N シーケンス）が Pattern B 該当時の最強引用元として固定
- Step 0.36（共通ルール）でコンテキスト空時に Frame N シーケンスを「許容するトレードオフ」の初期値として自動採用

### ユーザー直筆の仕様確認（2026-05-07・上記 case の精密分析）

下のフレームシーケンスは過去にユーザー本人が UniTask ソースを直接読んで突き合わせた結論。Pattern B の Critical 化を判断するときは **以下を一次資料として引用すること**（推論ではなくユーザー検証済みの仕様）:

```
レースウィンドウは存在するが、提案された修正（await 後の ct.ThrowIfCancellationRequested()）では閉じない。

GetCancellationTokenOnDestroy の発火タイミング (AsyncDestroyTrigger.cs:49-55):
  void OnDestroy()
  {
      called = true;
      cancellationTokenSource?.Cancel();
      cancellationTokenSource?.Dispose();
  }
Unity の MonoBehaviour.OnDestroy() ライフサイクルで初めて Cancel される。

Unity の Destroy(gameObject) は遅延実行。end-of-frame まで OnDestroy は走らない。
DestroyImmediate なら同期だが、ここでは Destroy なので非同期。

ToUniTask のキャンセル vs 値到着のレース (UniTaskObservableExtensions.cs):
TrySetResult / TrySetCanceled の先着順。両方メインスレッドなので原子的。

実際のフロー:
  1. Frame N, Update 中: OnExit() → DestroyUI() → Destroy(gameObject) がキューされるだけ
  2. Frame N, 同じフレームの後続 PlayerLoop: ネットワーク受信 → UniTask.SwitchToMainThread()
     → WaitSubject.OnNext(...) → await 再開 → _cachedNetworkInfo = result 代入
  3. Frame N, end-of-frame: Unity が実際に破棄 → AsyncDestroyTrigger.OnDestroy
     → ct が Cancel（遅すぎる）

→ レースは実在する。「Destroy 後に await が値を返して代入が走る」シナリオは正しい。
  しかし await 後の ct.ThrowIfCancellationRequested() では閉じない。
  逆パターン（OnDestroy が先、応答が後）なら ToUniTask 側の TrySetCanceled が勝って
  OperationCanceledException が出るので、await 後のガードは不要。

このレースは有害か？
  FetchNetworkInfo の await 後は C# フィールドへの代入のみで Unity API は触らない。
  Update() は破棄済み GO なので呼ばれない。可視な副作用なし／NRE なし ＝ 実害ゼロ。

正しく閉じたいなら、View 自身で CancellationTokenSource を持って DestroyUI() の中で
同期 Cancel するのが正攻法（GetCancellationTokenOnDestroy() は使わない）。
```

**運用ルール**: Pattern B を Critical 化しようとした瞬間、上のブロックを読み返す。「post-await `ThrowIfCancellationRequested` を Critical で要求」「副作用ゼロの race を Critical 化」のいずれかをやろうとしているなら、それは過去 false-positive 化したパターンと同形。Warning 以下に降ろすか、そもそも出さない。
