# サブエージェント共通ルール

このファイルは全レビューサブエージェントが最初に読む共通ルール。個別テンプレートより優先する。

## 0. 起動直後の BLOCKING GATE（criterion を読み進める前に必ず通す）

このゲートは **criterion 評価より先に走る**。ゲートを通さないまま Critical を 1 件でも出した場合、レビュー失敗として扱う（過去の false-positive 4 件は全て本ゲート未通過が原因）。

### Step 0.1: クラス名 / API 名のキーワードスキャン

レビュー対象のテキスト（成果物 + 過去レビュー出力）を以下のキーワードで grep 相当に走査する:

| キーワード | 該当時の必須行動 |
|---|---|
| `GearNetworkDatastore` / `Initialize(I*Protocol)` / `private static * _instance` を含むクラスが DI で依存を受け取る | **Pattern A** に固定マッチ。**Critical / Warning / Info すべて出力禁止**（ユーザー本人が 2026-05-07T6 で「これを実施すべきではない」「subagent のレビュー観点調整対象」と確定処分済み）。出力する場合の上限は「pass 扱いで黙る」。Step 0.35 の Pattern A verdict block を心の中で読み、新規証拠（注入先が transient/scoped 等）が無ければ無出力 |
| `GetCancellationTokenOnDestroy()` / `await * ToUniTask` / `UniTaskCompletionSource` + Unity Destroy | **Pattern B** に固定マッチ。**この時点で `references/unitask-cancellation-semantics.md` を Read 必須**。Read 完了前に B 系の指摘を一切組み立ててはならない（Info を含む）。Read 後も Critical 化は Step 0.4 引用ゲートで Frame N シーケンス（`AsyncDestroyTrigger.cs:49-55` + `UniTaskObservableExtensions.cs` 先着順）を逐語引用できることが必須 |
| `SimpleGearService.isChanged` / push ゲート / 単一サイトのガード追加 | **Pattern C** に固定マッチ。「根本修正されていない」「症状パッチである旨を明示せよ」「上流欠陥が未修正のまま残存」を Critical / Warning 化禁止。Info も 1 行 disclaimer 必須（「コンテキストにトレードオフが明示されれば消える指摘」） |
| `OnEnter` / `OnAttach` で 1 回 fetch、UI 表示中の再 fetch 無し | **Pattern D** に固定マッチ。「ネットワーク分断 / マージで不変条件を破る」「UI 表示中に古いキャッシュ」「鮮度問題」を Critical / Warning 化禁止。Info も 1 行 disclaimer 必須 |

固定マッチした項目は **下のテーブル（4 パターン）の既定挙動が無条件に適用される**。コンテキスト 4 カテゴリが空でも、今回の議論で明示的にトレードオフ合意が無くても、本セクションが優先する。

### Step 0.2: ゲート通過チェックリスト

Critical 候補を 1 件でも出す前に、以下を全て yes にする:

- [ ] Step 0.1 のキーワードスキャンを実行した
- [ ] マッチしたパターンについて、対応する一次資料（`unitask-cancellation-semantics.md` / `architecture-lifecycle-reviewer.md` criterion 6.1 / `bug-fix-intent-reviewer.md` criterion 2 / 本ファイルの 4 パターン表）を **その場で Read** した
- [ ] Critical に上げるための「追加証拠」を実際にコードから引用できる（推論ではなく文字列として引ける）

1 つでも no があれば、その指摘は **Warning 以下に降ろす**。

### Step 0.3: 二段階セルフ検証（Critical を返す直前）

出力直前にもう一度走らせる:

1. 各 Critical について「これは Pattern A〜D のどれかに該当しないか？」を再質問する
2. 該当するなら、降格して理由（`[pattern X applied: ...]`）を 1 行添える
3. 全 Critical について「追加証拠の引用」が出力に含まれているか？無ければ Warning に降ろす

このゲートに違反した出力は self-reject して再生成する。

### Step 0.35: Canonical user verdict block（A/B/C/D 共通の最強引用元）

メインから 4 カテゴリコンテキストが渡らなかった場合でも、以下の **ユーザー直筆の処分文（2026-05-07T2 / T4 一次資料）** を「黙示の合意」として扱い、対応する Pattern を Critical に上げない。Step 0.4 の引用ゲートではこのブロックを 1 件以上逐語で貼ること。

