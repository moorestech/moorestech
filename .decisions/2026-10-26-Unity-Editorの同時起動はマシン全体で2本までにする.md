# Unity Editor の同時起動はマシン全体で 2 本までにする

決定: Mac mini で同時に動かす Unity Editor は、全セッション合計で 2 本までにする（UITK 段階 0 の並列作業での取り決めは、それまで 3 本だった）。起動の前に毎回 `pgrep -fl "Unity.app/Contents/MacOS/Unity -projectPath"` で本数を数え、2 本未満のときだけ起動する。2 本とも埋まっていれば、Editor の要らない作業を先に進める。要らなくなった Editor は、起動したセッションが `kill <PID>` で止めて枠を返す。
棄却案: 3 本のまま続ける（PC が過負荷になった）。
理由: 2026-10-26 13:18 ごろに PC が過負荷になり、ユーザーが Unity をすべて終了させた。
出所: UITK 段階0 実測セッション（uitk-stage0-measure）。直前の agent の提示「Editor はいま 3 本とも使用中なので、承諾しても着手は C・B・F のどれかが空いてからです。」→ ユーザー 2026-10-26 13:18「PCが過負荷になったのでいったんunity全部落とした」→ 13:24「2本で」。範囲を全セッション合計・期限なしと読むのは agent の読み（3 本で起きた過負荷はマシンの性質なので、段階 0 に限らず恒久でマシン全体に効く）。
リンク: `CLAUDE.local.macmini.md`（Editor 数の目安は 2〜3 本）、bd `moorestech-albof`
