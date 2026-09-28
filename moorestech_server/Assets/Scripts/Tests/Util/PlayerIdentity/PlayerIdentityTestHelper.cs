using Game.PlayerIdentity;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Util.PlayerIdentity
{
    public static class PlayerIdentityTestHelper
    {
        public static int Register(ServiceProvider serviceProvider, string identity)
        {
            var registry = serviceProvider.GetRequiredService<IPlayerIdentityRegistry>();
            var assignment = registry.PreviewAssignment(identity);
            registry.Commit(assignment);
            return assignment.PlayerId;
        }
    }
}
