# moorestech_web

Web UI のフロントエンドプロジェクトと、Unity から spawn する Node.js / pnpm のバイナリを格納する。

## セットアップ

初回のみ、対応プラットフォームのセットアップスクリプトを実行する:

- macOS / Linux: `bash setup.sh`
- Windows (PowerShell): `.\setup.ps1`

これで `moorestech_web/node/<platform>/` に Node.js と pnpm のスタンドアロンバイナリが展開される。バイナリは `.gitignore` されているので commit されない。

## レイアウト

- `webui/` TypeScript + React + Vite プロジェクト
- `node/<platform>/` Node.js と pnpm のスタンドアロンバイナリ（setup スクリプトで配置）

## 開発中の実行

Unity クライアントを起動すると、内部から `node/<platform>/node` が自動 spawn されて Vite dev server が立ち上がる。手動で `pnpm dev` を回す必要はない。開発中に Vite を手動で再起動したい場合は Unity を再起動する。
