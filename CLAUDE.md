# 気をつけること
XY問題に気をつけてください、目先の問題にとらわれず、根本的な解決を常に行ってください

# 後方互換性についての方針
計画を立案する際、後方互換性は考慮する必要はありません。新しい実装や改善において、より良い設計を追求することを優先してください。

同様に、パフォーマンスの最適化や将来的な拡張性についても、現時点では考慮不要です。まずは動作する実装を優先し、必要に応じて後から改善を行います。

# コードの可読性向上のための指針
複雑なメソッド内でロジックが長くなる場合は、#regionとinternalメソッド（ローカル関数）を活用して、人間がすぐにコードを理解できるようにしてください。

例：
```csharp
public void ComplexMethod()
{
    // メインの処理フロー
    var data = ProcessData();
    var result = CalculateResult(data);
    
    #region Internal
    
    Data ProcessData()
    {
        // データ処理のロジック
    }
    
    Result CalculateResult(Data data)
    {
        // 計算ロジック
    }
    
    #endregion
}
```

この手法により：
- メソッドの主要な処理フローが一目で理解できる
- 詳細な実装はinternalメソッドに隠蔽される
- コードの意図が明確になり、保守性が向上する

`#endregion`の下にはコードを書かないでください。すべてのコードは`#region`ブロックの上部もしくは内部に記述するようにしてください。

# Nullチェックに関する指針
プログラムの基本的な部分はnullではない前提でコードを書くように意識してください。過度なnullチェックはコードの可読性を下げ、本質的なロジックを見えにくくします。

適切なnullチェックが必要な場面：
- 外部から受け取るデータ（API、ユーザー入力など）
- Addressableなどの非同期ロード結果

nullチェックが不要な場面：
- MasterHolderなどのシステムコアコンポーネント
- Awake/Startで初期化される基本的なコンポーネント
- 設計上必ず存在することが保証されているオブジェクト

# ソフトウェアデバッグ
あなたは必要に応じて、テストコードがパスしない時、意図した実装ができないときが発生します。そのようなときは、デバッグログを使用し、原因を究明、修正し、タスクが完了できるように努めてください。

Reflect on 5-7 different possible sources of the problem, distill those down to 1-2 most likely sources, and then add logs to validate your assumptions. Explicitly ask the user to confirm the diagnosis before fixing the problem.


# ドキュメントの更新
*このドキュメントは継続的に更新されます。新しい決定事項や実装パターンが確立された場合は、このファイルに反映してください。*

# マスターデータシステムについて
このゲームのすべてのマスターデータ（ブロック、アイテム、液体、レシピ、チャレンジ、研究等）は、以下の一貫した仕組みで管理されています。

## 概要
マスターデータシステムは次の4段階のプロセスで動作します：
1. **YAMLでスキーマ定義** - データ構造とプロパティを定義
2. **SourceGeneratorで自動生成** - C#のデータクラスとローダーを生成
3. **JSONで実データ作成** - 実際のゲームデータを記述
4. **実行時にロード** - MasterHolderで一元管理してゲーム内で利用

## システムの主要構成

### スキーマ定義 (VanillaSchema/)
- `blocks.yml`, `items.yml`, `fluids.yml` など各種YAMLファイル
- データ構造、型、デフォルト値、外部キー参照などを定義
- 詳細な仕様は `moorestech_server/Assets/yaml仕様書v1.md` を参照

### 自動生成される名前空間
- **Mooresmaster.Model.*Module** - SourceGeneratorによって自動生成
  - `Mooresmaster.Model.BlocksModule` - ブロック関連のクラス
  - `Mooresmaster.Model.ItemsModule` - アイテム関連のクラス
  - `Mooresmaster.Model.FluidsModule` - 液体関連のクラス
  - その他、各マスターデータに対応するモジュール

### マスターデータ管理クラス
- **MasterHolder** (`Core.Master.MasterHolder`)
  - すべてのマスターデータを静的プロパティで保持
  - `ItemMaster`, `BlockMaster`, `FluidMaster` など各種Masterへのアクセスポイント
  - `Load(MasterJsonFileContainer)` メソッドでJSONからデータをロード

- **個別のMasterクラス**
  - `ItemMaster`, `BlockMaster`, `FluidMaster` など
  - 各種マスターデータのID検索、データ取得機能を提供

## データフロー
```
VanillaSchema/*.yml (スキーマ定義)
    ↓ SourceGenerator (ビルド時に自動生成)
Mooresmaster.Model.*Module (C#クラス)
    ↓ 実行時
mods/*/master/*.json (JSONファイル)
    ↓ MasterJsonFileContainer
MasterHolder.Load()
    ↓
ゲーム内で利用 (MasterHolder.ItemMaster等でアクセス)
```

## 開発時の重要事項

### 絶対に守るべきルール
1. **手動でのクラス作成禁止**: `Mooresmaster.Model.*` 名前空間のクラスは絶対に手動で作成しない
2. **BlockParamなどは自動生成**: ブロックパラメータ等もSourceGeneratorが生成するため手動実装禁止

