# GameUpdaterの毎tick処理はSetMasterTickの単一エントリだけにする

日付: 2026-10-14
文脈: GameUpdater.AdditionalUpdates が誰でも Add できる public static List のままで、「本体1本だけ」のコメントと食い違っていた。mod や録画シナリオが MasterTickUpdater の明示順序の外に処理を挟める（moores-code-review C18 / D4）。

決定: List を private 化し、public static void SetMasterTick(Action tick) の単一エントリに畳む。MasterTickUpdater だけがここに登録される。出所: ユーザー裁定 2026-10-14「案A: SetMasterTick 1本に畳む（推奨）」
棄却案: List のまま「MasterTickUpdater 以外の登録禁止」と注意書きし、録画シナリオの差し込み口として明示的に許容する（症状は残る）。
理由: 外部から足す口が無くなり、2026-10-13 の「購読口・mod フックを新設しない」裁定を型で強制できる。
リンク: moorestech_logs/harness/moores-code-review/runs/2026-10-13-2340-belt-segment-client-sync-22de6217/design.md
