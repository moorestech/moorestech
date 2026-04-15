# コーディングの指針

## QA（必須）
**問題がある前提で進めてください。あなたの仕事はそれを見つけることです。**
最初の実装が最初から正しいことは、ほとんどありません。QAは確認作業ではなく、バグ狩りとして取り組んでください。最初のチェックで問題が1つも見つからなかったなら、十分に細かく見ていません。

## 文字化け防止ワークフロー（必須）
このリポジトリでは、履歴上 UTF-16 BOM の `.cs` と UTF-8 系ファイルが混在しており、PowerShell の生出力をそのまま読むと文字化けした文字列を誤って再保存する事故が起こりうる。以後、非ASCII文字を含むファイルでは以下を必須手順とする。

1. 編集前に必ず元エンコーディングを確認する
`$b=[IO.File]::ReadAllBytes((Resolve-Path <path>)); if($b.Length -ge 2 -and $b[0] -eq 0xFF -and $b[1] -eq 0xFE){'utf16-le-bom'} elseif($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF){'utf8-bom'} else {'utf8-no-bom'}`
2. 日本語を含む内容確認では `Get-Content` の生出力を信用しない
必ず UTF-8 出力へ切り替えてから読む
`[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false); $OutputEncoding=[Console]::OutputEncoding; Get-Content <path>`
3. 文字化けしたターミナル表示の文字列を、そのままパッチやコメントに貼り付けるのを禁止する
4. 編集後は、手順1で確認した元エンコーディングへ必ず戻す
PowerShell で再保存する場合は `Set-Content` の `-Encoding` を明示する。元が UTF-16 LE BOM なら `-Encoding Unicode`、UTF-8 BOM なら `-Encoding utf8BOM` を使う
5. 編集後チェックとして、変更ファイルに対して必ず文字化け検査を実行する
`git diff -- <path>` で、日本語差分に `縺`, `繧`, `繝`, `鬧`, `蛻`, `蜈` が3回以上連続して出ていないことを目視確認する
6. 上記の化け文字パターンが 1 箇所でも見えたら、そのファイルの編集結果は破棄して読み直しからやり直す

特に `.cs` を PowerShell で読んだ結果が `縺`, `繧`, `鬧`, `蛻` などを大量に含む場合、それは日本語ではなく文字化けとみなして処理を停止すること。

## 互換性とパフォーマンス
計画立案時、後方互換性・パフォーマンス最適化・将来の拡張性は考慮不要です。より良い設計と動作する実装を優先し、改善は必要に応じて後から行います。

## regionの活用

複雑なメソッドでは#regionとローカル関数を活用してください。これにより主要フローが一目で把握でき、詳細実装はローカル関数に隠蔽され、コードの意図が明確になり保守性が向上します。#endregionの下にはコードを書かず、すべて#regionブロックの上部か内部に記述してください。

**重要**: `#region Internal` は「メソッド内のローカル関数をまとめる用途」に限定してください。クラス直下でprivateメソッド群を囲うために `#region Internal` を使うのは禁止です。クラス直下のprivateメソッドは通常どおりそのまま並べるか、必要なら別の責務分割で解決してください。

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

禁止例：
```csharp
public class BadExample
{
    public void UpdateView()
    {
        Execute();
    }

    #region Internal

    private void Execute()
    {
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
全マスタデータ（ブロック、アイテム、液体、レシピ等）は以下の4段階で管理
1. YAMLスキーマ定義（VanillaSchema/*.yml）
2. SourceGeneratorで自動生成（Mooresmaster.Model.*Module）
3. JSONで実データ作成
4. MasterHolderで実行時ロード

- yamlを編集する際は当該skillを参照すること  
- Mooresmaster.Model.*Module（BlocksModule, ItemsModule, FluidsModule等）は全て自動生成、手動作成禁止
- MasterHolder（Core.Master.MasterHolder）が全Masterを静的プロパティで一元管理し、Load(MasterJsonFileContainer)でJSONからロード

# 関連リポジトリ
- `../moorestech_master` — マスターデータ（JSON）とアセット画像のリポジトリ。テストプレイ用Modは`../moorestech_master/server_v8/mods`からロード
- `./moorestech_client/Assets/PersonalAssets/moorestech-client-private` — クライアント側の非公開アセット（有料アセット等）

# テスト・コンパイルの実行

## ドメインリロード中の待機
uloopで「Unity is reloading (Domain Reload in progress)」エラーが出た場合は、45秒待機してからリトライすること。
EditModeInPlayingTest等のPlayMode遷移テストはドメインリロードを引き起こすため、テスト実行後にこのエラーが頻発する。

## コンパイル
| | コマンド |
| サーバー | `uloop compile --project-path ./moorestech_server` |
| クライアント | `uloop compile --project-path ./moorestech_client` |

## テスト
基本的に`--filter-type regex`で実行対象を限定すること。

| | コマンド |
| サーバー | `uloop run-tests --project-path ./moorestech_server --filter-type regex --filter-value "正規表現"` |
| クライアント | `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "正規表現"` |

## ログ確認
| | コマンド |
| サーバー | `uloop get-logs --project-path ./moorestech_server --log-type Error` |
| クライアント | `uloop get-logs --project-path ./moorestech_client --log-type Error` |

# Objectシングルトンパターン
GameObjectはシーン/Prefabに事前配置前提とし、Awakeで_instanceを設定。Instanceプロパティでの動的生成は禁止
public class MySingleton : MonoBehaviour
{
    private static MySingleton _instance;
    public static MySingleton Instance => _instance;
    
    private void Awake()
    {
        _instance = this;
    }
}

# 絶対に守る指示
コードを書き終わったら必ずコンパイルを実行する(.csファイル変更限定)
.metaファイルは絶対に手動作成しない。Unity自動生成のため。Unity起動で作成された.metaのコミットは可
Prefab・シーン・ScriptableObject等のUnity固有ファイル（YAML形式）は直接編集禁止。ユーザーに編集を指示すること。
Library/ディレクトリは絶対に削除禁止。再インポートに膨大な時間がかかるため
try-catchは基本的に使用禁止。エラーハンドリングが必要な場合は、適切な条件分岐やnullチェックで対応
git worktree頻用のため、最初に必ず`pwd`で現在ディレクトリを確認すること。タスク終了前に必ず全作業をコミットすること。作業消失防止
