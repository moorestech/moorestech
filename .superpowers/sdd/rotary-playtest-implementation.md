# Task 5 録画シナリオ引き継ぎ

状態: scenario-ready。シナリオ commit `46297d0056`。実走・コンパイル・スクリーンショット確認は未実施。Unity は controller 側のドメインテスト中のため、この作業では触れていない。

シナリオ: `.agents/skills/unity-playmode-recorded-playtest/scenarios/connect/rotary-generator-ui.cs`。蒸気機関と20歯→10歯の二段増速で回転発電機の最低40 RPMを満たす。蒸気の継続補給は `GameUpdater.UpdateObservable` 購読で準備し、シナリオ終了時に解除する。Fキー開閉とホットバー電線ツールの結線は実操作経路を通す。各状態でサーバー詳細値、DOM文言、セクションの画面上の順を assert し、3枚の GameView を撮る。

実行（controller が Unity の同時作業を終えた後）:

```bash
uloop control-play-mode --project-path ./moorestech_client --action stop
uloop compile --project-path ./moorestech_client
.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client .agents/skills/unity-playmode-recorded-playtest/scenarios/connect/rotary-generator-ui.cs /Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/rotary-generator-openable/server_v8
```

実測待ち: scout の歯車座標・回転比は静的導出だけ。蒸気補給の tick 順、機械同士の電線ツールクリック、F照準、React 更新遅延も未実測。`result.json` の全 assert、0 byte でない mp4、3枚のスクリーンショットにキャラ・地面・HUD があることを確認する。DOM query は文字と位置のみを返すので、停止中だけが赤く、待機中・発電中は赤くないことはスクリーンショットで目視する。実走で失敗した場合はサーバー歯車網と各RPM、`IsPipeDisconnected`、電線両端の接続を順に probe する。
