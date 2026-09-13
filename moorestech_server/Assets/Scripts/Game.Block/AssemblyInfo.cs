using System.Runtime.CompilerServices;

// 確率抽選の internal な入口（MachineOutputFactoryUtil 等）をテストから直接1回だけ呼ぶために公開する
// Exposes internal probabilistic entry points (MachineOutputFactoryUtil and friends) so tests can invoke them exactly once
[assembly: InternalsVisibleTo("Server.Tests")]
