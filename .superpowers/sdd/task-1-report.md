# Task 1 実装レポート: 履歴スタックとレコード型（純C#）＋ユニットテスト

Status: DONE
Commit: `82c82f646` feat(client): 建築Undo履歴スタックとレコード型を追加

## 実装内容
brief（`.superpowers/sdd/task-1-brief.md`）記載のコードを逐語で作成した。

- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/IBuildOperationRecord.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/BuildOperationHistory.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/PlaceOperationRecord.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/RemoveOperationRecord.cs`
- `moorestech_client/Assets/Scripts/Client.Tests/BuildUndo/BuildOperationHistoryTest.cs`
- `moorestech_client/Assets/Scripts/Client.Tests/BuildUndo/PlaceOperationRecordTest.cs`
- `moorestech_client/Assets/Scripts/Client.Tests/BuildUndo/RemoveOperationRecordTest.cs`

Step2（先にテストだけ置いてコンパイルエラー確認）は事前指示によりスキップし、実装とテストを同時に作成してからコンパイルした（1コミットにまとめる方針のため妥当）。

着手前に`PlaceInfo`（`Server.Protocol.PacketResponse`、`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlacePacketDto.cs`）、`BlockId`（`Core.Master`、partial struct、`BlockMaster.cs`）、`BlockDirection`/`BlockVerticalDirection`（`Game.Block.Interface`）の実体を確認し、brief記載のシグネチャ・プロパティ名と一致することを確認済み。

## コンパイル・テスト実行

```
uloop compile --project-path ./moorestech_client
→ Success: true, ErrorCount: 0, WarningCount: 0

uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client.Tests.BuildUndo"
→ Success: true, TestCount: 6, PassedCount: 6, FailedCount: 0
```

初回の`run-tests`実行および直後の`compile`呼び出しが180秒タイムアウトしたが（他worktreeのUnityプロセスと並行起動していたための一時busyと推測。「Unity is reloading」特有のエラー文言ではなかった）、その後の`compile`は正常応答し、再実行した`run-tests`は6テスト全PASSで完了した。

## 自己レビュー結果

- brief要件との照合: `IBuildOperationRecord`/`PlaceOperationRecord`/`RemoveOperationRecord`/`BuildOperationHistory`のAPI（`CreateFrom`, `HasCells`, `SelectUndoableCells`, `SelectReplaceableCells`, `Push`/`TryPop`、上限32件LIFO）はbrief記載シグネチャと完全一致。テスト3ファイルもbrief本文を逐語転記。
- 行数: 各ファイル10〜74行、全て200行以下。
- partial: 使用なし（`grep -c partial`で全ファイル0件）。
- 日英2行コメント: 各実装ファイルのXMLサマリコメント・主要処理コメントは日本語行→英語行のセットで記述済み（brief記載のまま、各行1行に収まっている）。
- getter/setter規約: `HasCells`は読み取り専用の算出プロパティであり、可変状態のSetを提供するものではないため「単純なgetter/setter禁止」規約には抵触しない。
- [SerializeField]・try-catch・デフォルト引数: いずれも本タスクのコードに該当箇所なし（MonoBehaviourもUnity外部境界処理も無い純C#ロジックのため）。
- コミット対象: brief指定パス＋Unity生成の.meta（ディレクトリ.meta含む）のみ。`.moorestech-external-revisions.json`（無関係な自動更新ファイル）はステージ・コミットから除外済み（`git status`で確認、commit後もModified状態のまま残置）。

## 懸念
- 初回`uloop run-tests`/直後の`uloop compile`が180秒タイムアウトした（他worktreeのUnityプロセスとの競合が原因と推測、再実行で解消。テスト結果自体には影響なし）。
- 本タスクは新機能の初回タスクであり依存先タスクは無い前提。`BuildOperationHistory`/各Recordは他コードから未参照（呼び出し側（PlaceSystem/RemoveSystem/Ctrl+Zハンドラ）への統合は後続タスクの範囲）。
