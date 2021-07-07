using System;

namespace industrialization.Core.Installation
{
    public abstract class InstallationBase
    {
        protected int InstallationID;
        protected int intID;

        public int InstallationId => InstallationID;

        public int IntId => intID;

        protected InstallationBase(int installationId, int intID)
        {
            this.InstallationID = installationId;
            this.intID = intID;
        }
    }
}