```
[Pattern A — 2026-05-07T2 / T4 ユーザー直筆]
別にこれはこれでOKです。なぜかと言うと、GearNetworkDatastore 自体がシングルトンとして設計、運用
されているからです。そのため、static シングルトン運用を基本としているインスタンスに対して、一部
DI コンテナによる解決のほうが不可解です。そのため、これを実施すべきではありません。
これは、subagent のレビュー観点調整対象です。

[Pattern D — 2026-05-07T2 / T4 ユーザー直筆]
これは仕様として許容している範囲内です。
これは subagent に何を目指し、何を目指さないか、何をトレードオフにしたかが明示されていなかった
のが原因です。subagent 作成時のコンテキスト受け渡しの問題です。

[Pattern C — 2026-05-07T2 / T4 ユーザー直筆]
これも UI オープン時 1 回と同じく、必要なトレードオフや目指すこと、目指さないことが伝わって
いないのが原因です。

[Pattern B — 2026-05-07T2 / T4 ユーザー直筆]
少なくとも、これについては Critical ではない。これはどちらかと言うと UniTask に関連する事項
なので、別途 subagent 内部に UniTask 関連の仕様についてのドキュメントを参照するように明示する
のが良いと思われます。
（一次資料の Frame N シーケンスは unitask-cancellation-semantics.md「ユーザー直筆の仕様確認」
セクションを参照）
```

このブロックは「コンテキストが空でも、過去にユーザー本人が処分理由を逐条で開示済みである」事実を保存している。Pattern X の Critical/Warning を組み立てる前に、対応する行を読んでなお Critical で出す合理性があるかを自問する。多くの場合は **Info 降格 か出力しない** が正解。

### Step 0.36: コンテキスト空時の自動 verdict 適用（T5 追加・最重要）

メインから渡されたプロンプトの「目指す / 目指さない / 許容するトレードオフ / 尊重すべき制約」4 カテゴリの **本文が空または欠落** している、かつ Step 0.1 のキーワードスキャンで Pattern A/B/C/D のいずれかに固定マッチした場合、**Step 0.35 の Canonical user verdict block の対応行を、欠落しているカテゴリの初期値として自動採用する**。判定方法:

1. プロンプト中に「## 目指さない（非目標）」「## 許容するトレードオフ」「## 尊重すべき制約」のいずれかの見出しがあるが、対応する本文が空（見出し直下に文字列なし、または `${...}` プレースホルダのまま）→ 欠落とみなす
2. 4 カテゴリの見出し自体が無い → 全カテゴリ欠落とみなす
3. Step 0.1 のキーワードスキャンで Pattern X が固定マッチした場合、対応する Step 0.35 verdict block を該当カテゴリの初期値として読む:
   - **Pattern A** マッチ → 「尊重すべき制約」に Pattern A verdict を初期値として読み込む（static singleton + DI 受け取りは合意済み制約）
   - **Pattern B** マッチ → 「許容するトレードオフ」に Pattern B verdict を読み込む（post-await ガードでは閉じない / 副作用ゼロは許容）
   - **Pattern C** マッチ → 「目指さない」に Pattern C verdict を読み込む（単一サイト症状パッチの根本残存指摘は非目標）
   - **Pattern D** マッチ → 「目指さない」に Pattern D verdict を読み込む（UI 表示中の自動更新は非目標）
4. 自動採用したら、出力の冒頭にメタ行を 1 行添える: `[implicit-verdict-applied: A,B,C,D のうち該当パターン]`
5. 自動採用後は、該当パターンを Critical/Warning に上げる前に Step 0.4 の引用ゲートを通す。Step 0.35 verdict block を引用した時点で「ユーザー本人が処分済み」のため、原則 **そもそも出力しない**（出すなら Info、かつ 1 行で「コンテキストにトレードオフが明示されれば消える指摘」と添える）

**運用ルール**: 「メインがコンテキストを埋めなかったから出した」を言い訳として Critical を出すのは禁止。Step 0.36 の自動採用が効くため、コンテキスト空 ＝ verdict block 適用済み、と扱う。逆向きに「verdict block 採用したのにメインが新しい証拠を別途渡した」場合のみ Critical 化を検討する。

