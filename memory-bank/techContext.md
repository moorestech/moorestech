# Tech Context

## 使用技術

### ゲームエンジン
- **Unity 2022.3.x以降**: ゲームエンジンとして使用

### プログラミング言語
- **C#**: メインのプログラミング言語

### 通信プロトコル
- **MessagePack**: クライアントとサーバー間の通信に使用
- **TCP**: 通信プロトコルとして使用

### 依存性注入
- **Microsoft.Extensions.DependencyInjection**: サーバー側の依存性注入に使用
- **VContainer**: クライアント側の依存性注入に使用

### リアクティブプログラミング
- **UniRx**: イベント駆動型アーキテクチャのためのリアクティブプログラミングに使用

### その他のライブラリ
- **CsvHelper**: CSVファイルの読み書きに使用

## 開発環境

### 必要なソフトウェア
- **Unity 2022.3.x以降**: ゲーム開発環境
- **Visual Studio 2022または同等のIDE**: コード編集環境
- **Git**: バージョン管理システム

### リポジトリのセットアップ
```bash
# リポジトリのクローン
git clone https://github.com/moorestech/moorestech.git
cd moorestech

# サブツリーの設定
git remote add schema git@github.com:moorestech/VanillaSchema.git
git fetch schema
git subtree add --prefix=schema --squash schema main
```

## 技術的制約

### パフォーマンス
- **ブロック数の制限**: パフォーマンスを考慮して、ワールド内のブロック数に制限がある可能性があります。
- **アイテム処理の最適化**: 大量のアイテム処理を効率的に行うための最適化が必要です。

### ネットワーク
- **レイテンシの考慮**: クライアント・サーバー間の通信におけるレイテンシを考慮する必要があります。
- **帯域幅の最適化**: 通信データ量を最小限に抑えるための最適化が必要です。

### プラットフォーム
- **クロスプラットフォーム対応**: 異なるプラットフォーム（Windows、Mac、Linuxなど）での動作を考慮する必要があります。

## 依存関係

### 外部ライブラリ
- **VContainer**: https://github.com/hadashiA/VContainer
- **NaughtyCharacter**: https://github.com/dbrizov/NaughtyCharacter
- **Microsoft.Extensions.DependencyInjection**: https://github.com/aspnet/DependencyInjection
- **CsvHelper**: https://github.com/JoshClose/CsvHelper
- **AllSkyFree_Godot**: https://github.com/rpgwhitelock/AllSkyFree_Godot

### フォント
- **Noto Sans Japanese**: https://fonts.google.com/noto/specimen/Noto+Sans+JP
- **Noto Sans**: https://fonts.google.com/noto/specimen/Noto+Sans

## ツール使用パターン

### ビルドと実行
1. Unity Hubを開き、`moorestech_server`プロジェクトを開きます。
2. Playボタンをクリックしてサーバーを起動します。
3. 別のUnityインスタンスで`moorestech_client`プロジェクトを開きます。
4. MainGameシーンを開きます。
5. Playボタンをクリックしてクライアントを起動します。

### デバッグ
- **Unityコンソール**: ログの確認
- **ブレークポイント**: コードの実行を一時停止して変数の値を確認
- **デバッグUI**: クライアント側のデバッグ情報の表示

### MOD開発
- **MODの構造**: `mod.json`、MODのコード（DLL）、アセット、データファイル
- **MODのデプロイ**: MODファイルをゲームのMODディレクトリに配置
- **MODのテスト**: ゲームを起動してMODの機能をテスト

## コード規約

### ファイルエンコーディング
- **UTF-8（BOMなし）**: すべてのソースコードファイルはBOMなしのUTF-8エンコーディングを使用します。BOMが含まれているとクロスプラットフォーム環境での問題の原因になる可能性があります。

### コンポーネント設計
- **依存性の明示**: コンポーネント間の依存関係は明示的に定義し、インターフェースを通じて接続します。
- **プロパティ公開**: 他のクラスから参照するためのプロパティは適切にゲッターを提供し、クラス内部の実装詳細を隠蔽します。

### クラス構造
- **ヘッダーによるグループ化**: SerializeFieldなどのUnity Inspector要素はHeader属性でグループ化し、関連する要素を視覚的に区別します。
- **アクセス修飾子の順序**: public > protected > private の順でメンバーを定義します。

### コードスタイル
- **Null条件演算子**: nullチェックには条件演算子(?.)を活用し、安全なメソッド呼び出しを行います。
- **XMLドキュメントコメント**: 公開APIには適切なXMLドキュメントコメントを提供します。