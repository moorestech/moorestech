using System;

namespace industrialization.Core.Installation
{
    public class NullInstallation : InstallationBase
    {
        public NullInstallation(int installationId, int intID) : base(installationId, intID)
        {
            intID = Int32.MaxValue;
            installationId = -1;
        }
    }
}