このゲートが追加された理由: T2/T4 で verdict block と引用ゲートを入れたが、メイン側がコンテキストを埋め忘れた瞬間に subagent が「合意なし」と解釈して既定挙動の Critical を出していた（T5 で同 4 件再発）。Step 0.36 で「コンテキスト空 = verdict 自動適用」という物理的強制を入れることで、メイン側の埋め忘れに依存せず再発を抑える。

### Step 0.37: 出力直前の語彙ブラックリスト強制スキャン（T7 追加・最重要）

出力を返す直前に、**指摘文の本文に含まれる語句を以下のブラックリストと照合する**。1 つでもマッチしたら、対応する Step 0.35 verdict block の逐語引用が同じ指摘文中にあるかを確認する。引用が無ければ **その指摘を出力から削除する**（降格ではなく削除）。

| パターン | 出力中に検出したら危険な語（部分一致で判定） |
|---|---|
| **A** | `DI 経由`, `ライフサイクル不整合`, `static と DI の混在`, `ホストも DI 化`, `DI を剥がせ`, `DI コンテナで Resolve すべき`, `シングルトン寿命の整合性`, `static singleton の運用と不整合` |
| **B** | `ThrowIfCancellationRequested を await 後に追加`, `post-await でガード`, `Destroy race を閉じよ`, `キャンセル後の代入を防げ`, `await 後に ct チェックを入れよ`, `GetCancellationTokenOnDestroy 由来の ct を信頼するな` |
| **C** | `根本欠陥が未修正のまま残存`, `上流欠陥が残っている`, `症状パッチである旨を明示せよ`, `push ゲート側の欠陥を修正せよ`, `isChanged 判定を直せ`, `SimpleGearService.isChanged 判定の不備` |
| **D** | `ネットワーク分断`, `マージで不変条件を破る`, `UI 表示中に古いキャッシュ`, `鮮度問題`, `再 fetch を入れよ`, `OnEnter で 1 回だけ取得は危険`, `1 度だけ取得は問題` |

**運用ルール**:

1. 検出語句が出ているのに verdict block 逐語引用が無い → その指摘 1 件を **削除**（Warning でも Info でも削除。降格ではない）
2. 検出語句が出ていて verdict block 逐語引用が **ある** → 「verdict と矛盾する新規証拠」を別途引用しているかを確認。矛盾しない（= verdict をそのまま追認している）なら指摘を出す意味が無いので削除
3. 検出語句が無くても、Pattern X シグネチャ（コード位置 / 文脈）に該当するなら Step 0.4 引用ゲートを通す
4. **Pattern A は語彙マッチした瞬間に削除**（criterion 6.1 で出力禁止が確定済み。verdict 引用があっても出力しない。Critical/Warning/Info すべて）

**この語彙ブラックリストが追加された理由**: T2/T4/T6 で verdict block と引用ゲートを入れたが、subagent が「ルールに違反していないつもり」で同形の指摘を別の言い回しで出していた。語彙レベルの物理マッチを最終ゲートに置くことで、ルールを読み飛ばしても出力に出る瞬間に検出する。語彙ブラックリストは ルール文より誤検出に強い（False Negative を出さない）。

### Step 0.38: T9 即時 self-skip ゲート（T9 追加・最重要）

Step 0.37 の語彙ブラックリスト通過後、**指摘 1 件ごとに**以下を実行する。1 件でもマッチしたら、その指摘の出力を組み立てる作業を中断し、**評価ループから即時 escape して `[T9-skip: pattern X verdict applied, no new evidence]` の 1 行のみを残す**。

判定フロー:

1. その指摘が Pattern A/B/C/D のどれかに固定マッチするか（コード位置・キーワード・指摘の趣旨）を判定
2. マッチした場合、Step 0.35 verdict block の対応行を**新規証拠と照合**する:
   - **新規証拠の例**: Pattern A なら「注入される依存が transient/scoped 寿命であること」を Read/Grep で確認し逐語引用、Pattern B なら「破棄済み GameObject の Component / Transform / 静的 state / 外部リソースに副作用が及ぶ証拠」を Read/Grep で確認し逐語引用、Pattern C なら「同欠陥が他経路でも発火し実害が出る具体ケース」を Read/Grep で確認し逐語引用、Pattern D なら「UI が長時間表示される運用前提があり fetch 前提が壊れる具体シナリオ」を Read/Grep で確認し逐語引用
   - **新規証拠ゼロ**: その指摘は前回の verdict をそのまま追認しているだけなので **指摘自体に意味が無い**。Critical/Warning/Info すべて出力削除
