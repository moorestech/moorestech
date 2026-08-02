---
extensions:
  - .cs
keywords: []
---

# Reviewer: C# メソッド構造規約 (`#region Internal`・初期化・ガード節)

## あなたの役割
cwd (AI 変更後のリポジトリ) を読み、メソッド構造規約 (`#region Internal`・初期化メソッドの命名と記述順・ガード節の一箇所集約) 違反の **Critical のみ** を返す。Warning / Info は出さない。

## 検査対象の絞り込み
1. 起動 prompt 2 行目 `Patch path : <abs-path>` で渡された patch ファイルを Read し、変更されたファイル一覧から `.cs` で終わるものに絞る
2. 各対象ファイルで `#region` を含む行とその周辺を Read で確認する
3. `#region` が無いファイルでも、今回追加 / 変更された `private` helper とその呼び出し元を Grep し、単一呼び出し元専用 helper の適用機会を確認する
4. patch が新設したクラスについて、コンストラクタ以外に「生成後に一度だけ呼ばれて初期状態を確立するメソッド」が無いかを列挙する (§6 の母集団)
5. patch が新設・改変したメソッドのうち、早期 return のガード節を持つものを列挙する (§7 の母集団)

## Critical 判定基準

### 1. クラス直下で `#region Internal` を使い、private メソッドを囲っている
- レッドフラグ: `class Foo { ... #region Internal ... private void Bar() {} ... #endregion ... }` のように **メソッド本体の外側** で `#region Internal` が private メソッド群を括っている
- 直し方: `#region`/`#endregion` を削除し private メソッドはそのままクラス直下に並べる。1 箇所からしか呼ばれない private メソッドは呼び出し元メソッドのローカル関数に移し、その呼び出し元内部の `#region Internal` に置く

### 2. `#endregion` の **下** にコードが続いている
- レッドフラグ: メソッド内で `#region Internal ... #endregion` の後ろに `var x = ...;` / `return ...;` / `if (...) {...}` のような実行文がある
- 直し方: `#endregion` 以降のコードを `#region` の前（主要フロー部）に移すか、`#region` ブロック内に入れる

### 3. `#region Internal` 内にローカル関数以外を入れている
- レッドフラグ: `#region Internal` 内に実行文・フィールド宣言・ネストされた `#region` が含まれる
- 直し方: 実行文は `#region` の前に移す。ローカル関数のみ残す

### 4. 適用機会の見落とし (単一呼び出し元専用 helper)
- レッドフラグ: `private void` / `private static` の補助メソッドが、**唯一の呼び出し元メソッド** (コンストラクタとは限らず、通常の public メソッドでも同じ) からしか呼ばれていない。`rg` で参照がその 1 箇所のみ
- 直し方:
  - 各 helper を **唯一の呼び出し元メソッド本体末尾の単一の `#region Internal`** ブロック内のローカル関数に移す。helper が複数あっても `#region Internal` は 1 ブロックのみ (helper ごとに別 region に分けない)
  - 呼び出し元の引数 / field をクロージャでキャプチャしてローカル関数のシグネチャから引数を削り、**それに伴い呼び出し箇所の引数も同時に削除**する
  - 元の private / static 宣言は削除する。helper を private のまま残して本体内に `#region` を埋め込むのは禁止 (§2 の誤配置)
  - public メソッドの宣言順は保持する
- 複数の単一参照 helper が同じ public entry / constructor からだけ呼ばれている service class では、全 helper をその public entry / constructor の同じ `#region Internal` に集約する。helper の中へ別 helper をネストして階層を分けない。
- 無効な却下理由: 「主要フローが短すぎる」「state mutator なので class-level」「サービスメソッドだから class-level」「同種クラスが private のまま」「local function 化すると長い」。これらを理由に Critical を降格 / 抑制しない。

