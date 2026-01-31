---
description: ghでPRを作成（日本語・差分確認→サマリー→PR作成）
---

# PR作成（差分チェック → サマリー作成 → ghでPR作成）

## 目次
- 重要な注意点（必ず遵守）
- 引数
- 事前情報（必ず取得して利用）
- 1) 差分がないかチェック（あればコミット）
  - 1-1. 未コミット差分の把握
  - 1-2. スキーマyamlファイルの判定ルール
  - 1-3. `_CompileRequester.cs` の扱い（最重要）
  - 1-4. コミット手順
- 2) 今までのブランチ差分をチェックしてサマリーを考える
  - 2-1. ベースブランチを決める
  - 2-2. 差分情報の取得（必ず）
  - 2-3. PRサマリー（日本語）を作る
- 3) ghコマンドでPRを作成する
  - 3-1. PR作成前チェック
  - 3-2. リモートへpush（force禁止）
  - 3-3. 既存PRがあるか確認し、作成 or 更新
- 実行時の注意（出力）

## 重要な注意点（必ず遵守）
- `_CompileRequester.cs` は **スキーマyamlファイルを変更していない限り、コミット不要**。
- ただし **スキーマyamlファイルを変更している場合は** `_CompileRequester.cs` も **同じコミットに含めてコミットする**。
- 破壊的な操作は禁止（force push / reset --hard / clean -fd / 共有ブランチへのrebase 等）。
- 不明点が出たら先に質問して止める。

---

## 引数
- `$ARGUMENTS` があれば **PRタイトルとして優先**（なければ差分から日本語タイトルを自動生成）

---

## 事前情報（必ず取得して利用）
- 現在ブランチ: !`git branch --show-current`
- originのデフォルトブランチ推定: !`git symbolic-ref --quiet --short refs/remotes/origin/HEAD || echo origin/main`
  - 上の出力が `origin/main` のように出るので、ベースブランチ名は `origin/` を除いたもの（例: `main`）
- 作業ツリー状況: !`git status --porcelain`
- 直近コミット: !`git log --oneline -20`

---

## 1) 差分がないかチェック（あればコミット）
### 1-1. 未コミット差分の把握
以下を必ず確認して、未コミット変更ファイル一覧を作る:
- !`git diff --name-status`
- !`git diff --name-status --cached`

### 1-2. スキーマyamlファイルの判定ルール
「スキーマyamlファイル」は、以下のどれかに該当する `.yml/.yaml` とする:
- パスまたはファイル名に `schema` / `schemas` が含まれる
- `schema/` または `schemas/` ディレクトリ配下
（該当が曖昧なら、変更された `.yml/.yaml` を列挙して私に確認する）

### 1-3. `_CompileRequester.cs` の扱い（最重要）
- 未コミット変更に `_CompileRequester.cs` が含まれていても、**スキーマyamlファイルの変更が無い場合はコミット対象に含めない**。
  - この場合、PR作成は「コミット済みの差分」だけで進める（未コミットの `_CompileRequester.cs` はPRに入らない）。
- スキーマyamlファイルの変更がある場合は、`_CompileRequester.cs` の変更も **必ず同じコミットに入れる**。

### 1-4. コミット手順
- 未コミット差分がある場合:
  1) コミット対象ファイルを決める（上記ルールで `_CompileRequester.cs` を除外/含める）
  2) `git add <対象ファイル>` を実行
  3) 1つのコミットにまとめて `git commit -m "<日本語コミットメッセージ>"` を実行
     - メッセージは「何をどう変えたか」が分かる日本語にする（例: `スキーマ更新に伴う生成コード更新` / `○○の不具合修正`）

- 未コミット差分が無い場合:
  - そのまま次へ進む

---

## 2) 今までのブランチ差分をチェックしてサマリーを考える
### 2-1. ベースブランチを決める
- `originのデフォルトブランチ推定` の出力から `origin/` を除いた値をベースブランチにする（例: `origin/main` → `main`）。
- 以降、ベースブランチを `<BASE>`、現在ブランチを `<HEAD>` として扱う。

### 2-2. 差分情報の取得（必ず）
- 差分統計: !`git diff <BASE>...HEAD --stat`
- 変更ファイル一覧: !`git diff <BASE>...HEAD --name-status`
- 差分内容（必要に応じて）: !`git diff <BASE>...HEAD`

### 2-3. PRサマリー（日本語）を作る
以下を作成する:
- **PRタイトル（日本語）**
  - `$ARGUMENTS` があればそれを使用
  - なければ差分から 1行で要点が分かるタイトルを生成
- **PR本文（Markdown、日本語）** を `./.tmp/pr-body.md` に書き出す
  - 形式:
    - 概要（2〜5行）
    - 変更点（箇条書き）
    - 影響範囲 / 注意点（あれば）
    - 動作確認（実施したこと / すべきこと）
    - 関連Issue/チケット（分からなければ私に質問）

---

## 3) ghコマンドでPRを作成する
### 3-1. PR作成前チェック
- ブランチにコミット済み差分があるか確認:
  - !`git diff <BASE>...HEAD --name-only`
- 差分が空なら **PRを作らず停止**（理由を説明）

### 3-2. リモートへpush（force禁止）
- 現在ブランチを `origin` に push:
  - `git push -u origin <HEAD>`

### 3-3. 既存PRがあるか確認し、作成 or 更新
- 既存PR確認:
  - `gh pr list --head <HEAD> --state open --json number,url,title`
- 既存PRがある場合:
  - `gh pr edit <number> --title "<TITLE>" --body-file ./.tmp/pr-body.md`
  - その後 `gh pr view <number> --web` 相当の情報（URL等）を表示
- 既存PRが無い場合:
  - `gh pr create --base <BASE> --head <HEAD> --title "<TITLE>" --body-file ./.tmp/pr-body.md`
  - 作成後に表示されるURLを出す

---

## 実行時の注意（出力）
- 実行した `git/gh` コマンドと結果を簡潔にログとして残す
- `_CompileRequester.cs` をコミット対象から外した場合は、その旨を明示する