3. 削除した指摘は出力本文には載せず、出力末尾のみに `[T9-skip: pattern X verdict applied, no new evidence]` の 1 行ログを残す（複数該当しても 1 行に集約）

**T9 ゲートが追加された理由**: T2/T4/T6/T7/T8 で verdict block・引用ゲート・自動採用・語彙ブラックリストを 5 段重ねたが、subagent が「Step 0.4 引用ゲートを通すために verdict block を逐語引用しつつ、それを根拠に Critical を Warning に降ろすだけで Info/Warning として出力に残す」経路で同形指摘が再発し続けた。T9 では「verdict 引用 ＋ 新規証拠ゼロ ＝ 指摘そのものを削除」を強制し、Pattern X の出力経路を完全に閉じる。

**運用の鉄則**: verdict 引用は出力残留の許可ではなく **削除トリガー**。verdict を引用すべき場面なら、それは「ユーザー本人が処分済み」の場面なので、新規証拠が無い限り出力に残してはいけない。

### Step 0.4: 一次資料の逐語引用ゲート（A/B/C/D 関連で Critical/Warning を出す場合は必須）

Pattern A/B/C/D のいずれかに **少しでも触れる** 指摘（Critical または Warning）を出す場合、対応する一次資料からの **逐語引用** を指摘文中に含めなければならない。要約・パラフレーズ・「〜と書かれている」の参照表現は **不可**。原文をそのまま貼る。

| パターン | 引用元（最低 1 箇所。**Step 0.35 Canonical user verdict block の対応行を最優先**で貼る） |
|---|---|
| A: static singleton + DI 受け取り | Step 0.35 Pattern A ブロック、`architecture-lifecycle-reviewer.md` criterion 6.1 の「例（Critical にしてはいけない）」ブロック、または本ファイル Pattern A 行 |
| B: `await` + `GetCancellationTokenOnDestroy` race | Step 0.35 Pattern B ブロック、`unitask-cancellation-semantics.md` 「ユーザー直筆の仕様確認」セクションの Frame N シーケンス（`unitask-pattern-reviewer.md` criterion 4 のインライン抜粋でも可） |
| C: 単一サイト症状パッチの根本未修正再フラグ | Step 0.35 Pattern C ブロック、`bug-fix-intent-reviewer.md` criterion 2 の「Critical 化しない条件」ブロック、または本ファイル Pattern C 行 |
| D: UI open 1 回 fetch | Step 0.35 Pattern D ブロック、または本ファイル Pattern D 行 |

**運用**: 引用を貼れない（doc を Read していない・該当行が見つからない・貼ると自分の指摘と矛盾する）場合、その指摘は Pattern X として false-positive 候補なので、**自動的に Info に降ろす**か、そもそも出さない。引用を貼ること自体が「仕様を確認した上で例外的に Critical 化している」証跡になる。

このゲートが追加された理由: 過去に「Read した／覚えている」と称して引用を省略した結果、同じ false-positive を再発させた実績がある（2026-05-07 の 4 件同時 Critical 化、2026-05-07T3 の再再発）。引用必須にすることで、Read を省略した状態では物理的に Critical を組み立てられないようにする。

---

## 出力の簡潔さ

トークン消費を最小化する。

- **スコープ外**: `⏭️ skip: {一行で理由}` とだけ返す。それ以外何も書かない。
- **スコープ内だが指摘ゼロ**: `✅ pass` とだけ返す。それ以外何も書かない。
- **指摘あり**: 個別テンプレートの出力形式に従う。ただし上限400語を厳守。

## 合意済みトレードオフの尊重（全エージェント共通・最優先）

メインエージェントから渡されるコンテキストを **「目指す」「目指さない / 非目標」「許容するトレードオフ」「尊重すべき制約」** の 4 カテゴリに分解して読む。このうち **非目標・許容トレードオフ・尊重すべき制約** に該当する挙動を Critical として再フラグしない。

コンテキストに明示されている例:

- 「UIオープン時に1回だけ取得。UI表示中は更新しない」と宣言 → 「UI表示中にネットワーク構成が変わると古いキャッシュのまま」を Critical として出すのは **二重指摘**。既に合意済みの仕様なので Info 以下に抑えるか、そもそも出さない。
- 「これは症状パッチであり、根本修正は別 issue 化」とユーザー了承済みで提示 → 「症状パッチである旨を明示せよ」を Critical として出すのは **二重指摘**。
- 「既存の static singleton パターンを踏襲する」「後方互換ハック禁止・未使用コード削除」の方針宣言 → 該当パターンから離脱する提案は却下理由として扱う。

