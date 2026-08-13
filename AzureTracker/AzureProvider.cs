using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

namespace AzureTracker
{
    public enum AzureObject
    {
        PR,
        WorkItem,
        Build,
        Commit,
        None
    };

    #region Types
    public class AzureObjectBase
    {
        public Int64 ID { get; set; }
        public string? ProjectName { get; set; } = string.Empty;

        public string? Title { get; set; } = string.Empty;
        public string? Status { get; set; } = string.Empty;

        public Uri? Uri { get; set; }

        public string? CreatedBy { get; set; } = string.Empty;
    }

    public class WorkItem : AzureObjectBase 
    {
        public DateTime? ChangedDate { get; set; }
        public string? Type { get; set; } = string.Empty;
        public string? AssignedTo { get; set; } = string.Empty;
        public string? ChangedBy { get; set; } = string.Empty;

        public string? Priority { get; set; } = string.Empty;

        public string? RnDPriority { get; set; } = string.Empty;

        public string? IterationPath { get; set; } = string.Empty;

        public string? AreaPath { get; set; } = string.Empty;

        public string? Tags { get; set; } = string.Empty;

        public DateTime? ResolvedDate { get; set; }

        public string? ResolvedBy { get; set; } = string.Empty;

        public DateTime? CreatedDate { get; set; }
    }
    
    public class PR : AzureObjectBase
    {
        public string? RepoName { get; set; } = string.Empty;
        public string? SourceBranch { get; set; } = string.Empty;

        public string? TargetBranch { get; set; } = string.Empty;
        public string? Reviewers { get; set; } = string.Empty;
        public string? IsDraft { get; internal set; } = string.Empty;

        public DateTime? CreatedDate { get; set; }

        public DateTime? ChangeDate { get; set; }
    }

    public class Build : AzureObjectBase 
    {
        public string? RepoName { get; set; } = string.Empty;
        public string? Branch { get; set; } = string.Empty;

        public string? Result { get; internal set; } = string.Empty;
        public string? Definition { get; internal set; } = string.Empty;
        public DateTime? QueueTime { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }
		
        public string? PoolName { get; set; } = string.Empty;

        public string? Parameters { get; set; } = string.Empty;
    }

    public class Commit : AzureObjectBase
    {
        public string? RepoName { get; set; } = string.Empty;

        public string? CommitID { get; set; } = string.Empty;

        public DateTime? TimeStamp { get; set; }
    }


    #endregion
    public class AzureProvider
    {
        public delegate void DataFetchEventHandler(AzureObject azureObject, string? projName, string? param);
        public event DataFetchEventHandler? DataFetchEvent;

        const string API_VERSION = "api-version=7.1";
        AzureProviderConfig m_APConfig = new AzureProviderConfig();
        public string AzureEndPoint
        {
            get { return $"https://dev.azure.com/{m_APConfig.Organization}"; }
        }

        public class AzureProviderConfig
        {
            public string Organization { get; set; } = string.Empty;
            public string PAT { get; set; } = string.Empty;

            public string[] WorkItemTypes { get; set; } = new string[0];

            public double BuildNotOlderThanDays { get; set; } = 5;

            public int MaxBuildsPerDefinition { get; set; } = 100;
            public object MaxCommitsPerRepo { get; internal set; } = 100;

            public bool UseCaching = true;
        }
        public AzureProvider() { }

        public bool Init(AzureProviderConfig apc)
        {
            bool res = true;
            try
            {
                if (apc.UseCaching)
                {
                    LoadCahcedAzureItems();
                }
                m_APConfig = apc;

                if (m_APConfig.WorkItemTypes.Length > 0)
                {
                    foreach (var type in m_APConfig.WorkItemTypes)
                    {
                        m_witTypeSubQuery += $"[System.WorkItemType] = '{type}' OR ";
                    }
                    m_witTypeSubQuery = " AND (" + m_witTypeSubQuery.Remove(m_witTypeSubQuery.Length - 4, 3) + ")";
                }
                Projects = GetProjectList();
            }
            catch(Exception e)
            {
                Logger.Instance.Error(e.Message);
                res = false;
            }
            return res;
        }

        private string m_witTypeSubQuery = string.Empty;

        #region Cache

        const string Cache = "AzureItemsCache";
        readonly string PRCache = Path.Combine(Cache, "PRs");
        readonly string WITCache = Path.Combine(Cache, "WorkItems");
        readonly string BuildCache = Path.Combine(Cache, "Builds");
        readonly string CommitCache = Path.Combine(Cache, "Commits");
        private void LoadCahcedAzureItems()
        {
            if (Directory.Exists(Cache))
            {
                LoadFromCache<PR>(PRCache, PRs);
                LoadFromCache<WorkItem>(WITCache, WorkItems);
                LoadFromCache<Build>(BuildCache, Builds);
                LoadFromCache<Commit>(CommitCache, Commits);
            }
        }

