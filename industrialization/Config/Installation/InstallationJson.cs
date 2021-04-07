using System.Runtime.Serialization;

namespace industrialization.Config.Installation
{
    [DataContract] 
    class InstallationJson
    {
        [DataMember(Name = "installations")]
        private InstallationData[] _installations;

        public InstallationData[] Installations => _installations;
    }
}