ただし以下は Critical にしてよい:

- 合意済みトレードオフの**境界を越えて**別の未検証のリスク/挙動が混入している場合（例: 「UIオープン時1回」と言いつつ実装は毎フレームpoll、など合意とコードが乖離している）
- 合意済みトレードオフを達成するためのコード自体に実装ミスがある場合
- コンテキストで触れられていない新しい観点（例: スキーマの直交性違反）

**コンテキストに何も書かれていないときの振る舞い:**
トレードオフ/非目標欄が空ならば、合意が取れていないと仮定して通常通りレビューする。ただし Critical には **「コンテキストでトレードオフが明示されれば Info に落ちうる」と 1 行添える** こと。これによりメインエージェントは「次回はコンテキストに書けば下がる」ことを学習できる。

### コンテキスト欠落時の保守的フォールバック（既知の false-positive 4 パターン）

メインがコンテキスト 4 カテゴリを埋め忘れていても、以下のパターンは **黙示の合意 / 既定の制約** として扱い、追加証拠なしに Critical 化しない（最大でも Warning）。理由は過去のレビューで何度も false-positive を出している実績パターンだから。

| パターン | 既定挙動 | Critical に上げるための追加証拠 |
|---|---|---|
| **A. static singleton ホストが singleton 寿命の依存を DI で受け取る** （`private static Foo _instance` パターンのクラスが Initialize / コンストラクタ / Resolve で他の singleton を受け取る） | 不整合扱いしない | 注入される依存が transient / scoped 寿命であることを Read / Grep で確認した上で、寿命ミスマッチの具体ケース |
| **B. `await someApi(ct)` の post-await ガード欠落** （ct が `GetCancellationTokenOnDestroy()` 由来） | post-await `ThrowIfCancellationRequested` で race を閉じられない仕様（unitask-cancellation-semantics.md）。await 後がフィールド代入のみなら Info 止まり | 破棄済み GameObject の Component / 静的 state / 外部リソースに副作用が及ぶ証拠 + 一次資料の引用 |
| **C. 単一呼び出しサイトの限定的な症状パッチ** （ローカルガード追加 / 局所フラグ / 特定条件のスキップ） | 「症状パッチである旨を明示せよ」を Critical で出さない。さらに「この症状パッチは push ゲート側の根本欠陥（`isChanged` 判定など）を残している」を Critical で再フラグしない（根本修正は別 issue 化が前提） | 同じ欠陥が修正対象外の他の経路でも発火する証拠と、その経路で実害が出る具体ケース |
| **D. UI open 時 1 回 fetch（自動更新無し）** （`OnEnter` / `OnAttach` で 1 度だけ取得、UI 表示中は再取得しない） | 「UI 表示中の鮮度問題」「ネットワーク分断/マージで不変条件を破る」を Critical で出さない（仕様として許容範囲内） | UI が長時間表示される運用前提があり、その間に fetch の前提が壊れる具体シナリオの証拠 |

**T5 再発の追記（2026-05-07T5）**: T2/T4 で verdict block + 引用ゲートを入れた後、もう 1 周同 4 件（A/B/C/D）が Critical 化された。原因は **メインのコンテキスト 4 カテゴリ未記入** が継続しており、subagent 側が空コンテキストを「合意なし」と解釈して既定 Critical を出したこと。T5 で **Step 0.36（コンテキスト空時の自動 verdict 適用）** を追加し、コンテキストが空でもキーワードマッチさえすれば verdict が初期値として読み込まれるように物理的に強制した。

**T9 再発の追記（2026-05-07T9・最重要）**: T8 補強後にもう 1 周同 4 件（A/B/C/D）が Critical 化された。ユーザーから再度「**再レビューでこの FB 観点が反映され、同じ指摘を受けないレビューになるようにしてください**」を直筆で要求。T9 で追加した強制構造（Step 0.38 と SKILL.md Step 1.05）は、verdict 引用 ＋ 新規証拠ゼロの組み合わせを物理的に削除する最終ゲート。さらにメイン側に「再レビュー前事前検証チェックリスト」を新設し、4 カテゴリ未記入のままの発火を物理的に止める。

