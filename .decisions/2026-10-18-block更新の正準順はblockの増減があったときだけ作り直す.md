# block更新の正準順はblockの増減があったときだけ作り直す

日付: 2026-10-18
文脈: スループット回帰テストの所要時間を調べる中で `MasterTickUpdater.Update` を仮計測したところ、毎 tick 全 block を List に詰めて BlockInstanceId 昇順に Sort する処理が、361 block の世界で 1 tick 249 µs のうち 132 µs を占め、ベルト搬送(10 µs)より重かった(ADR 0084 §サーバー 1 tick の内訳)。この sort は 2026-09-13 の 4ef58ba38 で入り、origin/master にもある本番の処理。
目的は決定論: block 更新フェーズは各 block の Update が隣のインベントリへ直接押し込む(機械の出力、チェストの押し出し、採掘機、機械→ベルト)ため更新順で結果が変わり、Dictionary の列挙順は設置・撤去の履歴で変わる。セーブは昇順なので、ロード前後やクライアント再生で同じ順にするために昇順へ揃えている。
決定: 昇順リストは `IWorldBlockUpdateEvent` の設置・撤去イベントを購読して dirty にし、dirty の tick の先頭でだけ作り直す。設置・撤去は tick 末尾で確定するので、次 tick の block 更新前に必ず反映される。順序の保証は変えない。出所: ユーザー裁定 2026-10-18「ok毎tickのsortをdirtyのときだけやるで修正を」
棄却案: 毎 tick sort を続ける(n log n が本番の tick に常に乗る)。block 更新フェーズのアイテム移動を中央の更新器へ寄せて順序依存そのものを消す(歯車・電力と同じ形。正しい方向だが別の設計変更、今回の範囲外)。
理由: 昇順に揃える目的(ロード前後・再生で同じ更新順)は、block の集合が変わったときにだけ並べ直せばそのまま保てる。設置・撤去は全て `WorldBlockDatastore` の 2 経路を通り、どちらもイベントを出すので購読で漏れなく dirty にできる。
リンク: `Server.Boot/MasterTickUpdater.cs`、docs/adr/0084 §サーバー 1 tick の内訳
