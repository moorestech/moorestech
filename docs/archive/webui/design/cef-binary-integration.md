# CEF バイナリ恒久統合

**決定日**: 2026-07-18  
**対象**: Web UI 移行 Phase A1 / INFRA-1

## 決定

`jp.juha.cefunity` の Git URL 参照を維持し、Git LFS のセットアップ自動化と Unity Editor の検証ゲートを組み合わせる。
CEF バイナリは本リポジトリへコミットしない。開発者は Unity を初めて開く前に OS 別セットアップスクリプトを1回実行する。

- macOS / Linux: `./scripts/setup-cef.sh`
- Windows PowerShell: `.\scripts\setup-cef.ps1`

スクリプトは Git と Git LFS の存在確認、`git lfs install`、既存の `Library/PackageCache/jp.juha.cefunity@*` の削除を行う。
その後 Unity を開くと、UPM が manifest と packages-lock の固定内容に従ってパッケージと LFS 実体を再取得する。

## 選択肢の比較

| 方式 | リポジトリサイズ | worktree 運用 | Windows | 運用コスト | 判定 |
|---|---|---|---|---|---|
| (a) embedded package | バイナリを含めると各 clone/worktree が巨大化する。除外すると別取得機構が必要 | worktree ごとの巨大な配置または同期処理が必要 | Unity上は同形だが配布問題が残る | 中〜高 | 不採用 |
| (b) tarball / registry / バイナリ別取得 | 本体リポジトリは小さい | キャッシュ共有を設計できる | OS別成果物の配布に適する | 配布先、認証、version/hash、障害対応を新設する必要がある | 将来候補 |
| (c) Git URL + 自動セットアップ + 検証ゲート | 現状維持。巨大バイナリの直コミットなし | 各 worktree の UPM キャッシュを安全に再生成できる | 同じ Git LFS 経路を PowerShell で初期化できる | 既存配布元と lock を継続利用でき最小 | **採用** |

## 決定根拠

真因は UPM 自体ではなく、開発環境で Git LFS のグローバル smudge filter が未登録だったことである。
したがって既存の配布経路を置き換えず、前提を機械的に構築して壊れたキャッシュを捨てる方式が最短で根本原因を除去する。

Git URL と `Packages/packages-lock.json` の hash が依存の単一ソースであり続けるため、独自の version 対応表やバイナリ保管先を増やさない。
worktree ごとの `Library` は生成物なので、対象パッケージだけを削除して再解決しても他 worktree や追跡ファイルへ影響しない。
Windows も Git for Windows と Git LFS を前提に同じ取得経路を使え、独自アーカイブの展開差異を持ち込まない。

## 実装

### セットアップ

`scripts/setup-cef.sh` と `scripts/setup-cef.ps1` は次の順に処理する。

1. `git` が利用可能か確認する。
2. `git lfs version` で Git LFS が利用可能か確認する。
3. `git lfs install` を実行し、UPM の内部 clone にも適用される smudge filter を登録する。
4. 当該 worktree の `moorestech_client/Library/PackageCache/jp.juha.cefunity@*` だけを削除する。
5. Unity を開いて UPM 再解決するよう案内する。

スクリプトは繰り返し実行できる。Unity 起動中の PackageCache 削除を避けるため、実行前に Unity を終了する。

### Unity Editor 検証ゲート

`Assets/Scripts/Editor/Cef/CefPackageLfsValidator.cs` は Editor 起動時に対象 PackageCache を検査する。
1 KiB 以下のファイルだけを対象に先頭を読み、`version https://git-lfs` と一致した場合は、そのセッションで一度だけ Error を出す。
エラーには検出ファイルと OS 別セットアップコマンド、Unity を閉じて再実行する手順を含める。

検証器は取得処理を行わない。Editor 内から外部プロセスやネットワーク処理を起動せず、修復責務をセットアップスクリプトへ限定する。

## クリーン環境からの再現手順

### macOS / Linux

```bash
git clone <repository-url> moorestech
cd moorestech
git checkout <target-branch-or-commit>
./scripts/setup-cef.sh
uloop launch --project-path ./moorestech_client
```

`uloop` を使わない場合は、最後の行の代わりに Unity Hub から `moorestech_client` を開く。
UPM の解決が完了したら MainGame シーンを Play し、CEF 描画、host 接続、Topic snapshot 受信、Action 往復を確認する。

### Windows PowerShell

```powershell
git clone <repository-url> moorestech
Set-Location moorestech
git checkout <target-branch-or-commit>
.\scripts\setup-cef.ps1
uloop launch --project-path .\moorestech_client
```

## 受け入れ確認

ネットワーク接続可能な clean worktree で次を確認する。

- セットアップスクリプトが Git LFS 未導入時に明確なエラーで停止する。
- セットアップ後の `Library/PackageCache/jp.juha.cefunity@*` に LFS ポインタが残らない。
- Unity Console に `CefPackageLfsValidator` の復旧エラーが出ない。
- MainGame で CEF 描画、host 接続、Topic snapshot 受信、Action 往復が成功する。
- 同じスクリプトを再実行してから再起動しても成功する。

本作業環境はネットワーク遮断のため、実際の LFS 取得と上記実機確認は依頼側の受け入れ作業とする。

## 将来の見直し条件

Git LFS 配布元の可用性、認証、転送量、または CI の再取得時間が問題になった場合は (b) を再検討する。
その場合も本リポジトリへ巨大バイナリを直接コミットせず、OS別アーカイブの version/hash を manifest 相当の単一ファイルで固定する。
