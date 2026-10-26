# Unity Editor の同時起動はマシン全体で 2 本までにする

決定: Mac mini で同時に動かす Unity Editor は、全セッション合計で 2 本までにする（UITK 段階 0 の並列作業での取り決めは、それまで 3 本だった）。起動の前に毎回 `pgrep -fl "Unity.app/Contents/MacOS/Unity -projectPath"` で本数を数え、2 本未満のときだけ起動する。2 本とも埋まっていれば、Editor の要らない作業を先に進める。要らなくなった Editor は、起動したセッションが `kill <PID>` で止めて枠を返す。
棄却案: 3 本のまま続ける（PC が過負荷になった）。
理由: 2026-10-26 13:18 ごろに PC が過負荷になり、ユーザーが Unity をすべて終了させた。
出所: ユーザー 2026-10-26 13:18「PCが過負荷になったのでいったんunity全部落とした」、13:24「2本で」（UITK 段階0 実測セッションでの回答）
リンク: `CLAUDE.local.macmini.md`（Editor 数の目安は 2〜3 本）、bd `moorestech-albof`
