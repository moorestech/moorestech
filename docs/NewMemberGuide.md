# 新規メンバーガイド

moorestech プロジェクトへようこそ。このドキュメントでは、リポジトリの構成と開発環境の整え方を簡潔に紹介します。より詳細な情報は [DeepWiki](https://deepwiki.com/moorestech/moorestech/) も参照してください。

## リポジトリ概要

- **moorestech_client** – クライアント用の Unity プロジェクト。描画や UI、プレイヤー入力を担当します。
- **moorestech_server** – サーバー用の Unity プロジェクト。ゲームロジックやワールド状態を管理します。
- **test_servers** – テスト用のサーバー設定やサンプルを配置しています。
- **VanillaSchema** – ベースゲームのスキーマデータ。

各 Unity プロジェクトには独立した `ProjectSettings` ディレクトリがあり、アドレッサブルアセットを使用してコンテンツを管理します。

## 開発環境の準備

1. **Unity 2022.3.x 以降** をインストールします。
2. リポジトリをクローンしてサブモジュールを初期化します。
   ```bash
   git clone https://github.com/moorestech/moorestech.git
   cd moorestech
   git submodule update --init --recursive
   ```
3. Unity で `moorestech_server` を開き、**Play** を押してサーバーを起動します。
4. 別の Unity インスタンスで `moorestech_client` を開き、`MainGame` シーンをロードして **Play** を押します。

## 主要な概念

### ゲームアーキテクチャ
クライアントとサーバーを明確に分離したコンポーネントベースの構成です。サーバーがブロック挙動やワールドロジックを処理し、クライアントは表示を担当します。より詳しいレイヤー構成は `docs/ServerGuide.md` と `docs/ClientGuide.md` を参照してください。

### ブロックシステム
`blocks.json` で定義された機械ブロックが、ゲーム内のインフラを構成します。各ブロックは専用のコンポーネントを持ちます。

### リソースシステム
複数のサブシステムが存在し、それぞれ異なるパワーやリソースを扱います。
- **Mechanical Power System** – ギアやシャフトを介して回転力を伝える仕組み。
- **Energy System** / **Fluid System** – 電力や液体を輸送する仕組み。

### アイテム・クラフトシステム
アイテムは `items.json` で定義され、プレイヤークラフトや機械処理のレシピで生成します。

### 物流システム
ベルトなどの輸送ブロックでアイテムを搬送します。アイテムシューターやトレインシステムも存在します。

### ビルドとデプロイ
GitHub Actions で Windows、macOS、Linux 向けにビルドを行い、`.github/workflows/` 以下にワークフローが定義されています。テストも自動実行されます。

### ゲームアセット
`moorestech_client/Assets/AddressableResources` にアドレッサブル形式で各種モデルや UI 素材を配置しています。

## 参考リンク
- [DeepWiki: moorestech](https://deepwiki.com/moorestech/moorestech/)
- `README.md` – 基本的なセットアップ手順
- `memory-bank/` – プロジェクトブリーフや技術情報のまとめ
- `docs/ServerGuide.md` – サーバープロジェクトの解説
- `docs/ClientGuide.md` – クライアントプロジェクトの解説

以上で概要は終わりです。わからないことがあれば気軽に質問してください。楽しい開発を！
