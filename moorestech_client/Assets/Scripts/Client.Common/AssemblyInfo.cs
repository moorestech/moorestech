using System.Runtime.CompilerServices;

// テスト用に Client.Common の internal メンバーを Client.Tests へ公開する
// Expose internal members of Client.Common to Client.Tests for testing
[assembly: InternalsVisibleTo("Client.Tests")]
