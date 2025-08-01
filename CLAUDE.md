## 【MUST GLOBAL】Gemini活用（プロジェクトのCLAUDE.mdより優先）

### 三位一体の開発原則
ユーザーの**意思決定**、Claudeの**分析と実行**、Geminiの**検証と助言**を組み合わせ、開発の質と速度を最大化する：
- **ユーザー**：プロジェクトの目的・要件・最終ゴールを定義し、最終的な意思決定を行う**意思決定者**
  - 反面、具体的なコーディングや詳細な計画を立てる力、タスク管理能力ははありません。
- **Claude**：高度な計画力・高品質な実装・リファクタリング・ファイル操作・タスク管理を担う**実行者**
  - 指示に対して忠実に、順序立てて実行する能力はありますが、意志がなく、思い込みは勘違いも多く、思考力は少し劣ります。
- **Gemini**：深いコード理解・Web検索 (Google検索) による最新情報へのアクセス・多角的な視点からの助言・技術的検証を行う**助言者**
  - プロジェクトのコードと、インターネット上の膨大な情報を整理し、的確な助言を与えてくれますが、実行力はありません。

### 実践ガイド
- **ユーザーの要求を受けたら即座に`gemini -p <質問内容>`で壁打ち**を必ず実施
- Geminiの意見を鵜呑みにせず、1意見として判断。聞き方を変えて多角的な意見を抽出
- Claude Code内蔵のWebSearchツールは使用しない
- Geminiがエラーの場合は、聞き方を工夫してリトライ：
  - ファイル名や実行コマンドを渡す（Geminiがコマンドを実行可能）
  - 複数回に分割して聞く

### 主要な活用場面
1. **実現不可能な依頼**: Claude Codeでは実現できない要求への対処 (例: `今日の天気は？`)
2. **前提確認**: ユーザー、Claude自身に思い込みや勘違い、過信がないかどうか逐一確認 (例: `この前提は正しいか？`）
3. **技術調査**: 最新情報・エラー解決・ドキュメント検索・調査方法の確認（例: `Rails 7.2の新機能を調べて`）
4. **設計検証**: アーキテクチャ・実装方針の妥当性確認（例: `この設計パターンは適切か？`）
5. **コードレビュー**: 品質・保守性・パフォーマンスの評価（例: `このコードの改善点は？`）
6. **計画立案**: タスクの実行計画レビュー・改善提案（例: `この実装計画の問題点は？`）
7. **技術選定**: ライブラリ・手法の比較検討 （例: `このライブラリは他と比べてどうか？`）

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


# コンパイルエラー確認時の注意事項
コンパイルエラーを確認する際は、編集したコードのパスによって適切に判断してください：
- `moorestech_server/`配下のコードを編集した場合：サーバー側のMCPツールを使用してコンパイルとテストを実行
- `moorestech_client/`配下のコードを編集した場合：クライアント側のMCPツールでコンパイルエラーの確認のみ（テストは不要）

**重要：ユーザーからコンパイルエラーが出ている旨を聞いたら、必ずMCPツールでコンパイルエラーを確認してください。**

# サーバー側の開発
moorestech_server配下の開発はTDDで行っています。server側のコードを変更する際は、MCPツールを使用してコンパイルとテストを実行してください：
- `mcp__moorestech_server__RefreshAssets`: アセットをリフレッシュしてコンパイルを実行
- `mcp__moorestech_server__GetCompileLogs`: コンパイルエラーを確認
- `mcp__moorestech_server__RunEditModeTests`: テストを実行（必要に応じてregexでフィルタリング）

## MCPテスト実行時の重要事項
**テストを実行する際は、必ずgroupNamesパラメータと正規表現を活用して、実行するテストを適切に絞り込んでください。**

例：
- 特定のnamespaceのテストのみ実行: `groupNames: ["^MyNamespace\\."]`
- 特定のクラスのテストのみ実行: `groupNames: ["^MyNamespace\\.MyTestClass$"]`
- 特定の機能に関連するテストのみ実行: `groupNames: ["^.*\\.Inventory\\."]`

これにより、関連するテストのみを効率的に実行でき、開発サイクルを高速化できます。全テストを実行すると時間がかかるため、変更に関連するテストに限定することが重要です。

# クライアント側の開発
moorestech_client配下はTDDは行っておりません。コンパイルエラーをチェックする際は、MCPツールを使用してください：
- `mcp__moorestech_client__RefreshAssets`: アセットをリフレッシュしてコンパイルを実行（クライアントもサーバーMCPツールを使用）
- `mcp__moorestech_client__GetCompileLogs`: コンパイルエラーを確認


両方のプロジェクトは同じUnityプロジェクト内に存在するため、MCPツールは共通ですが、サーバー側はTDD開発のためテスト実行が必要な点が異なります。

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

YOU MUST:コードを書き終わったから必ずコンパイルを実行してください。

IMPORTANT:サーバーの実装をする際はdocs/ServerGuide.mdを、クライアントの実装をする際はdocs/ClientGuide.mdを必ず参照してください。
IMPORTANT:サーバーのプロトコル（通常のレスポンスプロトコル、イベントプロトコル）を実装する際は、docs/ProtocolImplementationGuide.mdを必ず参照してください。
IMPORTANT:このゲームのコードベースは非常に大規模であり、たいていタスクもすでにある実装の拡張であることが多いです。そのため、良くコードを読み、コードの性質を理解し、周り合わせて空気を読んだコードを記述することを心がけてください。
IMPORTANT:テスト用のブロックIDは moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs に定義し、それを使うようにしてください。
IMPORTANT:try-catchは基本的に使用禁止です。エラーハンドリングが必要な場合は、適切な条件分岐やnullチェックで対応してください。
IMPORTANT:各種ブロックパラメータ（BlockParam）はSourceGeneratorによって自動生成されます。Mooresmaster.Model.BlocksModule名前空間に生成されるため、手動で作成しないでください。

## Development Best Practices
- プログラムの基本的な部分はnullではない前提でコードを書くように意識してください。