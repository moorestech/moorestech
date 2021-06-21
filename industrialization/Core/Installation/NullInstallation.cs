using System;

namespace industrialization.Core.Installation
{
    public class NullInstallation : InstallationBase
    {
        public NullInstallation(int installationId, Guid guid) : base(installationId, guid)
        {
            guid = Guid.Empty;
            installationId = -1;
        }
    }
}