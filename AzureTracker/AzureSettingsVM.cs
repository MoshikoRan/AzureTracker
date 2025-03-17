namespace AzureTracker
{
    public class AzureSettingsVM
    {
        public string OrganizationName { get; set; } = string.Empty;
        public string PAT { get; set; } = string.Empty;

        public AzureSettingsVM(string organizationName, string pat)
        {
            OrganizationName = organizationName;
            PAT = pat;
        }
    }
}