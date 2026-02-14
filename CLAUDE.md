# コーディングの指針

## QA（必須）
**問題がある前提で進めてください。あなたの仕事はそれを見つけることです。**
最初の実装が最初から正しいことは、ほとんどありません。QAは確認作業ではなく、バグ狩りとして取り組んでください。最初のチェックで問題が1つも見つからなかったなら、十分に細かく見ていません。

## 互換性とパフォーマンス
計画立案時、後方互換性・パフォーマンス最適化・将来の拡張性は考慮不要です。より良い設計と動作する実装を優先し、改善は必要に応じて後から行います。

## regionの活用

複雑なメソッドでは#regionとローカル関数を活用してください。これにより主要フローが一目で把握でき、詳細実装はローカル関数に隠蔽され、コードの意図が明確になり保守性が向上します。#endregionの下にはコードを書かず、すべて#regionブロックの上部か内部に記述してください。

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

## コメント
主要な処理セクションには日本語・英語の2行セットコメント（// 日本語 → // English）を、約3〜10行ごとに挿入してください。冗長な説明は避け、意図を端的に示してください。

## Nullチェックに関する指針
基本的にnullでない前提でコードを書いてください。nullチェックは外部データ（API・ユーザー入力）や非同期ロード結果（Addressable等）にのみ行い、MasterHolder等のコアコンポーネントやAwake/Start初期化済みオブジェクトなど設計上存在が保証されるものには不要

## その他の規約
単純なgetter/setterプロパティは使用禁止、値のSetはpublic void SetHogeメソッドで行う
[SerializeField]は_無しの小文字キャメルケース
エディタ専用コードは#if UNITY_EDITORで囲みファイル末尾に配置
よく使うシステム(ワールド、インベントリ等に関連すること)は`ServerContext.cs`や`ClientContext.cs`にあるので適宜参照
デフォルト引数は基本使用禁止。引数の追加は必ずデフォルト値をつけず、呼び出し側を変更する

# マスタデータについて
全マスタデータ（ブロック、アイテム、液体、レシピ等）は以下の4段階で管理されます：
1. YAMLスキーマ定義（VanillaSchema/*.yml）
2. SourceGeneratorで自動生成（Mooresmaster.Model.*Module）
3. JSONで実データ作成 → 4. MasterHolderで実行時ロード

- yamlを編集する際は当該skillを参照すること  
- Mooresmaster.Model.*Module（BlocksModule, ItemsModule, FluidsModule等）は全て自動生成、手動作成禁止
- MasterHolder（Core.Master.MasterHolder）が全Masterを静的プロパティで一元管理し、Load(MasterJsonFileContainer)でJSONからロード

# テスト・コンパイルの実行
uLoop CLI（Skills経由）を優先、使用不可時は`tools/unity-test.sh`をフォールバックとして使用。

## コンパイル
編集パスに応じてuLoop CLIを使用。フォールバック時はunity-test.shに何にもマッチしない正規表現（例: `"^$"`）を渡してコンパイルのみ実行。

| | コンパイル |
|---|---|
| サーバー | `uloop compile --port 56901` |
| クライアント | `uloop compile --port 56902` |

## テスト
基本的に`--filter-type regex`で実行対象を限定すること。

| | uLoop CLI（推奨） | シェル（フォールバック） |
|---|---|---|
| サーバー | `uloop run-tests --port 56901 --filter-type regex --filter-value "正規表現"` | `./tools/unity-test.sh moorestech_server "正規表現"` |
| クライアント | `uloop run-tests --port 56902 --filter-type regex --filter-value "正規表現"` | `./tools/unity-test.sh moorestech_client "正規表現" isGui` |

- クライアント側シェル実行時は`isGui`オプション必須（バッチモードでは不安定）

## ログ確認
| | コマンド |
|---|---|
| サーバー | `uloop get-logs --port 56901 --log-type Error` |
| クライアント | `uloop get-logs --port 56902 --log-type Error` |

# Objectシングルトンパターン
GameObjectはシーン/Prefabに事前配置前提とし、Awakeで_instanceを設定。Instanceプロパティでの動的生成は禁止。                                                                                                                     public class MySingleton : MonoBehaviour
{
    private static MySingleton _instance;
    public static MySingleton Instance => _instance;
    
    private void Awake()
    {
        _instance = this;
    }
}

# 絶対に守る指示
コードを書き終わったから必ずコンパイルを実行する
.metaファイルは絶対に手動作成しない。Unity自動生成のため。Unity起動で作成された.metaのコミットは可
Prefab・シーン・ScriptableObject等のUnity固有ファイル（YAML形式）は直接編集禁止。ユーザーに編集を指示すること。
Library/ディレクトリは絶対に削除禁止。再インポートに膨大な時間がかかるため
try-catchは基本的に使用禁止。エラーハンドリングが必要な場合は、適切な条件分岐やnullチェックで対応
git worktree頻用のため、最初に必ず`pwd`で現在ディレクトリを確認すること。worktree環境ではMCPを使わず`unity-test.sh`でテスト・コンパイル確認し、タスク終了前に必ず全作業をコミットすること。作業消失防止