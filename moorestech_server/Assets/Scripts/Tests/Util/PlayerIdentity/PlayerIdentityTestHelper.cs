using Game.PlayerIdentity;

namespace Tests.Util.PlayerIdentity
{
    public static class PlayerIdentityTestHelper
    {
        public static PlayerIdAssignment Register(PlayerIdentityRegistry registry, string identity)
        {
            var assignment = registry.PreviewAssignment(identity);
            registry.Commit(assignment);
            return assignment;
        }
    }
}