ユーザー直筆 verdict（2026-05-07T9・前回までと完全同趣旨）:
- Pattern A: 「`GearNetworkDatastore` 自体がシングルトンとして設計、運用されている。**そのため、これを実施すべきではありません。これは、subagent のレビュー観点調整対象です**」
- Pattern D: 「これは仕様として許容している範囲内です。**subagent 作成時のコンテキスト受け渡しの問題です**」
- Pattern C: 「これも UI オープン時 1 回と同じく、必要なトレードオフや目指すこと、目指さないことが伝わっていないのが原因です」
- Pattern B: 「**少なくとも、これについては Critical ではない。これはどちらかと言うと UniTask に関連する事項なので、別途 subagent 内部に UniTask 関連の仕様についてのドキュメントを参照するように明示するのが良いと思われます**」
- 共通: 「**再レビューでこの FB 観点が反映され、同じ指摘を受けないレビューになるようにしてください**」（T8 と T9 の連続 2 回直筆）

**T8 再発の追記（2026-05-07T8・最重要）**: T7 補強後にもう 1 周同 4 件（A/B/C/D）が Critical 化された。今回ユーザーから「**再レビューでこの FB 観点が反映され、同じ指摘を受けないレビューになるようにしてください**」と明示的なテストフロー要求が追加された。T8 で追加した強制構造:

1. `unitask-pattern-reviewer.md` の **front-matter description 冒頭** に「`references/unitask-cancellation-semantics.md` Read は本 reviewer の存在条件」と明記（起動時に必ずロードされる箇所に Read 命令を埋め込み、起動シーケンスより前にトリガーする）
2. 同 reviewer の mandatory pre-read チェックリストに「Read 未実行時は Pattern B 系の出力を組み立てる行為そのものを禁止（Info 含む全レベル）」「Frame N シーケンス 1〜3 行目を逐語コピーできる状態を Read 完了の証跡とする」を追加
3. `unitask-cancellation-semantics.md` 冒頭に「本ファイル Read は Pattern B 系指摘の前提条件」を再宣言ブロックとして追加
4. T8 以降は **Read 命令を 5 箇所で重ねがけ**（description / 起動シーケンス Step 2.5 / mandatory pre-read / criterion 4 / セルフチェック Step G）。5 全てで Read を省略しないと Pattern B 系の出力は物理的に組み立てられない

ユーザー直筆 verdict（2026-05-07T8・前回までと同趣旨 + テストフロー要求）:
- Pattern A: 「`GearNetworkDatastore` 自体がシングルトンとして設計、運用されている。**そのため、これを実施すべきではありません。これは、subagent のレビュー観点調整対象です**」
- Pattern D: 「仕様として許容している範囲内。**subagent 作成時のコンテキスト受け渡しの問題です**」
- Pattern C: 「必要なトレードオフや目指すこと、目指さないことが伝わっていないのが原因」
- Pattern B: 「**少なくとも、これについては Critical ではない。これはどちらかと言うと UniTask に関連する事項なので、別途 subagent 内部に UniTask 関連の仕様についてのドキュメントを参照するように明示するのが良いと思われます**」
- 共通: 「**再レビューでこの FB 観点が反映され、同じ指摘を受けないレビューになるようにしてください**」

**T7 再発の追記（2026-05-07T7・最重要）**: T6 補強後にもう 1 周同 4 件（A/B/C/D）が Critical 化された。語彙ブラックリスト（Step 0.37）が追加された経緯。**T7 以降は Step 0.37 の語彙ブラックリストが最終ゲートとして全 reviewer の出力を物理的に検閲する**。語彙マッチした指摘は verdict 引用が無ければ削除。Pattern A は語彙マッチした瞬間に強制削除（Critical/Warning/Info すべて）。

ユーザー直筆 verdict（2026-05-07T7・前回と同趣旨）:
- Pattern A: 「`GearNetworkDatastore` 自体がシングルトンとして設計、運用されている。**そのため、これを実施すべきではありません。これは、subagent のレビュー観点調整対象です**」
- Pattern D: 「これは仕様として許容している範囲内です。**subagent 作成時のコンテキスト受け渡しの問題です**」
- Pattern C: 「これも UI オープン時 1 回と同じく、必要なトレードオフや目指すこと、目指さないことが伝わっていないのが原因です」
- Pattern B: 「少なくとも、これについては Critical ではない。**これはどちらかと言うと UniTask に関連する事項なので、別途 subagent 内部に UniTask 関連の仕様についてのドキュメントを参照するように明示するのが良いと思われます**」

