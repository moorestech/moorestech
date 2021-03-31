using System;

namespace industrialization.Installation
{
    public abstract class InstallationBase
    {
        protected int InstallationID;
        protected Guid GUID;

        public int InstallationId => InstallationID;

        public Guid Guid => GUID;

        protected InstallationBase(int installationId, Guid guid)
        {
            InstallationID = installationId;
            GUID = guid;
        }
    }
}