# mapObjectPinはドロップ品指定で最寄りを探せるようにする

決定: mapObjectPinの対象指定を「mapObjectGuid直指定」と「earnItem（ドロップ品itemGuid）指定」の切替にし、木を伐採チャレンジはearnItem=原木で最寄りのmapObjectへピンする。小石拾いは従来どおりmapObjectGuid直指定を維持する。
棄却案: ①mapObjectGuid配列で木の種類を列挙（JSONが100GUID超に膨らみ樹種追加のたび再生成）②装備中の道具で採掘可能かの判定で探す（クライアントに採掘可否判定の複製が要る）
理由: ユーザー裁定 2026-08-22 原文「木を伐採して原木を入手 の木を掘るチュートリアルでピンを石のmapobjectのターゲットにしてしまっている」→ 選択「ドロップ品で探す新param」。真因はピン先GUID『木』がv8 generationで未配置で最寄り探索がnullになりピンが前チャレンジ位置に残留すること。
リンク: docs/adr/0029-tutorial-equip-challenge-pin-target-and-hints.md