        void LoadFromCache<T>(string cacheName, Dictionary<Int64, AzureObjectBase> dest) where T : AzureObjectBase, new()
        {
            try
            {
                if (File.Exists(cacheName))
                {
                    using (FileStream fileStream = new FileStream(cacheName, FileMode.Open, FileAccess.Read))
                    {
                        var obj = JsonNode.Parse(fileStream);
                        if (obj != null)
                        {
                            foreach (var item in obj.AsObject().AsEnumerable())
                            {
                                if (item.Value != null)
                                    dest[Int64.Parse(item.Key)] = Parse<T>(item.Value);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) 
            {
                Logger.Instance.Error(ex.Message);
            }
        }

        T Parse<T>(JsonNode node) where T : new()
        {
            var tType = typeof(T);
            T result = new T();
            foreach (var pi in tType.GetProperties())
            {
                string key = pi.Name;
                var val = node?[key];
                if (val! != null)
                {
                    var valStr = val.ToString();
                    pi.SetValue(
                        result, 
                        TypeDescriptor.GetConverter(pi.PropertyType).ConvertFromString(valStr));
                }
            }

            return result;
        }

        public void Save()
        {
            if (m_APConfig.UseCaching)
            {
                SaveAzureItems();
            }
        }

        private void SaveAzureItems()
        {
            if (!Directory.Exists(Cache))
                Directory.CreateDirectory(Cache);

            SaveAzureItems<PR>(PRCache, PRs);
            SaveAzureItems<WorkItem>(WITCache, WorkItems);
            SaveAzureItems<Build>(BuildCache, Builds);
            SaveAzureItems<Commit>(CommitCache, Commits);
        }

        void SaveAzureItems<T>(string cacheName, Dictionary<Int64, AzureObjectBase> src) where T : AzureObjectBase 
        {
            if (src.Count>0)
            {
                using (FileStream fileStream = new FileStream(cacheName, FileMode.Create, FileAccess.Write))
                {
                    JsonSerializer.Serialize(fileStream, src.ToDictionary(p => p.Key, p => (T)p.Value));
                }
            }
        }

        #endregion

        static void SetClientAuth(HttpClient client, string PAT)
        {
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(
                    System.Text.ASCIIEncoding.ASCII.GetBytes(
                        string.Format("{0}:{1}", "", PAT))));
        }

        class Project
        {
            internal string? Name = string.Empty;
            internal List<string?> Repos { get; set; } = new List<string?>();
        }
        List<Project?> Projects { get; set; } = new List<Project?>();

        Dictionary<Int64, AzureObjectBase> PRs { get; set; } = new Dictionary<Int64, AzureObjectBase>();
        Dictionary<Int64, AzureObjectBase> WorkItems { get; set; } = new Dictionary<Int64, AzureObjectBase>();

        Dictionary<Int64, AzureObjectBase> Builds { get; set; } = new Dictionary<Int64, AzureObjectBase>();

        Dictionary<Int64, AzureObjectBase> Commits { get; set; } = new Dictionary<Int64, AzureObjectBase>();

        public List<AzureObjectBase> Get(AzureObject selectedAzureObject)
        {
            switch (selectedAzureObject)
            {
                case AzureObject.PR:
                    return PRs.Values.ToList();
                case AzureObject.WorkItem:
                    return WorkItems.Values.ToList();
                case AzureObject.Build:
                    return Builds.Values.ToList();
                case AzureObject.Commit:
                    return Commits.Values.ToList();
            }

            return new List<AzureObjectBase>();
        }
        public void Sync(AzureObject azureObject)
        {
            SetSyncInProgress(true);
            switch (azureObject)
            {
                case AzureObject.PR:
                    PRs = (PRs.Count == 0) ? GetPRs(PullRequestStatus.All) : GetPRs(PullRequestStatus.Active);
                    break;
                case AzureObject.WorkItem:
                    WorkItems = GetWorkItems();
                    break;
                case AzureObject.Build:
                    Builds = GetBuilds(
                        DateTime.Now.AddDays(-m_APConfig.BuildNotOlderThanDays), 
                        m_APConfig.MaxBuildsPerDefinition);
                    break;
                case AzureObject.Commit:
                    Commits = GetCommits();
                    break;
                default:
                    foreach (AzureObject ao in (AzureObject[])Enum.GetValues(typeof(AzureObject)))
                    {
                        if (ao != AzureObject.None)
                            Sync(ao);
                    }
                    break;
            }

            SetSyncInProgress(false);
        }

        private List<long> GetLatestWITChanges(string? projName, DateTime fromDate)
        {
            List<long> lstWITIDs = new List<long>();

            string formattedDate = fromDate.ToUniversalTime().ToString("yyyy-MM-dd");

            if (projName != null)
            {
                string wiqlQuery = "{\n \"query\":\""
                    + $"SELECT [System.Id] FROM WorkItems WHERE (([System.TeamProject] = '{projName}') "
                    + $"AND ([System.ChangedDate] > '{formattedDate}')) "
                    + $"ORDER BY [System.ChangedDate] DESC "
                    + "\" \n}";
                string uri = $"{AzureEndPoint}/{projName}/_apis/wit/wiql?{API_VERSION}";
                string sResponse = string.Empty;

                if (AzurePostRequest(uri, wiqlQuery, out sResponse))
                {
                    JsonNode? json = JsonNode.Parse(sResponse);
                    if (json != null)
                    {
                        var workitems = json?["workItems"]?.AsArray();

                        for (int i = 0; i < workitems?.Count; ++i)
                        {
                            JsonNode? wit = workitems[i];
                            var strID = wit?["id"]?.ToString();
                            if (strID != null)
                                lstWITIDs.Add(long.Parse(strID));
                        }
                    }
                }
                else
                {
                    throw new Exception($"GetLatestWITChanges => {sResponse}");
                }
            }

            return lstWITIDs;
        }

        private List<Project?> GetProjectList()
        {
            List<Project?> lstProjects = new List<Project?>();
            string sResponse = string.Empty;
            string uri = $"{AzureEndPoint}/_apis/projects?{API_VERSION}";
            Logger.Instance.Info($"GetProjectList from {AzureEndPoint}");
            if (AzureGetRequest(uri, out sResponse))
            {
                JsonNode? json = JsonNode.Parse(sResponse);
                if (json != null)
                {
                    JsonNode? value = json["value"];
                    JsonArray? jsonProjects = value?.AsArray();

                    for (int i = 0; i < jsonProjects?.Count; ++i)
                    {
                        JsonNode? project = jsonProjects[i];
                        string? name = project?["name"]?.ToString();
                        Project p = new Project();
                        p.Name = name;
                        p.Repos = GetProjectRepos(p);
                        lstProjects.Add(p);
                    }
                }
            }
            else
            {
                throw new Exception($"GetProjectList => {sResponse}");
            }

            return lstProjects;
        }

        private List<string?> GetProjectRepos(Project p)
        {
            List<string?> repos = new List<string?>();
            string sResponse = string.Empty;
            string uri = $"{AzureEndPoint}/{p.Name}/_apis/git/repositories?{API_VERSION}";
            if (AzureGetRequest(uri, out sResponse))
            {
                JsonNode? json = JsonNode.Parse(sResponse);
                JsonArray? jsonRepos = json?["value"]?.AsArray();

                for (int i = 0; i < jsonRepos?.Count; ++i)
                {
                    JsonNode? jsonRepo = jsonRepos[i];
                    bool isDisabled = true;
                    if (bool.TryParse(jsonRepo?["isDisabled"]?.ToString(), out isDisabled)
                        && !isDisabled)
                    {
                        repos.Add(jsonRepo?["name"]?.ToString());
                    }
                    else
                    {
                        Logger.Instance.Info($"{jsonRepo?["name"]?.ToString()} repository in project {p.Name} is disabled.");
                    }
                }

                return repos;
            }
            else
            {
                throw new Exception($"GetProjectRepos => {sResponse}");
            }
        }
        #region work items
        private Dictionary<Int64, AzureObjectBase> GetWorkItems()
        {
            Dictionary<Int64, AzureObjectBase> dicWorkItems = WorkItems;
            for (int i = 0; i < Projects?.Count; ++i)
            {
                if (Aborting)
                    return WorkItems;

                DataFetchEvent?.Invoke(AzureObject.WorkItem, Projects[i]?.Name, "in progress");
                var dt = DateTime.Now;
                GetWorkItemsByProject(dicWorkItems, Projects?[i]);
                DataFetchEvent?.Invoke(AzureObject.WorkItem, Projects?[i]?.Name, $"took {(DateTime.Now - dt).TotalSeconds} seconds");
            }
            WITUpdateFromDate = DateTime.Now;
            return dicWorkItems;
        }

        private DateTime WITUpdateFromDate = DateTime.Now.AddDays(-30);

        private void GetWorkItemsByProject(Dictionary<Int64, AzureObjectBase> dicWorkItems, Project? p)
        {
            const int WIT_PER_CALL = 19999;
            const int WIT_BY_ID_PER_CALL = 199;
            Int64 skip = -1;
            if (dicWorkItems.Count > 0) //not first time
            {
                var projWITs = dicWorkItems.Values.Where(x => x.ProjectName == p?.Name).OrderByDescending(x => x.ID);
                if (projWITs.Count() > 0)
                {
                    skip = projWITs.First().ID;
                }
            }

            string sResponse = string.Empty;

            var hsWitID = new HashSet<Int64>();
            while (true)
            {
                if (Aborting)
                    break;

                var jsonstr = "{\n \"query\":\""
                    + $"SELECT [System.Id] FROM WorkItems Where (([System.TeamProject] = '{p?.Name}')"
                    + $"{m_witTypeSubQuery} AND ([System.Id]>{skip})) "
                    + $"ORDER BY [System.Id] ASC"
                    + "\" \n}";
                string uri = $"{AzureEndPoint}/{p?.Name}/_apis/wit/wiql?{API_VERSION}&$top={WIT_PER_CALL}";

                if (AzurePostRequest(uri, jsonstr, out sResponse))
                {
                    JsonNode? json = JsonNode.Parse(sResponse);
                    JsonArray? jsonWITs = json?["workItems"]?.AsArray();

                    if (jsonWITs?.Count > 0)
                    {
                        for (int i = 0; i < jsonWITs?.Count; ++i)
                        {
                            JsonNode? jsonWIT = jsonWITs[i];
                            int? nID = jsonWIT?["id"]?.GetValue<int>();

                            if (nID.HasValue)
                            {
                                hsWitID.Add(nID.Value);
                                if (skip < nID.Value)
                                    skip = nID.Value;
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    throw new Exception($"GetWorkItemsByProject => {sResponse}");
                }
            }

            //update changed items per project
            hsWitID.AddRange(GetLatestWITChanges(p?.Name, WITUpdateFromDate));

            if (hsWitID.Count > 0)
            {
                Logger.Instance.Info($"updating {hsWitID.Count} work items from {p?.Name}");

                var lstWitID = hsWitID.ToList();
                for (int i = 0; i < lstWitID.Count; i += WIT_BY_ID_PER_CALL)
                {
                    if (Aborting)
                        break;

                    List<Int64> lstIDRange = lstWitID.GetRange(i, Math.Min(lstWitID.Count - i, WIT_BY_ID_PER_CALL));
                    if (lstIDRange?.Count > 0)
                        GetWorkItemsByIDs(dicWorkItems, lstIDRange);
                }
            }
        }

        private void GetWorkItemsByIDs(Dictionary<Int64, AzureObjectBase> dicWorkItems, List<Int64> witIDs)
        {
            string witStrIDs = string.Empty;
            foreach (int? id in witIDs)
            {
                witStrIDs += id.ToString();
                witStrIDs += ",";
            }

            witStrIDs = witStrIDs.Remove(witStrIDs.Length - 1);

            string sResponse = string.Empty;
            string uri =
                $"{AzureEndPoint}/_apis/wit/workitems?ids={witStrIDs}&{API_VERSION}";

            if (AzureGetRequest(uri, out sResponse))
            {
                JsonNode? json = JsonNode.Parse(sResponse);

                JsonArray? jsonWITs = json?["value"]?.AsArray();

                for (int i = 0; i < jsonWITs?.Count; ++i)
                {
                    JsonNode? jsonWIT = jsonWITs[i];

                    if (jsonWIT != null)
                    {
                        WorkItem wit = ParseWIT(jsonWIT);
                        dicWorkItems[wit.ID] = wit;
                    }
                }
            }
            else if (sResponse.Contains("TF401232")) //Work item does not exist, or you do not have permissions to read it
            {
                foreach (var s in sResponse.Split(' '))
                {
                    int id;
                    if (int.TryParse(s, out id))
                    {
                        witIDs.Remove(id);
                        dicWorkItems.Remove(id);
                        break;
                    }
                }
                GetWorkItemsByIDs(dicWorkItems, witIDs);
            }
            else
            {
                throw new Exception($"GetWorkItemsByIDs => {sResponse}");
            }
        }

        private WorkItem ParseWIT(JsonNode? jsonWIT)
        {
            WorkItem wit = new WorkItem();

            var id = jsonWIT?["id"]?.GetValue<int>();
            wit.ID = id.HasValue? id.Value : -1;
            JsonNode? fields = jsonWIT?["fields"];
            wit.ProjectName = fields?["System.TeamProject"]?.ToString();
            wit.Title = fields?["System.Title"]?.ToString();
            wit.Status = fields?["System.State"]?.ToString();
            wit.Uri = new UriBuilder($"{AzureEndPoint}/{wit.ProjectName}/_workitems/edit/" + wit.ID).Uri;
            wit.CreatedBy = fields?["System.CreatedBy"]?["displayName"]?.ToString();
            wit.Type = fields?["System.WorkItemType"]?.ToString();
            wit.ChangedBy = fields?["System.ChangedBy"]?["displayName"]?.ToString();
            wit.Priority = fields?["Microsoft.VSTS.Common.Priority"]?.ToString();
            wit.RnDPriority = fields?["Custom.RnDPriority"]?.ToString();
            wit.IterationPath = fields?["System.IterationPath"]?.ToString();
            wit.AreaPath = fields?["System.AreaPath"]?.ToString();
            wit.Tags = fields?["System.Tags"]?.ToString();
            wit.CreatedDate = fields?["System.CreatedDate"]?.GetValue<DateTime>();
            wit.ChangedDate = fields?["System.ChangedDate"]?.GetValue<DateTime>();

            JsonObject? obj = fields?.AsObject();
            if (obj != null && obj.ContainsKey("System.AssignedTo"))
            {
                wit.AssignedTo = jsonWIT?["fields"]?["System.AssignedTo"]?["displayName"]?.ToString();
            }

            wit.ResolvedDate = fields?["Microsoft.VSTS.Common.ResolvedDate"]?.GetValue<DateTime>();

            if (obj != null && obj.ContainsKey("Microsoft.VSTS.Common.ResolvedBy"))
            {
                wit.ResolvedBy = fields?["Microsoft.VSTS.Common.ResolvedBy"]?["displayName"]?.ToString();
            }
            return wit;
        }

        #endregion

        #region Commits

        private Dictionary<Int64, AzureObjectBase> GetCommits()
        {
            Dictionary<Int64, AzureObjectBase> dicCommits = new Dictionary<Int64, AzureObjectBase>();
            for (int i = 0; i < Projects?.Count; ++i)
            {
                if (Aborting)
                    return Commits;

                DataFetchEvent?.Invoke(AzureObject.Commit, Projects[i]?.Name, "in progress");
                var dt = DateTime.Now;
                GetCommitsByProject(dicCommits, Projects[i]);
                DataFetchEvent?.Invoke(AzureObject.Commit, Projects[i]?.Name, $"took {(DateTime.Now - dt).TotalSeconds}");
            }

            return dicCommits;
        }

        private void GetCommitsByProject(Dictionary<Int64, AzureObjectBase> dicCommits, Project? project)
        {
            for (int i = 0; i < project?.Repos.Count; ++i)
            {
                if (Aborting)
                    break;

                GetCommitsByProjectAndRepo(dicCommits, project.Name, project.Repos[i]);
            }
        }

        private void GetCommitsByProjectAndRepo(Dictionary<Int64, AzureObjectBase> dicCommits, string? p, string? r)
        {
            //GET https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repositoryId}/commits?api-version=7.1

            string sResponse = string.Empty;
            string uri =
                $"{AzureEndPoint}/{p}/_apis/git/repositories/{r}/commits?&searchCriteria.$top={m_APConfig.MaxCommitsPerRepo}&{API_VERSION}";

            if (AzureGetRequest(uri, out sResponse))
            {
                JsonNode? json = JsonNode.Parse(sResponse);

                if (json != null)
                {
                    ParseCommits(dicCommits, json, p, r);
                }
            }
            else
            {
                throw new Exception($"GetCommitsByProjectAndRepo => {sResponse}");
            }
        }

        private Commit GetCommitByID(string? p, string? r, string commitID)
        {
            //GET https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repositoryId}/commits/{commitId}?api-version=7.1
            string sResponse = string.Empty;
            string uri =
                $"{AzureEndPoint}/{p}/_apis/git/repositories/{r}/commits/{commitID}?{API_VERSION}";
            if (AzureGetRequest(uri, out sResponse))
            {
                JsonNode? json = JsonNode.Parse(sResponse);
                if (json != null)
                {
                    return ParseCommit(json, p, r);
                }
            }
            else
            {
                throw new Exception($"GetCommitByID => {sResponse}");
            }
            return new Commit();
        }

        private void ParseCommits(Dictionary<Int64, AzureObjectBase> dicCommits, JsonNode jsonCommit, string? p, string? r)
        {
            JsonArray? jsonCommits = jsonCommit?["value"]?.AsArray();
            for (int j = 0; j < jsonCommits?.Count; ++j)
            {
                Commit commit = ParseCommit(jsonCommits[j], p, r);
                if (!string.IsNullOrWhiteSpace(commit.CommitID))
                {
                    while(dicCommits.ContainsKey(commit.ID))
                    {
                        Logger.Instance.Warn($"Commit ID {commit.ID} already exists. generating new...");
                        commit.ID++;
                    }
                    dicCommits[commit.ID] = commit;
                }
                else
                {
                    throw new Exception($"ParseCommits => commitid is invalid");
                }
            }
        }

        Commit ParseCommit(JsonNode? jsonCommit, string? p, string? r)
        {
            Commit commit = new Commit();

            var id = jsonCommit?["commitId"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(id))
            {
                commit.ID = Int64.Parse(id.Substring(id.Length - 8), System.Globalization.NumberStyles.HexNumber);
                commit.CommitID = id;
                commit.ProjectName = p;
                commit.RepoName = r;
                commit.CreatedBy = jsonCommit?["committer"]?["name"]?.ToString();
                commit.Title = jsonCommit?["comment"]?.ToString();
                commit.TimeStamp = jsonCommit?["committer"]?["date"]?.GetValue<DateTime>();
                commit.Status = $"Add:{jsonCommit?["changeCounts"]?["Add"]?.ToString()}" +
                    $", Edit: {jsonCommit?["changeCounts"]?["Edit"]?.ToString()}" +
                    $", Delete: {jsonCommit?["changeCounts"]?["Delete"]?.ToString()}";
                var uri = jsonCommit?["remoteUrl"]?.ToString();

                if (!string.IsNullOrWhiteSpace(uri))
                    commit.Uri = new UriBuilder(uri).Uri;
            }

            return commit;
        }

        #endregion

        #region PR
        private Dictionary<Int64, AzureObjectBase> GetPRs(PullRequestStatus status)
        {
            Dictionary<Int64, AzureObjectBase> dicPRs = PRs;
            for (int i = 0; i < Projects.Count; ++i)
            {
                if (Aborting)
                    return PRs;

                DataFetchEvent?.Invoke(AzureObject.PR, Projects[i]?.Name, "in progress");
                var dt = DateTime.Now;
                GetPRsByProject(dicPRs, Projects[i], status);
                DataFetchEvent?.Invoke(AzureObject.PR, Projects[i]?.Name, $"took {(DateTime.Now - dt).TotalSeconds}");
            }

            return dicPRs;
        }

        readonly string PR_ACTIVE = PullRequestStatus.Active.ToString().ToLower();

        private void GetPRsByProject(Dictionary<Int64, AzureObjectBase> dicPRs, Project? p, PullRequestStatus status)
        {
            const int PRS_PER_CALL = 101;
            int skip = 0;
            HashSet<Int64>? activePRs = null;
            if (status == PullRequestStatus.Active)
            {
                activePRs = PRs.Where(
                    pr => pr.Value.Status == PR_ACTIVE &&
                    pr.Value.ProjectName == p?.Name)
                    .Select(p => p.Value.ID).ToHashSet();
            }

            while (true)
            {
                if (Aborting)
                    break;

                string sResponse = string.Empty;
                string uri =
                    $"{AzureEndPoint}/{p?.Name}/_apis/git/pullrequests?searchCriteria.status={status.ToString()}&$skip={skip}&{API_VERSION}";

                if (AzureGetRequest(uri, out sResponse))
                {
                    JsonNode? json = JsonNode.Parse(sResponse);
                    JsonArray? jsonPRs = json?["value"]?.AsArray();

                    if (jsonPRs?.Count == 0)
                        break;

                    for (int i = 0; i < jsonPRs?.Count; ++i)
                    {
                        JsonNode? jsonPR = jsonPRs[i];
                        if (jsonPR != null)
                        {
                            PR pr = ParsePR(jsonPR);

                            if (activePRs?.Count >0)
                            {
                                if (activePRs.Contains(pr.ID))
                                {
                                    activePRs.Remove(pr.ID);
                                }
                            }
                            dicPRs[pr.ID] = pr;
                        }
                    }
                    skip += PRS_PER_CALL;
                }
                else
                {
                    throw new Exception($"GetPRsByProject => {sResponse}");
                }
            }

            //update PRs that are not active anymore
            if (activePRs != null)
            {
                foreach (var prID in activePRs)
                {
                    string sResponse = string.Empty;
                    string uri =
                        $"{AzureEndPoint}/{p?.Name}/_apis/git/pullrequests/{prID}?{API_VERSION}";

                    if (AzureGetRequest(uri, out sResponse))
                    {
                        JsonNode? json = JsonNode.Parse(sResponse);

                        if (json != null)
                        {
                            dicPRs[prID] = ParsePR(json);
                        }
                    }
                    else
                    {
                        Logger.Instance.Error($"GetPRsByProject => {sResponse}");
                    }
                }
            }
        }

        PR ParsePR(JsonNode? jsonPR)
        {
            PR pr = new PR();

            var id = jsonPR?["pullRequestId"]?.GetValue<int>(); 
            pr.ID = id.HasValue ? id.Value : -1;
            pr.ProjectName = jsonPR?["repository"]?["project"]?["name"]?.ToString();
            pr.RepoName = jsonPR?["repository"]?["name"]?.ToString();
            pr.Title = jsonPR?["title"]?.ToString();
            pr.Uri = new UriBuilder($"{AzureEndPoint}/{pr.ProjectName}/_git/{pr.RepoName}" + "/pullrequest/" + pr.ID).Uri;
            pr.Status = jsonPR?["status"]?.ToString();
            pr.CreatedBy = jsonPR?["createdBy"]?["displayName"]?.ToString();
            pr.SourceBranch = jsonPR?["sourceRefName"]?.ToString();
            pr.TargetBranch = jsonPR?["targetRefName"]?.ToString();
            string? sDateTime = jsonPR?["creationDate"]?.ToString();
            DateTime dt;
            if (DateTime.TryParse(sDateTime, out dt))
                pr.CreatedDate = dt;

            pr.IsDraft = jsonPR?["isDraft"]?.ToString();
            //pr.ChangedDate = jsonWIT["fields"]["System.ChangedDate"].ToString();

            if (pr.Status?.ToLower() == PR_ACTIVE)
            {
                var lastMergeCommitID = jsonPR?["lastMergeCommit"]?["commitId"]?.ToString();
                var lastMergeCommit = GetCommitByID(pr.ProjectName, pr.RepoName, lastMergeCommitID!);

                if  (lastMergeCommit != null)
                {
                    pr.ChangeDate = lastMergeCommit.TimeStamp;
                }
            }

            JsonArray? jsonReviewers = jsonPR?["reviewers"]?.AsArray();
            for (int j = 0; j < jsonReviewers?.Count; ++j)
            {
                pr.Reviewers += jsonReviewers[j]?["displayName"]?.ToString() + "; ";
            }

            return pr;
        }

        #endregion

        #region Build

        private Dictionary<Int64, AzureObjectBase> GetBuilds(DateTime earliestCreateDate, int maxBuildsPerDefinition)
        {
            Dictionary<Int64, AzureObjectBase> dicBuilds = new Dictionary<Int64, AzureObjectBase>();
            for (int i = 0; i < Projects?.Count; ++i)
            {
                if (Aborting)
                    return Builds;

                DataFetchEvent?.Invoke(AzureObject.Build, Projects[i]?.Name, "in progress");
                var dt = DateTime.Now;
                GetBuildsByProject(dicBuilds, Projects[i], earliestCreateDate, maxBuildsPerDefinition);
                DataFetchEvent?.Invoke(AzureObject.Build, Projects[i]?.Name, $"took {(DateTime.Now - dt).TotalSeconds}");
            }

            return dicBuilds;
        }

        private bool QueueTimeEarlierThan(JsonNode? jsonBuild, DateTime dt)
        {
            DateTime dtQ;
            if (DateTime.TryParse(jsonBuild?["queueTime"]?.ToString(), out dtQ)
                && dtQ > dt)
            {
                return true;
            }
            return false;
        }
        private void GetBuildsByProject(Dictionary<Int64, AzureObjectBase> dicBuilds, Project? p, 
            DateTime earliestCreateDate, int maxBuildsPerDefinition)
        {
            string sResponse = string.Empty;
            string uri =
                $"{AzureEndPoint}/{p?.Name}/_apis/build/builds?maxBuildsPerDefinition={maxBuildsPerDefinition}&?{API_VERSION}";

            if (AzureGetRequest(uri, out sResponse))
            {
                JsonNode? json = JsonNode.Parse(sResponse);
                JsonNode? value = json?["value"];
                JsonArray? arrBuilds = value?.AsArray();

                if (arrBuilds?.Count > 0)
                {
                    IEnumerable<JsonNode?> jsonBuilds = 
                        arrBuilds.Where((jsonBuild) => QueueTimeEarlierThan(jsonBuild, earliestCreateDate));

                    foreach (JsonNode? jsonBuild in jsonBuilds)
                    {
                        if (jsonBuild != null)
                        {
                            Build build = ParseBuild(jsonBuild);
                            dicBuilds[build.ID] = build;
                        }
                    }
                }
            }
            else
            {
                throw new Exception($"GetPRsByProject => {sResponse}");
            }
        }

        private Build ParseBuild(JsonNode? jsonBuild)
        {
            Build build = new Build();
            var id = jsonBuild?["id"]?.GetValue<int>();
            build.ID = id.HasValue ? id.Value : -1;
            build.ProjectName = jsonBuild?["project"]?["name"]?.ToString();
            build.RepoName = jsonBuild?["repository"]?["name"]?.ToString();
            build.Title = jsonBuild?["buildNumber"]?.ToString();
            build.Uri = new UriBuilder($"{AzureEndPoint}/{build.ProjectName}/_build/results?buildId={build.ID}&view=results").Uri;
            build.Status = jsonBuild?["status"]?.ToString();
            build.Result = jsonBuild?["result"]?.ToString();
            build.CreatedBy = jsonBuild?["requestedBy"]?["displayName"]?.ToString();
            build.Branch = jsonBuild?["sourceBranch"]?.ToString();
            string? sDateTime = jsonBuild?["queueTime"]?.ToString();
            DateTime dt1, dt2, dt3;
            if (DateTime.TryParse(sDateTime, out dt1))
                build.QueueTime = dt1;

            sDateTime = jsonBuild?["startTime"]?.ToString();
            if (DateTime.TryParse(sDateTime, out dt2))
                build.StartTime = dt2;

            sDateTime = jsonBuild?["finishTime"]?.ToString();
            if (DateTime.TryParse(sDateTime, out dt3))
                build.FinishTime = dt3;

            build.PoolName = jsonBuild?["queue"]?["pool"]?["name"]?.ToString();

            build.Definition = jsonBuild?["definition"]?["name"]?.ToString();

            build.Parameters = jsonBuild?["parameters"]?.ToString();

            return build;
        }

        #endregion

        private bool AzurePostRequestWithPAT(string uri, string query, string PAT, out string response)
        {
            bool success = false;
            using (HttpClient client = new HttpClient())
            {
                SetClientAuth(client, PAT);

                HttpContent body = new StringContent(query, Encoding.UTF8, "application/json");
                var responseTask = client.PostAsync(uri, body);

                response = responseTask.Result.Content.ReadAsStringAsync().Result;
                success = responseTask.Result.StatusCode == HttpStatusCode.OK;
            }

            return success;
        }

        private bool AzurePostRequest(string uri, string query, out string response)
        {
            return AzurePostRequestWithPAT(uri, query, m_APConfig.PAT, out response);
        }

        private bool AzureGetRequestWithPAT(string uri, string PAT, out string response)
        {
            bool success = false;
            using (HttpClient client = new HttpClient())
            {
                SetClientAuth(client, PAT);

                var responseTask = client.GetAsync(uri);

                response = responseTask.Result.Content.ReadAsStringAsync().Result;
                success = responseTask.Result.StatusCode == HttpStatusCode.OK;
            }

            return success;
        }

        private bool AzureGetRequest(string uri, out string response)
        {
            return AzureGetRequestWithPAT(uri, m_APConfig.PAT, out response);
        }

        long m_bIsSyncInProgress = 0;
        internal void Abort()
        {
            SetSyncInProgress(false);
        }

        private bool Aborting
        {
            get { return Interlocked.Read(ref m_bIsSyncInProgress) == 0; }
        }

        private void SetSyncInProgress(bool bInProgress)
        {
            Interlocked.Exchange(ref m_bIsSyncInProgress, bInProgress ? 1 : 0);
        }

        internal bool SyncAzureObject(AzureObjectBase? aob)
        {
            Dictionary<Int64, AzureObjectBase>? dic = null;
            string uri = string.Empty;
            
            if (aob is PR)
            {
                uri = $"{AzureEndPoint}/{aob?.ProjectName}/_apis/git/pullrequests/{aob?.ID}?{API_VERSION}";
                dic = PRs;
            }
            else if (aob is WorkItem)
            {
                uri = $"{AzureEndPoint}/{aob?.ProjectName}/_apis/wit/workitems/{aob?.ID}?{API_VERSION}";
                dic = WorkItems;
            }
            else if (aob is Build)
            {
                uri = $"{AzureEndPoint}/{aob?.ProjectName}/_apis/build/builds/{aob?.ID}?{API_VERSION}";
                dic = Builds;
            }

            string sResponse = string.Empty;
            if (AzureGetRequest(uri, out sResponse))
            {
                AzureObjectBase? updatedAob = null;
                JsonNode? jsonNode = JsonNode.Parse(sResponse);
                if (aob is PR)
                {
                    updatedAob = ParsePR(jsonNode);
                }
                else if (aob is WorkItem)
                {
                    updatedAob = ParseWIT(jsonNode);
                }
                else if (aob is Build)
                {
                    updatedAob = ParseBuild(jsonNode);
                }

                if (dic != null && updatedAob != null)
                {
                    if (!Compare(aob, updatedAob))
                    {
                        dic[updatedAob.ID] = updatedAob;
                        return true;
                    }
                }
            }
            else if (sResponse.Contains("TF401232")) //item does not exist, or you do not have permissions to read it
            {
                if (dic != null && aob != null)
                {
                    dic.Remove(aob.ID);
                    aob.ID = -1; //item is invalid
                    return true;
                }
            }
            else
            {
                throw new Exception($"SyncAzureObject => {sResponse}");
            }
            return false;
        }


        public static bool Compare<T>(T e1, T e2)
        {
            if ((e1 != null) && (e2 != null)) //don't support null
            {
                foreach (PropertyInfo propObj1 in e1.GetType().GetProperties())
                {
                    var propObj2 = e2.GetType().GetProperty(propObj1.Name);

                    var e1val = propObj1.GetValue(e1, null);
                    var e2val = propObj2?.GetValue(e2, null);

                    if (e1val != null && !e1val.Equals(e2val))
                        return false;
                }
                return true;
            }
            return false;
        }
    }
}