**T6 再発の追記（2026-05-07T6）**: T5 補強後にもう 1 周同 4 件（A/B/C/D）が Critical 化された。ユーザー本人による T6 直筆処分文（前回と同趣旨だがより強い断定）:

- Pattern A: 「`GearNetworkDatastore` 自体がシングルトンとして設計、運用されている。static シングルトン運用を基本としているインスタンスに対して、一部 DI コンテナによる解決のほうが不可解です。**そのため、これを実施すべきではありません。これは、subagent のレビュー観点調整対象です**」
- Pattern D: 「これは仕様として許容している範囲内です。これは subagent に何を目指し、何を目指さないか、何をトレードオフにしたかが明示されていなかったのが原因です。**subagent 作成時のコンテキスト受け渡しの問題です**」
- Pattern C: 「これも UI オープン時 1 回と同じく、必要なトレードオフや目指すこと、目指さないことが伝わっていないのが原因です」
- Pattern B: 「少なくとも、これについては Critical ではない。**これはどちらかと言うと UniTask に関連する事項なので、別途 subagent 内部に UniTask 関連の仕様についてのドキュメントを参照するように明示するのが良いと思われます**」

T6 でユーザーが明示的に切り分けたこと:
1. **Pattern A は subagent 側のルール調整対象**（メインのコンテキスト渡し問題ではない）→ T6 で Step 0.1 Pattern A 行を「Critical 化禁止」から **「Critical/Warning/Info 全部出力禁止」** に格上げ。新規証拠が無ければ無出力
2. **Pattern C/D はメインのコンテキスト受け渡し不足側の問題**だが、subagent 側でも T6 で Step 0.1 行を「Critical 化禁止」から **「Critical/Warning 化禁止 + Info 出すなら 1 行 disclaimer 必須」** に格上げ
3. **Pattern B は UniTask 関連 doc を subagent 内部から明示参照**することが鍵。T6 で `unitask-pattern-reviewer.md` の起動シーケンスに「UniTask キーワード検出 → Applicability check 前に `unitask-cancellation-semantics.md` Read」を物理ステップ化。description にも明記

T6 以降に同形 Critical を出した場合は、subagent 側のルール失効として **レビュー失敗** 扱い。Step 0.1 に該当するキーワードを成果物中で見たら、criterion を読み進める前に本セクションを再読し、verdict と T6 強化分を踏まえた振る舞いに切り替える。

ユーザー直筆 verdict（2026-05-07T5・前回と同趣旨）:
- Pattern A: 「`GearNetworkDatastore` 自体がシングルトンとして設計、運用されている。static シングルトン運用を基本としているインスタンスに対して、一部 DI コンテナによる解決のほうが不可解。これを実施すべきではない」
- Pattern D: 「仕様として許容している範囲内」
- Pattern C: 「必要なトレードオフや目指すこと、目指さないことが伝わっていないのが原因」
- Pattern B: 「Critical ではない。UniTask に関連する事項なので、別途 subagent 内部に UniTask 関連の仕様についてのドキュメントを参照するように明示するのが良い」 → `unitask-pattern-reviewer.md` の Applicability check より前に「UniTask キーワード検出 → `unitask-cancellation-semantics.md` 強制 Read」のルートを既に強化。本ファイル Step 0.1 の Pattern B 行で `unitask-cancellation-semantics.md` を即時 Read 必須化済み

**過去の false-positive 事例（再発防止）**:

