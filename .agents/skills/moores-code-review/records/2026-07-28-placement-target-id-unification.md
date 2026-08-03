# 設置対象IDのGuid統一（plan A）レビュー記録 (2026-07-28)

## 対象
- base: `e202cf6ce` / reviewed head: `8b60a19c4`（レビュー後の適用は `42ad2f9f1`・`714c020b2`）
- ブランチ: `tree2` / PR: —
- plan: `docs/superpowers/plans/2026-07-28-placement-target-id-unification.md`（全7タスク・14コミット）
- context要約
  - ゴール: 設置対象（ブロック・列車車両・接続ツール・ビルドツール・BP）の識別子を生Guid1本へ統一。`buildMenu.yml`に`buildTools`配列を新設、共有アセンブリ`Game.PlacementTarget`にカタログを1本置き、BPをGUID識別化、Web契約を`entryType`+`entryKey`から`id`+`kind`へ差し替え。**ゲームの見た目・挙動は不変**
  - 非目標: 後方互換維持・パフォーマンス最適化・IDラッパー型導入・並び順/ラベル/アイコンの変更・plan B/Cのスコープ・`BlockIconEndpoint`のGuid化
  - 許容トレードオフ: Blockの`iconUrl`だけ揮発BlockIdをURLに残す／カタログが表示順を持つ／アンロックフィルタは呼び出し側に残す／旧セーブのみGUID発行／`GetBuildTool`はfail-loud／webuiの`id`は`z.string()`のまま／trainCar・blueprintのDTO生成経路が無テスト（全て`[agent前提]`。ユーザー裁定由来のラベルは0件）
  - 制約: Func禁止・partial禁止・try-catch原則禁止・200行/10ファイル・日英2行コメント・実行時BlockIdを永続/通信に使わない・schema optionalとフォールバック禁止・サーバー状態同期3点セット・新エラーコードは3レジストリ同時登録

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | 0 | 禁止構文違反なし。`context_source_label`で`[ADR:]`ラベルが解決不能と判明→全行を`[agent前提]`へ正直に降格して再実行 |
| precedent-alignment | 0 | `connectTools`前例への追従を確認。カタログ配置・マスタ化パターンは前例一致 |
| domain-boundary | 1 | アンロック判定switchの完全複製（uGUI/web 2箇所）→設計判断へ |
| type-driven-structure | 0 | `kind`導出の型パターン再分類を指摘→`PlacementTargetKind`網羅switchへ修正済（`8b60a19c4`） |
| master-data-defense | 0 | `buildTools`は必須プロパティ・フォールバックなし。旧セーブGUID発行はユーザー生成データで対象外 |
| hardcoded-content-enumeration | 1 | `PlacementModeTopic`の表示名ハードコード（"Blueprint Copy"）→設計判断へ |
| set-once-dependency-injection | 1 | DI登録済みカタログを誰も解決せず両カタログが毎回`new`→設計判断へ |
| その他reviewer群 | 0 | 削除応答の`FailureReason`未読・`delete_failed`の3レジストリ登録漏れを検出→適用済 |
| Codex外部監査 | 0 | Critical 0 / High 0 / **Medium 1**（種別横断のGuid一意性が未保証）/ Low 2 |
| Fable全般 | 0 | 上記と重複。独自Criticalなし |

## 適用した修正
- `kind`導出を型パターンから`PlacementTargetKind`網羅switchへ（type-driven-structure）→ `8b60a19c4`
- 削除応答の`FailureReason`を読み`InvalidRequest`の無言化を防止（reviewer群）→ `f1b939a96`
- `delete_failed`をerror_codes.json / WireContractTest.expected / actions.ts BENIGN_ERRORSの3者へ登録（reviewer群）→ `a10ac2c04`
- `BlueprintMessagePack.ToJsonObject`のGuid欠落修正（reviewer群）→ `91c030903`
- region化・削除失敗時の再構築抑止・factory回帰テスト追加（決定論＋reviewer群の機械的分）→ `42ad2f9f1`
- 設計判断4件＋Codex Medium 1件（下記）→ `714c020b2`

