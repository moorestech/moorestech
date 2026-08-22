# コーディングの指針

## QA（必須）
**問題がある前提で進めてください。あなたの仕事はそれを見つけることです。**
最初の実装が最初から正しいことは、ほとんどありません。QAは確認作業ではなく、バグ狩りとして取り組んでください。最初のチェックで問題が1つも見つからなかったなら、十分に細かく見ていません。

## 互換性とパフォーマンス
計画立案時、後方互換性・パフォーマンス最適化・将来の拡張性は考慮不要です。より良い設計と動作する実装を優先し、改善は必要に応じて後から行います。

## 既知の制約（設計上の割り切り）
- ゲームは起動後メインメニューへ戻らない（modによるコード・static書き換えがあるため再開時の状態を保証できず、終了以外の道を作らない設計）。よってゲーム寿命オブジェクトのdispose漏れ・IDisposable付与は考慮不要
- プロトコルのplayerIdはクライアント自己申告で偽造容易だが現時点で許容。セキュリティ是正は後でリポジトリ全体を一括改修する

## regionの活用

複雑なメソッドでは`#region Internal`とローカル関数を活用してください。これにより主要フローが一目で把握でき、詳細実装はローカル関数に隠蔽され、コードの意図が明確になり保守性が向上します。`#region Internal`の`#endregion`の下にはコードを書かず、すべて#regionブロックの上部か内部に記述してください。

**重要**: `#region Internal` は「メソッド内のローカル関数をまとめる用途」に限定してください。クラス直下でprivateメソッド群を囲うために `#region Internal` を使うのは禁止です。クラス直下のprivateメソッドは通常どおりそのまま並べるか、必要なら別の責務分割で解決してください。
`#region HogeHoge`といったInternalで無いのは問題ない。

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
主要な処理セクションには日本語・英語の2行セットコメント（// 日本語 → // English）を、約3〜10行ごとに挿入してください。日本語・英語それぞれ必ず1行に収めること（長くなっても折り返さない。日本語複数行＋英語複数行の固まりは禁止）。冗長な説明は避け、意図を端的に示してください。

複数行にわたる解説コメント（YAMLヘッダ等）は「日本語ひとかたまり → 英語ひとかたまり」で記述し、行ごとの交互や言語ブロックの往復は禁止。

自明なコメント（名前から読める説明等）は書かない。日本語本文の長さ目安は処理・変数20字、メソッド30字。複雑なアルゴリズムと「なぜ必要か」の根拠コメントは長くても可。

## Nullチェックに関する指針
基本的にnullでない前提でコードを書いてください。nullチェックは外部データ（API・ユーザー入力）や非同期ロード結果（Addressable等）にのみ行い、MasterHolder等のコアコンポーネントやAwake/Start初期化済みオブジェクトなど設計上存在が保証されるものには不要

## その他の規約
単純なgetter/setterプロパティは使用禁止、値のSetはpublic void SetHogeメソッドで行う。`{ get; private set; }`（自クラス内でのみ書き換え）は許容し、バッキングフィールド＋素通しプロパティの二重保持はこの形に畳む
[SerializeField]は_無しの小文字キャメルケース
エディタ専用コードは#if UNITY_EDITORで囲みファイル末尾に配置
よく使うシステム(ワールド、インベントリ等に関連すること)は`ServerContext.cs`や`ClientContext.cs`にあるので適宜参照
デフォルト引数は基本使用禁止。引数の追加は必ずデフォルト値をつけず、呼び出し側を変更する
初期化メソッド名は`Initialize`固定（記述順はctor→`Initialize`）
名前は実処理と一致させる（イベントは変化対象を名前に含める）
同種の条件分岐は文脈が集まっている側の一箇所へ揃える（直下に集めるなら直下、ローカル関数に集めるならローカル関数）
デバッグ/テスト専用publicをプロダクションに残さない

### 時間に関して
サーバーのゲームロジックで経過時間を測るときは`Core.Update.GameUpdater`のティック加算だけを使う。`Time.deltaTime`・`Stopwatch`・`Environment.TickCount`といった実時間APIは使わない
秒との換算は`GameUpdater.SecondsToTicks``TicksToSeconds`
`DateTime`は「実世界の日時そのものを記録する」用途（セーブの世界作成日時・セッション開始時刻・累計プレイ時間）に限りOK
現在の累積ティックは`GameUpdater.CurrentTick`。

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
`uloop compile --project-path ./moorestech_client`

## テスト
基本的に`--filter-type regex`で実行対象を限定すること。
`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "正規表現"`

サーバー側のテストはクライアントプロジェクトからもインポートされており、上記コマンド（クライアントのproject-path）で同時に実行できる。サーバー単体プロジェクトを別途指定する必要はない。
unity-playmode-recorded-playtestでPlayModeを通しで動かす検証は「unityプレイ録画テスト」と呼ぶ。「e2e」「E2Eテスト」とは呼ばない。