- **Pattern A の実例**: `GearNetworkDatastore` (static singleton) が `Initialize(IGetGearNetworkInfoProtocol)` で DI 経由のシングルトン依存を受け取る形。「DI と static の混在は不可解」「ライフサイクル不整合」を Critical で出した過去あり。両者とも singleton 寿命なので不整合無し。詳細は `architecture-lifecycle-reviewer.md` criterion 6.1
- **Pattern B の実例**: `SubInventoryView.FetchNetworkInfo` の `await ... GetCancellationTokenOnDestroy()` 後に `ct.ThrowIfCancellationRequested()` を要求した過去あり。Unity の `Destroy` 遅延実行の仕様で post-await ガードは race を閉じない。かつ await 後は C# フィールド代入のみで可視副作用ゼロ。詳細は `unitask-cancellation-semantics.md` 「過去の false-positive 事例」セクション
- **Pattern C の実例**: `SubInventoryState` 側のローカル症状パッチに対して「push ゲート (`SimpleGearService.isChanged` 判定) の欠陥が未修正のまま残存」を Critical で出した過去あり。今回のスコープが症状パッチで、根本修正は別 issue 化することがコンテキストに書かれていれば（または書かれていなくても単一サイト局所パッチなら）この指摘は出さない
- **Pattern D の実例**: UI オープン時 1 回 fetch に対して「ネットワーク分断/マージで不変条件を破る」を Critical で出した過去あり。これは仕様として許容している範囲内なので、長時間表示中に壊れる具体シナリオが提示されない限り Critical 化しない

**運用ルール**:

1. これらのパターンを検出したら、まず該当する一次資料（`unitask-cancellation-semantics.md`、`architecture-lifecycle-reviewer.md` criterion 6.1、`bug-fix-intent-reviewer.md` criterion 2 等）を Read してから出力レベルを判定する
2. 判定根拠を 1 行で添える（例: `[Info: パターン A 該当 — 注入元が singleton 登録なら 6.1 適用]`）
3. 「追加証拠なし」で Critical を出すと false-positive になる。証拠を集められないなら Warning 以下に降ろす
4. パターン該当を見落として Critical 化したケースはレビュー失敗として扱われる（過去 4 件の実績）
5. **Pattern B（`await` + `GetCancellationTokenOnDestroy`）に該当する変更を検出した瞬間、criterion を読み進める前に `unitask-cancellation-semantics.md` を Read する**。ここに先着したシーケンス図と `SubInventoryView.FetchNetworkInfo` の precedent があるので、ルールへの照合は **そのドキュメントを開いてから** 行う。「criterion を覚えているから Read 不要」は禁止 — 仕様は変わる、precedent は増える

## 既存アーキテクチャ整合性（全エージェント共通）

既存コードが **静的シングルトン / service locator / global / static initializer** のパターンで運用されている場合、**同じパターンを追従するのが正解** である。1 箇所だけ DI / factory / instance 注入に切り替える提案は、それ自体が「アーキテクチャが部分的に異質」という別のアンチパターンを生む。

Critical 化する前に次を確認する:

1. そのクラス/ファイルの定義で `private static Foo _instance;` などの static 保持を使っているか（Read で確認）
2. 他の呼び出し箇所（Grep で確認）が static API 経由で統一されているか
3. ユーザーが提示している変更は **単独の脱出ハッチ** なのか、それとも **全体のアーキテクチャ移行** の一部なのか

1,2 が yes かつ 3 が「単独の脱出ハッチ」であれば、「DI 化しろ」という提案は出さない。代わりに「既存の static パターンを踏襲せよ」を推奨として Info で出す（または何も言わない）。

**全体のアーキテクチャ移行**（= 該当クラス全体を DI 化する意図がある）である場合に限り、その方針への提言を出してよい。

**逆方向の鏡像ケース（同じく false-positive）**: 既存の static singleton クラスが、内部で **singleton ライフサイクルの依存** を DI で受け取っている形を「static と DI の混在」「ライフサイクル不整合」として Critical 化しない。両者とも singleton 寿命なら寿命は揃っており不整合は無い。DI は単なる解決手段の違いであってライフサイクル境界ではない。Critical 化前に注入先の登録方法を Read / Grep で確認し、singleton であることを確かめる。詳細は `architecture-lifecycle-reviewer.md` criterion 6.1。

## UniTask / 非同期キャンセルの race 系指摘は専用ドキュメントを参照する

`await` と `GetCancellationTokenOnDestroy()` / `UniTaskCompletionSource` / Unity の `OnDestroy` 発火タイミングが絡む race 指摘を出す前に、必ず **[unitask-cancellation-semantics.md](unitask-cancellation-semantics.md)** を Read して仕様を確認する。

このファイルに書かれている通り、たとえば `await` 後に `ct.ThrowIfCancellationRequested()` を差すだけでは多くの race は閉じない。また **可視副作用が無い race は Critical ではない**（Warning / Info 止まり）。仕様を読まずに「await 後に ct チェックを入れよ」を Critical で出すのは false-positive になりやすいので禁止する。
