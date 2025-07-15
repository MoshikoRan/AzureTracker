namespace AzureTracker
{
    public interface IAzureSettings
    {
        public string Organization { get; set; }
        public string PAT { get; set; }
        public string WorkItemTypes { get; set; }
        public int BuildNotOlderThanDays { get; set; }
        public int MaxBuildsPerDefinition { get; set; } 
        public int MaxCommitsPerRepo { get; set; }
        public bool UseCaching { get; set; }
    }
    public class AzureSettingsVM
    {
        public string Organization { get; set; } = string.Empty;

        public IAzureSettings AzureSettings { get; set; }

        public AzureSettingsVM(IAzureSettings azureSettings)
        {
            AzureSettings = azureSettings;
        }
    }
}