## ログ確認
`uloop get-logs --project-path ./moorestech_client --log-type Error`

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
Prefab・シーン・ScriptableObject等のUnity固有ファイル（YAML形式）をテキストエディタや`Write`/`Edit`ツールで直接編集することは禁止。整合性が壊れるため。ただし`uloop execute-dynamic-code`によるUnity Editor経由の変更は正規ルートとして許容（Unity自身がシリアライズするため整合性が保たれる）手で書き換える必要があるケースのみユーザーに指示すること
Library/ディレクトリは絶対に削除禁止。再インポートに膨大な時間がかかるため
worktreeの新規作成等で新しくunityを立ち上げる場合メインworktreeからLibraryをコピーして時間短縮する
try-catchは基本的に使用禁止。エラーハンドリングは条件分岐やnullチェックで対応。例外として、外部境界（外部プロセス起動、WebSocket等のネットワーク送受信、外部入力JSONのパース）の隔離目的に限り使用可。その場合は境界である根拠をコメントで明記すること
git worktree頻用のため、最初に必ず`pwd`で現在ディレクトリを確認すること。タスク終了前に必ず全作業をコミットすること。作業消失防止
partialは禁止。如何なる条件でもpartialを絶対に使ってはいけない
`Func<>`の使用は禁止。コールバックや述語を渡したくなった時点で設計を見直すサインであり、`Action`やカスタムdelegateへ置き換えるだけの非本質的な対応に逃げてはいけない。interfaceの導入、依存関係の整理、呼び出し方向の変更など本質的な設計変更を検討すること

# 設計原則（実PRレビュー指摘由来・違反はレビューで必ず差し戻される）
- **変更の波及を恐れない。** 正しい設計のためなら全JSON・全呼び出し側の一括更新を選ぶ。スキーマの`optional: true`・`?? Default`フォールバック・ローダーでの欠損補完で吸収するのは設計の敗北
- **汎用基盤にドメイン語彙を持ち込まない。** 基底コンポーネント・共通サービス・Master・Templateは上位の業務概念（アイドル・採掘中等）や`Func<bool>`述語を知らない。判断は具体側で行い、基盤には`SetHoge(値)`でプッシュする
- **状態変化の検知は購読で。** `Update()`内で毎tickの同値判定をしない。UniRxの変化通知をSubscribeするか、変化を起こす操作の直後にプッシュする。`Update()`は物理進行（搬送・採掘進捗）専用
- **着手前に前例を探す。** 同形の問題を解いている既存実装を検索し、そのパターンに従う。前例は機構でなく役割で選び、逸脱するなら理由を明記して裁定に出す。サーバー可変状態のクライアント同期は「イベントパケット＋初期データ＋購読」の3点セットが標準
- **挙動の変更・機能追加はgrill-first。** 「〜にしたい」「〜するようにして」型の依頼は、バグ修正に見えても・仕様が自明に見えても、実装前に moores-grill-with-docs を起動する（HARD GATE: 単純だから省略は禁止・真に単純なら数問で終わる）。直接実装してよいのは調査・質問への回答・明白なクラッシュ/コンパイル修正のみ
- 実装前チェックリスト: `.agents/skills/moores-code-review/references/lens-digest.md`／PR前レビュー: moores-code-review スキル

# スキル配置と実行記録
- スキルのgit正本は `.agents/skills/` のみ。`.claude/skills` と `.codex/skills` はそこへのsymlink（tracked）。ミラー実体の複製・CI同期は禁止（マージ衝突が3倍になるため廃止済み）
- レビュー実行記録（moores-code-reviewのrecords/eval-log、pr-independent-reviewのrecords/シャドー台帳）はコードrepoに置かず `../moorestech_logs/harness/` へ書く。featureブランチで記録ファイルをコミットしない
- `../moorestech_logs` 内で作業する前に同repoの `README.md` を読む（レイアウト・考古学手順・消失事故の落とし穴が書いてある）

# コーディングにおける重要な原則
- 長くてもいいから適切な名前のクラス名、変数名をつける
- 1ファイルにつき必ず200行以下に抑える。はみ出る場合は適切な階層構造、ディレクトリ構造を構築しファイルを分割する。分割時partialは禁止。如何なる条件でもpartialを絶対に使ってはいけない。
- ファイルを新規作成する際1ディレクトリに入れるコードの数は10ファイルまで。それ以上膨らむ場合はサブディレクトリを作成、適切に構造的な分割を行い、コードを分ける。
- イベント発火のためにActionを使わない。UniRxを使う。

# タスク管理 (Beads)
treeにログを残すためにマージは通常のマージコミットを使うSquash and mergeは使用しない
本repoはbd(Beads)でタスク・設計検討・学びを管理する。セッション開始時にhookが概況を注入する（詳細ワークフローは`.agents/skills/beads/SKILL.md`）。
- タスクは着手前に`bd create`で積み、`bd update <id> --claim`で着手、`bd close <id> --reason="..."`で完了。派生発見は`--parent`や`bd dep`で系譜を残す
- 設計メモ・経緯・失敗は`bd note <id> "..."`へ。応答末尾に`LEARN: <一行>`と書くとhookが自動でnoteに保存する
- ユーザー裁定の蒸留は従来どおり`.decisions/`が正。bd側からは`[[ファイル名]]`で参照する
- issueデータはprivate remote(moorestech_logs)へ同期される。それでも秘密情報（トークン・認証情報）は書かない
- `bd edit`は対話エディタを開くため使用禁止。ハーネス内蔵のタスクリストは当該ターンの実行チェックリスト用途に限る