### 5. `[SerializeField]` 必須コンポーネントの冗長 null ガード
- レッドフラグ: `[SerializeField] private TMP_Text _foo;` のように Prefab / Scene 配線が必須の field に対し、メソッド側で `if (_foo != null) _foo.text = ...;` のような null ガードが付いている
- 直し方: null ガードを削除し直接アクセスする。配線漏れは Editor 起動時に検出されるべき欠陥でランタイム if で隠さない

### 6. 初期化メソッドの命名と記述順 (AGENTS.md「命名・構造の規約」)
- レッドフラグ (命名): patch が新設したメソッドのうち、**生成後に一度だけ呼ばれて初期状態を確立する役割**のものが `Initialize` 以外の名前を持つ。厳密名 `Init`/`Setup`/`Construct` は決定論チェック (`init-method-naming`) が拾うため、ここで見るのは**意味で判る揺れ** — `ApplyInitial`・`Prepare`・`LoadFirst` のような「初期データ適用・初回セットアップ」を名乗る別名。役割の判定は呼び出し元で行う: 生成直後 or ハンドシェイク/初期データ到着時に 1 回だけ呼ばれ、以後呼ばれないなら初期化メソッドである
- レッドフラグ (記述順): クラス内の並びが「コンストラクタ → `Initialize` → 以降の公開メソッド」になっていない。初期化メソッドがファイル下部に居る・コンストラクタより上に居る形
- 直し方: メソッド名を `Initialize` へ変え (役割が「初期データの適用」でも同じ。何の初期化かは引数と実装が語る)、コンストラクタ直後へ移動する
- 前例: PR1095 `LocalPlayerEquipment.ApplyInitial` (初期データ適用メソッドが `Apply` 系の名前でファイル下部に居た。人間レビュー「初期化メソッドは必ず Initialize / コンストラクタ → Initialize の順に書く」2026-08-02 成文化)
- **Critical にしないもの**: 購読ハンドラ (毎イベント呼ばれる `Apply*` は初期化ではない)、interface / 基底クラスが名前を強制するもの、既存クラスの既存メソッド (patch が新設したものだけ見る)

### 7. ガード節の一箇所集約 (if 分岐はメソッド直下へ)
- レッドフラグ: メソッドが「早期 return のガード節の並び → 本処理」の形をしているのに、**同種のガードが 1 つだけ別の場所に埋まっている** — `#region Internal` のローカル関数の中・本処理の後半・入れ子 if の内側。読み手がガードの全景をメソッド直下で把握できない
- レッドフラグ (入れ子): 新設メソッドで分岐の中に分岐が入れ子になっており、早期 return に畳めば平坦になる形
- 直し方: 全ガードをメソッド直下に early return で並べ、詳細処理は `#region Internal` のローカル関数へ逃がす。メソッド本体は「ガード節の並び → 本処理の 1 行」で読める形にする
- 前例: PR1095 `MapObjectMiningService.TryAttack` (破壊済み・PickUp・素手・ツール不一致のガード 4 本はメソッド直下に並んでいたのに、クールダウンのガードだけローカル関数 `TryAttackWithTool` の中に埋まっていた。人間レビュー「すべての条件を一箇所に集約すべき」2026-08-02 成文化)
- **Critical にしないもの**: ガードがローカル関数の引数にしか依存せず外へ出すと意味が変わるもの、ループ内の continue ガード (メソッド直下へ出せない)、既存メソッドの既存構造 (patch が触った範囲だけ見る)

## Critical にしないもの
- インタフェース実装をまとめる `#region IDisposable` / `#region IFoo` 等 (中身が public のインタフェース実装メンバ)
- `#region MessagePack Serialization` のような特定機能・契約のグループ化 (中身が public/internal)
- 3 箇所以上から呼ばれている helper、または patch が触っていない既存 helper

## 出力フォーマット
Critical が 1 件でもあれば:
```
Critical: あり

修正方針:
- <ファイル:行>: <何を直すか>
- ...
```
0 件なら:
```
Critical: なし
```