### スキーマを変更する場合
1. `VanillaSchema/*.yml` の該当ファイルを編集
2. プロジェクトをリビルドして自動生成を実行
3. 生成されたクラスを確認

### 新しいマスターデータを追加する場合
1. `VanillaSchema/` に新しいYAMLファイルを作成
2. `Core.Master.MasterHolder` にプロパティを追加
3. 対応するMasterクラス（例：`NewDataMaster`）を実装
4. `MasterHolder.Load()` メソッドに読み込み処理を追加

## テスト時の注意事項
ユニットテストでマスターデータが必要な場合は、以下のテスト用マスターデータを使用してください：

### テスト用マスターデータの場所
- **パス**: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/`
- **含まれるファイル**:
  - `items.json` - テスト用アイテムデータ
  - `blocks.json` - テスト用ブロックデータ
  - `fluids.json` - テスト用液体データ
  - `craftRecipes.json` - テスト用レシピデータ
  - その他、各種マスターデータのテスト用JSON

### テスト用ブロックIDの定義
- `ForUnitTestModBlockId.cs` でテスト用のブロックIDを定義
- テストコードではこれらの定義済みIDを使用すること

### テスト用マスターデータの更新
新しいテストケースでマスターデータが必要な場合：
1. 上記のテスト用JSONファイルを更新
2. 必要に応じて `ForUnitTestModBlockId.cs` に新しいIDを追加
3. 既存のテストに影響しないよう注意して編集


# テストとコンパイルの実行方針

## 基本方針
このプロジェクトでは、**MCPツールを優先的に使用**し、Unityエディタが使用できない場合**シェルスクリプトをフォールバック**として使用します。

## テストの実行

### 1. MCPツールでのテスト実行（推奨）
Unityエディタが起動している場合は、MCPツールを使用してテストを実行します。

#### サーバー側テスト
```
mcp__moorestech_server__RunEditModeTests
```
- 必ず`groupNames`パラメータで実行対象を限定
- 例: `groupNames: ["^Tests\\.CombinedTest\\.Core\\.ElectricPumpTest$"]`

#### クライアント側テスト
```
mcp__moorestech_client__RunEditModeTests
```
- 必ず`groupNames`パラメータで実行対象を限定
- 例: `groupNames: ["^ClientTests\\.Feature\\.InventoryTest$"]`

### 2. シェルスクリプトでのテスト実行（フォールバック）
Unityエディタが使用できない場合やMCPツールリストに上記MCPが無い場合では `tools/unity-test.sh` を使用します。

```bash
# サーバー側のテスト
./tools/unity-test.sh moorestech_server "^Tests\\.CombinedTest\\.Core\\.ElectricPumpTest$"

# クライアント側のテスト クライアント側の場合、バッチモードでは結果が安定しないことがあるため、 isGui オプションを追加してください。
./tools/unity-test.sh moorestech_client "^ClientTests\\.Feature\\.InventoryTest$" isGui
```

### 重要な注意事項
- **必ず正規表現で実行対象を限定してください**
- 全テストの一括実行は時間がかかり、不安定になる可能性があります
- 関連するテストのみを実行して開発サイクルを高速化しましょう

## コンパイルエラーの確認

### MCPツールでのコンパイル（推奨）
編集したコードのパスに応じて適切なMCPツールを使用：

- **サーバー側**（`moorestech_server/`配下）
  - `mcp__moorestech_server__RefreshAssets`: コンパイル実行
  - `mcp__moorestech_server__GetCompileLogs`: エラー確認

- **クライアント側**（`moorestech_client/`配下）
  - `mcp__moorestech_client__RefreshAssets`: コンパイル実行
  - `mcp__moorestech_client__GetCompileLogs`: エラー確認

**重要：ユーザーからコンパイルエラーが出ている旨を聞いたら、必ずMCPツールでコンパイルエラーを確認してください。**

## ビルドの実行
CLIからUnityプロジェクトをビルドする場合は `tools/unity-build-test.sh` を使用してください。

### 使用方法
```bash
# 基本的な使い方（デフォルト出力先: moorestech_client/Library/ShellScriptBuild）
./tools/unity-build-test.sh moorestech_client

# 出力先を指定する場合
./tools/unity-build-test.sh moorestech_client /path/to/output
```

### 機能
- プラットフォームの自動判定（macOS/Windows/Linux）
- Unityのビルド結果（Succeeded/Failed）を正確に判定
- ビルド失敗時のコンパイルエラー詳細表示
- ビルド成功時のファイルサイズ表示
- エラー時のログファイル保存（デバッグ用）

### 注意事項
- ビルドが失敗した場合、ログファイルが保存されるので詳細を確認してください
- macOSの場合、.appファイルが生成されても実際に開けない場合があるため、Unityが報告するビルド結果を信頼してください

# シングルトンパターンの実装指針
Unityプロジェクトにおけるシングルトンの実装では、以下の方針に従ってください：

1. **GameObjectは配置前提**：シングルトンのGameObjectは、シーンやPrefabに事前に配置されている前提で実装します。
2. **Awakeでの初期化**：`_instance`の設定は`Awake`メソッドで行います。
3. **動的生成の禁止**：`Instance`プロパティで`GameObject`を動的に生成することは避けます。

例：
```csharp
public class MySingleton : MonoBehaviour
{
    private static MySingleton _instance;
    public static MySingleton Instance => _instance;
    
