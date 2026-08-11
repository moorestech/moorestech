---
name: safe-github-push
description: >
  GitHub無料リポジトリのサイズ制限を回避して安全にpushする。
  大きなバイナリアセットを含むリポジトリでもpush失敗を防ぐ。

  Use When:
  - GitHubにpushしたい
  - 大量のアセットやバイナリファイルをpushする必要がある
  - pushが「pack exceeds maximum allowed size」エラーで失敗した
  - リポジトリに大きなファイルが含まれている
---

# Safe GitHub Push

GitHub無料リポジトリの制限を回避して安全にpushするワークフロー。

## 課金安全ルール（最優先）

- **Git LFSは明示的なオプトインとする。** 課金が発生する可能性、対象ファイル、合計容量を説明した後、この会話内でユーザーがGit LFSの使用を明示承認した場合に限り使用できる。
- 「全部push」「未コミットも含めて」「大きいファイルも含めて」などの包括的な依頼は、Git LFS使用の承認とはみなさない。
- 明示承認前は `.gitattributes` の作成・変更、`git lfs track`、`git lfs migrate`、LFSオブジェクトのpushを禁止する。デフォルトはGit LFS不使用とする。
- 100MB超ファイルを検出した場合、その扱いが決まるまでコミットとpushを開始しない。

## 制限事項

- **ファイルサイズ**: 1ファイル100MB以下（超えるとpush拒否）
- **pushサイズ**: 1回のpush約2GB以下（超えると `pack exceeds maximum allowed size` エラー）
- **警告**: 50MB以上のファイルは警告が出るが、pushは成功する

## ワークフロー

### Step 1: 差分の把握

```bash
git status --short
git diff --stat
```

### Step 2: 100MB超ファイルの検出

未追跡・変更ファイルから100MB超を検出する。

```bash
# 未追跡ディレクトリのサイズ確認
du -sm <directory>

# 100MB超のファイルを検索
find <directory> -type f -size +100M
```

100MB超ファイルを1件でも検出したら、ここで**強制停止**する。

1. 対象ファイル、各サイズ、合計容量をユーザーに提示する
2. 通常Gitではpushできないことと、Git LFSには課金が発生する可能性があることを説明する
3. `.gitignore`に追加するか、未追跡のまま残すか、Git LFSを使うかを確認する
4. ユーザーの回答を待つ。回答前に `.gitattributes` の変更、コミット、pushを行わない

Git LFSを選択肢として提示してもよいが、使用できるのはユーザーが「Git LFSを使う」と明示承認した場合だけである。

### Step 3: コミット分割計画

各pushが**1.5GB以下**になるようコミットを分割する（2GB制限に余裕を持たせる）。

分割の優先順位:
1. **小ファイル群**（設定、メタデータ、ドキュメント）→ 最初にコミット＆push
2. **中サイズアセット**（テレインデータ、シーンファイル等）→ カテゴリ別に分割
3. **大ディレクトリ**（テクスチャ、スタンプ等）→ サブディレクトリ単位で分割
4. **コード変更**（スクリプト、シェーダー等）→ 最後にコミット＆push

`du -sm` でディレクトリサイズを確認し、1.5GB以下の塊に分ける。

### Step 4: コミット＆push（繰り返し）

各グループごとにコミットとpushを交互に行う:

```bash
git add <files>
git commit -m "説明"
git push origin <branch>
```

**重要**: 必ず1コミットごとにpushする。複数コミットをまとめてpushすると、packサイズが合算されて2GB制限に引っかかる。

push成功後、未pushコミットが残っていないか確認:
```bash
git log origin/<branch>..HEAD --oneline
```

### Step 5: push失敗時の対処

`pack exceeds maximum allowed size` エラーが出た場合:

1. 未pushコミットを `git log origin/<branch>..HEAD` で確認
2. 1コミットずつ個別にpush:
   ```bash
   git push origin <commit-hash>:<remote-branch>
   ```
3. それでも失敗する場合はコミットが大きすぎる。`git reset --soft HEAD~1` で戻し、さらに細かく分割して再コミット

### 注意事項

- `git push origin <hash>:<branch>` で特定コミットまでpush可能
- バイナリファイル（PNG, EXR, asset等）はgit packの圧縮効率が悪いため、実ファイルサイズより大きくなることがある
- pushに時間がかかる場合はバックグラウンド実行を検討
- `.DS_Store` 等のOS生成ファイルは除外する
