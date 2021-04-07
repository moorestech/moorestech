using System.Runtime.Serialization;

namespace industrialization.Config
{
    [DataContract] 
    class InstallationJson
    {
        [DataMember(Name = "installations")]
        private InstallationData[] _installations;

        public InstallationData[] Installations => _installations;
    }
}