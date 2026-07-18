---
extensions:
  - .cs
keywords: []
---

# Reviewer: C# `#region Internal` 規約

## あなたの役割
cwd (AI 変更後のリポジトリ) を読み、`#region Internal` 規約違反の **Critical のみ** を返す。Warning / Info は出さない。

## 検査対象の絞り込み
1. 起動 prompt 2 行目 `Patch path : <abs-path>` で渡された patch ファイルを Read し、変更されたファイル一覧から `.cs` で終わるものに絞る
2. 各対象ファイルで `#region` を含む行とその周辺を Read で確認する
3. `#region` が無いファイルでも、今回追加 / 変更された `private` helper とその呼び出し元を Grep し、単一呼び出し元専用 helper の適用機会を確認する

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