    private void Awake()
    {
        _instance = this;
    }
}
```

# 大規模コードベースとの向き合い方
このゲームのコードベースは非常に大規模で、あらゆる機能が複数のサブシステム、長年の設計判断、無数の依存関係によって成立しています。
そのため、新しいタスクに着手する際は「手元のファイルだけを眺めて素早く解決する」発想を捨て、過去の設計意図を追体験するつもりでコードを丹念に読み込み、各モジュールがどのように連携しているのかを把握してください。既存のパターンを見つけ出し、命名規則や責務の分担、データフローやイベントの流れを精査することで初めて、チーム全体の文脈を壊さずに改修するための正解が見えてきます。
中途半端な理解のまま手を動かすと、思わぬ副作用や重複実装、既存APIとの齟齬を生み出しリグレッションリスクが跳ね上がりますが、徹底した読解を経てから設計を考えれば、既に存在する拡張ポイントや抽象化を安全に活用でき、レビュー時の説得力も飛躍的に向上します。
何より、過去のエンジニアが積み上げた知見を踏まえて意思決定することは、空気を読んだ改善を行ううえで不可欠です。常に「既存のどの原則に則るべきか」「既存資産をどう再利用できるか」「この変更が全体最適にどう寄与するか」を問い続け、状況に応じてドキュメントや履歴、関連スクリプトを貪欲に参照しながら、設計と実装を丁寧に積み上げていきましょう。

## プロジェクト全体での一貫性保持
周りの空気を読み、ファイル全体、ネームスペース全体、アセンブリ全体、プロジェクト全体で一貫性のあるコードを書いてください。
単一の箇所だけが美しく整っていても、隣接する処理や他チームの前提と噛み合わなければ即座に技術的負債へと転化します。
必ず既存の命名規約やAPI構成、データ表現、エラー伝播の方針、コメントスタイルまで細かく観察し、どの層でも矛盾が起きないよう調整してください。
各レイヤーで当たり前のように踏襲されている設計は、長期運用の知恵と失敗の歴史によって磨かれた暗黙知の結晶であり、それを尊重することが品質維持と開発速度の両立につながります。
コードを追加・変更するときは「この書き方は同じ責務を持つ隣の型でも採用されているか」「この例外パターンは別のアセンブリでも同様に扱われているか」「レビュー担当が違和感なく読み進められるか」を自問し、全体の拍に合わせた筆運びを徹底しましょう。

# 既存システムの活用原則
このプロジェクトは大規模なプロジェクトです。新機能の実装要望がある場合、多くの場合その基盤となるシステムはすでに存在しています。

**実装前に必ず行うこと：**
1. 関連する既存システムの徹底的な調査（検索、探索、ファイル読み込み）
2. 既存の実装パターンやアーキテクチャの理解
3. 類似機能の実装方法の確認
4. 必要に応じてGemini等の他のAIツールも活用して関連ファイルを発見

早計に新しい概念やシステムを追加するのではなく、既存システムの上に実装を積み重ねることを原則としてください。

# 追加指示

NEVER:.metaファイルは生成しないでください。これはUnityが自動的に生成します。このmetaファイルの有無はコンパイル結果に影響を与えません。.metaの作成は思わぬ不具合の原因になります。

NEVER:Libraryディレクトリの削除は絶対にしないでください。UnityのLibraryディレクトリには重要なキャッシュやビルド情報が含まれており、削除すると再インポートに膨大な時間がかかります。

YOU MUST:コードを書き終わったから必ずコンパイルを実行してください。

IMPORTANT:サーバーの実装をする際はdocs/ServerGuide.mdを、クライアントの実装をする際はdocs/ClientGuide.mdを必ず参照してください。
IMPORTANT:サーバーのプロトコル（通常のレスポンスプロトコル、イベントプロトコル）を実装する際は、docs/ProtocolImplementationGuide.mdを必ず参照してください。
IMPORTANT:テスト用のブロックIDは moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs に定義し、それを使うようにしてください。
IMPORTANT:try-catchは基本的に使用禁止です。エラーハンドリングが必要な場合は、適切な条件分岐やnullチェックで対応してください。
IMPORTANT:各種ブロックパラメータ（BlockParam）はSourceGeneratorによって自動生成されます。詳細は「マスターデータシステムについて」セクションを参照してください。

## Development Best Practices
- プログラムの基本的な部分はnullではない前提でコードを書くように意識してください。
