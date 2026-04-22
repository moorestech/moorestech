# Rider Debugger 運用の詰まりどころ集

SKILL.md の Step 4 / 5 で BP を使うときに参照する、実運用で踏んだ罠の詳細。SKILL.md のサマリで足りない時だけ読む。

## 1. BP設置の順序

1. 疑わしいメソッドの**先頭行**に `set_breakpoint`（サスペンドポリシーは `all` デフォルト）
2. 必要なら `wait_for_pause(timeout=15)` でブロック。resume → wait の繰り返しで hitCount を進める
3. 調査が終わったら `list_breakpoints` → `remove_breakpoint` で自分が張ったline BPを全削除

**先にBP、後でPlayMode操作**。PlayModeが走ってから set_breakpoint してもその後の呼び出しで普通に発火する。

## 2. hitCount=0 は「実行されていない」の決定的証拠

`list_breakpoints` は各line BPの `hitCount` フィールドを返す。数十秒〜数分 PlayMode を動かしても hitCount=0 なら、そのコードパスは**物理的に通っていない**。

「設定ミスかも」より先に hitCount を信じる。上流へ遡って次の BP を張る:

1. このメソッドの呼び出し元は何か (`Grep` で呼び出し箇所検索)
2. 呼び出し元の先頭行にBP → hitCount=0 ならさらに上流へ
3. 最終的に「どこまでは動いているか」の境界が見える

**このhitCount追跡が本スキルの最大の威力**。推論より経験的に正確。

## 3. wait_for_pause の運用

- `timeout` は秒単位で必須
- breakpoint ヒットで解除される時は、返却値に breakpointHit・stackSummary・variables がすべて入る
- タイムアウトで解除される時は `waitResult: "timeout"`。これ自体はバグではなく「何も起きなかった」情報
- タイムアウト後もセッションは running のまま。改めて set_breakpoint するなり `pause_execution` するなり好きにできる

## 4. ローカル変数とフィールドの見方

paused した時の変数観測は 2 通り:

| ツール | 用途 |
|---|---|
| `get_variables` | 現フレームの this + 引数 + ローカル変数を一括取得。最初の一手 |
| `evaluate_expression` | 特定のフィールドチェーンやインデクサを狙い撃ち |

**Implicit evaluation is disabled 問題**:

Rider soft debugger は property getter と method 呼び出しをデフォルトで評価しない。これを知らないと property で死ぬ。

- NG: `list.Count`, `dict[key]`, `obj.GetName()`, `array.Length`（これは何故か通ることもある）
- OK: `list._size`, `list._items[i]`, `obj._privateField`

コレクションの内部フィールド早見表:

| 型 | Count 相当 | インデクサ相当 |
|---|---|---|
| `List<T>` | `_size` | `_items[i]` |
| `T[]` | `Length` (通る時がある) | `[i]` 直接 |
| `Dictionary<K,V>` | `_count` | `_entries[slot].value` (複雑) |
| `HashSet<T>` | `_count` | 実用的にはほぼ無理 |

Dictionary と HashSet は debugger で中身を読むのが難しいので、そういう時は **素直に動的コードに逃げる**。

## 5. 条件付きBP

`set_breakpoint --condition "..."` の式は実行時に評価される。ただし同じ「Implicit evaluation is disabled」の制約を受ける。

**検証のコツ**: いきなり複雑な条件を書かない。段階を踏む。

1. まず条件なしで BP を張って一度ヒットさせる
2. `evaluate_expression` で本番の条件式を評価して、エラー無く true/false が返ることを確認
3. その式をそのまま `condition` に載せて set_breakpoint し直す

型判定の `is` は **fully qualified name** が必須。`is ItemStack` では `CS0246: The type or namespace name 'ItemStack' does not exist` になる。`Grep` でクラス定義のファイルを開いて `namespace` を読み、`is Core.Item.Implementation.ItemStack` と書く。internal クラスでも可。

## 6. tracepoint (logpoint) は地雷

`suspend_policy: "none"` + `log_message` で非停止ログ出力が作れるが、次の 3 点で Unity を簡単にフリーズさせる:

1. **複雑な式の評価**: `{this}` や `{_inventory._items[0]}` のような深いチェーンを毎tick評価するとオーバーヘッドが膨大
2. **内部で例外が出ると消失**: 評価で例外が出ると log が出ないまま hit 数は進む（デバッグ不能）
3. **log が stdout ではなく Rider の Debug Output に出る**: `uloop get-logs` では拾えない

**基本的に tracepoint は避ける**。どうしても使うなら `i={i}` 程度の超シンプルな式に絞る。

## 7. Unity が busy 化した時の復旧手順

**症状**: `uloop execute-dynamic-code` が 180秒タイムアウトでエラー、Unity Editor GUI が無反応、`get-logs` も返ってこない。

**原因**: debugger が内部的に paused/step 状態のまま、Unity のスクリプトスレッドが debugger のコマンド待ちで固まっている。

**復旧手順** (優先順):

1. **Rider の Debug ツールウィンドウから赤い停止ボタンを押す** (Shift+F5) → Unity 即復活。最速
2. もしくは `mcp__rider-debugger__stop_debug_session` → `start_debug_session` で再アタッチ
3. それでもダメなら Unity を一度閉じて uloop launch で再起動

**予防**: Step 6 のクリーンアップを毎回忠実に実行する。paused のまま離席しない。

## 8. スレッド一覧の読み方

`list_threads` は Unity プロセスの全スレッドを返す。moorestech 系なら以下に注目:

| 名前 | 意味 |
|---|---|
| `(main)` / id="main" | Unity main thread。MonoBehaviour.Update とかここ |
| `[moorestech]ゲームアップデートスレッド` | ServerGameUpdater スレッド。ブロックロジックのtick源 |
| `<Thread Pool>` x N | `Task.Run` 等のワーカー |
| `Burst-CompilerThread-*` | Unity Burst コンパイラ内部 |
| `ServerSocket-UnityServer-*` | RemoteConfig / IDE連携 |

期待するバックグラウンドスレッドが**名前で存在するか**を最初に見る。名前が無ければそのスレッドは起動していない。

## 9. ステップ実行系 (step_over / step_into / run_to_line)

- `step_into` は this のメソッド呼び出しに深く入れる。ただしフレームワーク内部（UniRx とか）に吸い込まれると戻ってこられないので注意
- `run_to_line` は BP を張る代わりの一時的な進め方。対象行に到達しなくてもタイムアウトしたら手動で resume する
- `step_over` は property getter の呼び出しも1ステップでスキップするので、内部で複雑なことが起きていても追えない。疑わしい時は step_into に切り替え

## 10. Exception breakpoint

`list_breakpoints` の返却には `type: "exception"` の要素が複数並んでいる (`DotNet_Exception_Breakpoints` 等)。これらはデフォルトで disabled。自分で張った line BP とは区別すること。**削除不要**で、残しても害は無い。
