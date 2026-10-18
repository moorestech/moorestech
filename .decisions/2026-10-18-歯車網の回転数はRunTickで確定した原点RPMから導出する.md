# 歯車網の回転数はRunTickで確定した原点RPMから導出する

日付: 2026-10-18
文脈: スループット回帰テスト A11 ②で、燃料を抜いた風車の装填燃料が尽きて減速に入った tick に、網の RPM が 9.83（減速中の風車の値）になりベルトが1 tick だけ 17 で走った（ADR 0084 F3、実測 `phase2w t=3281 speed=17 rpm=9.8333 genRpm=10.0000`）。原因は `TryResolveRotation` が cache の原点 generator の `GenerateRpm` を生で読むため、同じ tick の後半の燃料消費で下がった値が原点交代前に漏れること。
決定: `GearNetworkRuntimeState` に `OriginRpm` を持たせ、RunTick（需給計算）時点の原点 RPM を凍結する。`TryResolveRotation` はそれから導出する。tick の順序と計算回数は変えない。出所: ユーザー裁定 2026-10-18「3でいいとおもう」
棄却案: 燃料消費を RunTick の前に移す（燃料消費に渡す負荷率が前 tick の値になる）。燃料消費の後に出力が変わった網の原点だけ選び直す（RPM が変われば要求動力も変わるので需給を回し直さないと整合しない、ユーザー指摘）。需給計算を2パス回す（継続駆動の網が毎 tick 二重計算）。
理由: 需要 → 需給・原点選定 → 燃料消費 の依存を保ったまま、tick の途中で値が混ざることだけを消せる。generator の出力変化が次 tick に反映される 1 tick の遅れは今も全体がそうで、新しい遅れは増えない。
リンク: docs/adr/0084 §裁定 F3、`BeltThroughputGearTest.A11_FuelWindmillsRunningDry`（②の全 tick で速度 18）
