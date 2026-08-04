// 導出キーは宣言表 Localization/content_keys.csv からC#と同時生成される（npm run gen:i18n）
// Derived keys are generated from Localization/content_keys.csv alongside C# (npm run gen:i18n)
export * from "./generated/contentKeys";

// skitキーはUnity側resolverで解決済みの表示文字列をpushするためWebから構築しない
// Skit keys are resolved by the Unity-side resolver before display strings are pushed
