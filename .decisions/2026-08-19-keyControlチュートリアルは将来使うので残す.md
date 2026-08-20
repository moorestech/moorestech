# keyControlチュートリアルは将来使うので残す

決定: tutorialType `keyControl`（キー操作を促す提示）は実データが server_v8 から消えても抽象一式（schema case・KeyControlTutorialManager・TutorialManager配線・prefab結線）を残す。旧mod moorestechAlphaMod_3 の1件もそのまま。

棄却案: keyControl の全廃（schema enum削除→SourceGenerator再生成、Manager削除、ctor引数削減、prefab結線削除）。レビューで「受益者ほぼゼロの死に抽象」としてCritical指摘されていた。

理由: ユーザー裁定 2026-08-19「それってキーの操作を促すやつだよね。後で使う」。将来の再利用が具体的に見込まれるため、死に抽象ではなく休眠中の機構として扱う。

リンク: [[2026-08-18-チュートリアル提示はWebUI経路に統一しD&Dは矢印ループで示す]]
