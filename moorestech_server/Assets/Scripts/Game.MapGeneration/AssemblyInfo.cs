using System.Runtime.CompilerServices;

// 配置ステージの internal ヘルパーを、public 化せずテストから直接検証できるようにする。
// Lets the tests exercise the placement stage's internal helpers without making them public.
[assembly: InternalsVisibleTo("Server.Tests")]