## 設計判断（AskUserQuestion裁定）
- Q: アンロック判定20行switchが`BuildMenuEntryCatalog`と`WebBuildMenuEntryCatalog`に完全複製（6系統一致）。web側は`build_menu.select`の権限ゲートも兼ねるため片方の更新漏れが未解放ブロック設置につながる。plan Cで3本目の複製になる
  - 裁定: **共有アセンブリに静的フィルタを新設** / 適用: `Game.PlacementTarget/PlacementTargetUnlockFilter.cs`を新設し両カタログを1行へ。ConnectToolが`showAllPlaceable`を見ない既存の非対称は挙動不変で保存（根拠コメント付き）。asmdefに`Game.UnlockState`追加 → `714c020b2`
- Q: `PlacementModeTopic`の表示名が"Blueprint Copy"/"Train Car"のハードコード（5系統がCritical/Warning）。`buildTools`がN件マスタになったため2本目で全部同じ表示になる。ただしマスタ名に直すと表示文字列が変わり「見た目不変」と形式上衝突
  - 裁定: **BuildToolのみマスタ名へ** / 適用: `MasterHolder.BuildToolMaster.GetBuildTool(buildTool.Id).Name`。TrainCarの`"Train Car"`は据え置き → `714c020b2`
- Q: `PlacementTargetCatalog`をサーバDI・クライアントDI双方にSingleton登録したが本番コードは誰も解決せず、両カタログが毎回`new`（7系統一致）。planのTask 5が「plan Cの注入前提」として登録を指示した結果
  - 裁定: **呼び出し側を注入に寄せる** / 適用: `CreateEntries(unlockState, placementTargetCatalog)`へ変更し内部`new`を廃止。`BuildMenuView`は`[Inject]`、`WebUiGameBinder`は`resolver.Resolve`で供給。DI登録は維持 → `714c020b2`
- Q: `blueprint.delete`が通信失敗時に`blueprint_delete_request_failed`でトーストするようになり「挙動不変」と衝突（2系統）。かつエラーコード命名が非対称（良性側が汎用名`delete_failed`、本物の失敗側が具体名）（4系統）
  - 裁定: **トーストは残し命名を対称化** / 適用: `delete_failed` → `blueprint_delete_not_found`を3レジストリ＋`BuildMenuActions.cs`＋`actions.test.ts`で同時改名 → `714c020b2`

## 破棄した指摘
- 「`BuildMenuEntryDtoFactoryTest`が空リストでも通る空振りテスト」（Task 6再レビュー）— 実コード照合で`Assert.Greater(dtos.Count, 0)`が既に存在すると確認。誤検知として台帳に記録し修正を派遣せず
- 「アンロックフィルタをカタログ内に入れるべき」（初期レンズ指摘）— カタログに入れると`build_menu.select`が未解放ブロックを通す権限バイパスになるため配置自体は正。真の欠陥は「複製していること」であり、その形で設計判断へ昇格して解消

## 事後結果（マージ後追記可）
- （未記入）

## メタ
- セッションID: `a10f3e5d-7ad8-4973-bb09-d038bd24e518`
- スキップ系統: なし（Codex監査は完走）
- falsification: Codex Medium対応で追加した`ValidateIdentity`を一時無効化 → `PlacementTargetCatalogTest`が9件中3件REDになることを実測 → 復元・再コンパイル確認
- 最終検証: `uloop compile` ErrorCount 0 / EditMode `PlacementTarget|BuildMenu|Blueprint|WireContract|WebUi|Unlock|ConnectTool` 186/186 / webui build成功・vitest 389/389 / playwright 118/118
- 備考: レビュー中に共有checkout `../moorestech_master` を別系統が`9ae0786`へ進めたため`.moorestech-external-revisions.json`が汚染。plan Aのピン`68e937d`へ復元して混入を防いだ
- 事故: 修正適用subagentが`API Error: ENOTFOUND`で途中終了。作業ツリーの残存差分を検証したうえでオーケストレータが引き継ぎ完了
