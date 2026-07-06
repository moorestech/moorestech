# プレイテストDSL 使い方ガイド

`docs/playtest-stabilization-investigation.md` の提案を実装したもの。
録画付き実プレイ検証を「Bash呼び出し約170〜190回」から「シェル1コマンド」に圧縮する。

Implementation of the proposal in the stabilization investigation: compresses a recorded
play-verification session from ~170-190 Bash calls down to a single shell command.

## 構成 / Layout

- `moorestech_client/Assets/Scripts/Client.Playtest/` — 事前コンパイル済みDSL（Editor専用asmdef、Test Runner非依存）
- `tools/playtest/preflight.sh` — 実行前チェック（疎通/コンパイル/master実在/マスタロードドライラン）
- `tools/playtest/run-scenario.sh` — 一発実行ランナー
- `tools/playtest/scenarios/*.cs` — シナリオ置き場（execute-dynamic-codeに渡すC#スニペット。コンパイル資産にはならない）
- 成果物は `moorestech_client/PlaytestResults/<セッション>/<ラン名>/`（result.json・スクショ・mp4、git管理外）

## 一発実行 / One-shot run

```bash
cd <worktree-root>
./tools/playtest/run-scenario.sh \
    /path/to/moorestech_client \
    tools/playtest/scenarios/sample-chest.cs \
    [master-server-dir]   # 省略時: /Users/katsumi/moorestech-worktrees/playtest-master/server_v8
```

流れ: preflight → `PlaytestBoot.PrepareAndEnterPlayMode`（NoSave・masterパス設定・PlayMode起動）→
ready.marker出現待ち（初期化完了イベント駆動、実測~26秒）→ シナリオをEDC 1回で投入 →
result.json出現待ち → 表示（Successでexit 0）。

## シナリオの書き方 / Writing scenarios

execute-dynamic-code のスニペット形式（using可・文の羅列・returnで値を返す）:

```csharp
using Client.Playtest;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };   // Record=trueでmp4録画
return PlaytestRunner.Run("my-scenario", options, async p =>
{
    await p.SetupFlatGround();                            // 足場(50x3x50 @ y30)生成+ワープ
    p.PlaceBlockDirect("木のチェスト", new Vector3Int(3, 32, 3), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(3, 32, 3)); // クライアント側出現待ち
    await p.GiveItem("鉄インゴット", 16);                   // 本番giveコマンド経路+反映待ち
    p.GiveItemDirect("鉄板", 8);                           // サーバー直挿入（即時）
    await p.Until(() => p.CountItem("鉄インゴット") >= 16, 10f, "在庫反映");
    p.Assert(p.GetBlock(new Vector3Int(3, 32, 3)) != null, "チェスト存在");
    await p.Screenshot("final");                           // UI込みGameViewスクショ
});
```

主なDriver API: `SetupFlatGround` / `WarpPlayer` / `PlayerPosition` /
`GiveItem`(コマンド経路) / `GiveItemDirect` / `CountItem` /
`PlaceBlockDirect` / `RemoveBlock` / `GetBlock` / `WaitBlockGameObject` /
`Until(条件, timeout, ラベル)` / `WaitSeconds` / `Assert` / `Screenshot` /
`SendCommand` / `ServerService<T>()`。

- Direct系＝サーバーデータストア直叩き（状態を素早く作る用・インベントリ非消費）
- 非Direct系＝本番プロトコル経路。Phase 3でUIクリック経路(`via: UI`)を追加予定
- 例外・タイムアウト・Assert失敗はすべて result.json に落ちる（実行は`Error`に記録して終了）

## ハマりどころ（今回実際に踏んだもの） / Pitfalls actually hit

1. **masterデータとスキーマの不整合**: moorestech_masterリポジトリHEADが別ブランチ（plan4）用に
   移行していると `MooresmasterLoaderException` でPlayMode初期化が無言死する。
   → ブランチ互換コミットにピン留めしたmasterのgit worktree（`playtest-master`、現在584a14e）を使う。
   preflight [4/4] のマスタロードドライランがPlayMode前に検出する。
2. **コンパイル直後のEDC失敗**: ドメインリロード中のexecute-dynamic-codeは失敗する。
   スクリプト内の`edc_retry`/`edc`がリトライで吸収。手で叩くときも1回の失敗で諦めない。
3. **worktree初回起動のスキーマ再コンパイル**: 「Schemaフォルダに変更がありました」がPlayMode中に
   発火するとドメインリロードで初期化が破壊される。初回はPlayMode前に一度コンパイルを通して落ち着かせる。
4. **DebugObjectsBootstrap**: 無効化しないとIngameDebugConsoleが毎フレームNREを吐く環境がある。
   `PlaytestBoot`がテストと同じ`DebugObjectsBootstrap_Disabled`フラグで自動無効化する。
5. **worktreeのLibrary**: メインの`Library`(28G)を`cp -Rc`（APFS clonefile）で複製すれば再インポート不要。
   メイン側のUnityが閉じている時に行うこと。

## 今後 / Next (Phase 3)

- セマンティック入力層（`ClickBlock`/`DragItem`等、QueueStateEvent + WorldToScreenPoint）
- 前提: legacy Input残存3箇所（カメラズームF1/F2・設置高さQ/E・右クリックカメラ）のInputSystem移行
- Enter Play Mode Optionsのドメインリロード無効化調査（Play開始の大幅短縮）
