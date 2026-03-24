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

100MB超ファイルは以下のいずれかで対処:
1. `.gitignore`に追加
2. Git LFSで管理
3. コミットに含めず放置（ユーザーに確認）

### Step 3: コミット分割計画

各pushが**1.5GB以下**になるようコミットを分割する（2GB制限に余裕を持たせる）。

分割の優先順位:
1. **小ファイル群**（設定、メタデータ、ドキュメント）→ 最初にコミット＆push
2. **中サイズアセット**（テレインデータ、シーンファイル等）→ カテゴリ別に分割
3. **大ディレクトリ**（テクスチャ、スタンプ等）→ サブディレクトリ単位で分割
4. **コード変更**（スクリプト、シェーダー等）→ 最後にコミット＆push

`du -sm` でディレクトリサイズを確認し、1.5GB以下の塊に分ける。

### Step 4: コミット＆push（1つずつ順番に）

**必ず「1コミット → pushの完了を待つ → 次のコミット」の順で行う。** 複数コミットを先にまとめて作ってからpushしてはいけない。packサイズが合算されて2GB制限に引っかかる。

```bash
# グループ1（初回は -u でトラッキング設定）
git add <files>
git commit -m "説明"
git push -u origin <branch>
# ← push完了を確認してから次へ

# グループ2以降
git add <files>
git commit -m "説明"
git push origin <branch>
# ← push完了を確認してから次へ
# ... 繰り返し
```

**注意**: 初回pushでは必ず `-u` フラグを付ける。これがないとローカルのリモートトラッキングが設定されず、`git log origin/<branch>..HEAD` が `ambiguous argument` エラーになる。

pushはバックグラウンド実行せず、完了を待ってから次のコミットに進む。

push成功後、未pushコミットが残っていないか確認:
```bash
git fetch origin && git log FETCH_HEAD..HEAD --oneline
```

### Step 5: push失敗時の対処

`pack exceeds maximum allowed size` エラーが出た場合:

1. 未pushコミットを `git fetch origin && git log FETCH_HEAD..HEAD` で確認（`origin/<branch>..HEAD` はトラッキング未設定時にエラーになるため `FETCH_HEAD` を使う）
2. 1コミットずつ個別にpush:
   ```bash
   git push origin <commit-hash>:<remote-branch>
   ```
3. それでも失敗する場合はコミットが大きすぎる。`git reset --soft HEAD~1` で戻し、さらに細かく分割して再コミット

### 注意事項

- `git push origin <hash>:<branch>` で特定コミットまでpush可能
- バイナリファイル（PNG, EXR, asset等）はgit packの圧縮効率が悪いため、実ファイルサイズより大きくなることがある
- pushは必ずフォアグラウンドで実行し、完了を待ってから次のコミットに進む
- `.DS_Store` 等のOS生成ファイルは除外する
