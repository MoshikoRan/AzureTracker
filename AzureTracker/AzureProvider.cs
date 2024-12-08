using Microsoft.TeamFoundation.SourceControl.WebApi;
using System;
using System.Collections.Generic;
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
        None,
        PR,
        WorkItem,
        Build
    };

    #region Types
    public class AzureObjectBase
    {
        public int ID { get; set; }
        public string? ProjectName { get; set; } = string.Empty;

        public string? Title { get; set; } = string.Empty;
        public string? Status { get; set; } = string.Empty;

        public Uri? Uri { get; set; }

        public string? CreatedBy { get; set; } = string.Empty;

        public DateTime? CreatedDate { get; set; }
    }

    public class WIT : AzureObjectBase 
    {
        public DateTime? ChangedDate { get; set; }
        public string? Type { get; set; } = string.Empty;
        public string? AssignedTo { get; set; } = string.Empty;
        public string? ChangedBy { get; set; } = string.Empty;

        public string? Priority { get; set; } = string.Empty;

        public string? RnDPriority { get; set; } = string.Empty;

        public string? IterationPath { get; set; } = string.Empty;

        public string? Tags { get; set; } = string.Empty;

        public DateTime? ResolvedDate { get; set; }

        public string? ResolvedBy { get; set; } = string.Empty;
    }
    
    public class PR : AzureObjectBase
    {
        public string? RepoName { get; set; } = string.Empty;
        public string? SourceBranch { get; set; } = string.Empty;

        public string? TargetBranch { get; set; } = string.Empty;
        public string? Reviewers { get; set; } = string.Empty;
        public string? IsDraft { get; internal set; } = string.Empty;
    }

    public class Build : AzureObjectBase 
    {
        public string? RepoName { get; set; } = string.Empty;
        public string? Branch { get; set; } = string.Empty;

        public string? Result { get; internal set; } = string.Empty;
        public string? Definition { get; internal set; } = string.Empty;
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

            public bool UseCaching = true;
        }
        public AzureProvider(AzureProviderConfig apc)
        {
            if (apc.UseCaching)
            {
                LoadCahcedAzureItems();
            }
            m_APConfig = apc;
            Projects = GetProjectList();
        }
        private AzureProvider() { }

        const string Cache = "AzureItemsCache";
        readonly string PRCache = Path.Combine(Cache, "PRs");
        readonly string WITCache = Path.Combine(Cache, "WorkItems");
        readonly string BuildCache = Path.Combine(Cache, "Builds");
        private void LoadCahcedAzureItems()
        {
            if (Directory.Exists(Cache))
            {
                try
                {
                    FileStream fileStream = new FileStream(PRCache, FileMode.Open, FileAccess.Read);
                    var obj = JsonNode.Parse(fileStream);
                    if (obj != null)
                    {
                        foreach (var pr in obj.AsObject().AsEnumerable())
                        {
                            PRs[int.Parse(pr.Key)] = ParsePR(pr.Value);
                        }
                    }

                    fileStream = new FileStream(WITCache, FileMode.Open, FileAccess.Read);
                    obj = JsonNode.Parse(fileStream);
                    if (obj != null)
                    {
                        foreach (var wit in obj.AsObject().AsEnumerable())
                        {
                            WorkItems[int.Parse(wit.Key)] = ParseWIT(wit.Value);
                        }
                    }

                    fileStream = new FileStream(BuildCache, FileMode.Open, FileAccess.Read);
                    obj = JsonNode.Parse(fileStream);
                    if (obj != null)
                    {
                        foreach (var build in obj.AsObject().AsEnumerable())
                        {
                            Builds[int.Parse(build.Key)] = ParseBuild(build.Value);
                        }
                    }
                }
                catch (Exception ex) 
                {
                    Logger.Instance.Error(ex.Message);
                }

            }
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

            FileStream fileStream = new FileStream(PRCache, FileMode.Create, FileAccess.Write);
            JsonSerializer.Serialize(fileStream,PRs);
            fileStream.Flush();
            fileStream.Close();

            fileStream = new FileStream(WITCache, FileMode.Create, FileAccess.Write);
            JsonSerializer.Serialize(fileStream, WorkItems);
            fileStream.Flush();
            fileStream.Close();

            fileStream = new FileStream(BuildCache, FileMode.Create, FileAccess.Write);
            JsonSerializer.Serialize(fileStream, Builds);
            fileStream.Flush();
            fileStream.Close();
        }

        void SetClientAuth(HttpClient client)
        {
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(
                    System.Text.ASCIIEncoding.ASCII.GetBytes(
                        string.Format("{0}:{1}", "", m_APConfig.PAT))));
        }

        class Project
        {
            internal string? Name = string.Empty;
            internal List<string?> Repos { get; set; } = new List<string?>();
        }
        List<Project?> Projects { get; set; } = new List<Project?>();

        Dictionary<int,AzureObjectBase> PRs { get; set; } = new Dictionary<int, AzureObjectBase>();
        Dictionary<int, AzureObjectBase> WorkItems { get; set; } = new Dictionary<int, AzureObjectBase>();

        Dictionary<int, AzureObjectBase> Builds { get; set; } = new Dictionary<int, AzureObjectBase>();

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
        private List<Project?> GetProjectList()
        {
            List<Project?> lstProjects = new List<Project?>();
            string sResponse = string.Empty;
            string uri = $"{AzureEndPoint}/_apis/projects?{API_VERSION}";
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
                    repos.Add(jsonRepo?["name"]?.ToString());
                }

                return repos;
            }
            else
            {
                throw new Exception($"GetProjectRepos => {sResponse}");
            }
        }
        #region work items
        private Dictionary<int, AzureObjectBase> GetWorkItems()
        {
            Dictionary<int, AzureObjectBase> dicWorkItems = WorkItems;
            for (int i = 0; i < Projects?.Count; ++i)
            {
                if (Aborting)
                    return WorkItems;

                DataFetchEvent?.Invoke(AzureObject.WorkItem, Projects[i]?.Name, "in progress");
                var dt = DateTime.Now;
                GetWorkItemsByProject(dicWorkItems, Projects?[i]);
                DataFetchEvent?.Invoke(AzureObject.WorkItem, Projects?[i]?.Name, $"took {(DateTime.Now - dt).TotalSeconds} seconds");
            }

            return dicWorkItems;
        }

        private bool IsActiveWorkItem(AzureObjectBase x)
        {
            return (x.Status != "Verified" && x.Status != "Closed" && x.Status != "Removed");
        }

        private void GetWorkItemsByProject(Dictionary<int, AzureObjectBase> dicWorkItems, Project? p)
        {
            bool success = false;
            string sResponse = string.Empty;
            using (HttpClient client = new HttpClient())
            {
                SetClientAuth(client);

                string sTypes = string.Empty;
                if (m_APConfig.WorkItemTypes.Length > 0)
                {
                    foreach (var type in m_APConfig.WorkItemTypes)
                    {
                        sTypes += $"[System.WorkItemType] = '{type}' OR ";
                    }
                    sTypes = " AND (" + sTypes.Remove(sTypes.Length - 4, 3) + ")";
                }

                const int WIT_PER_CALL = 19999;
                int? skip = 0;
                //Get new items per project
                var projWITs = dicWorkItems.Values.Where(x => x.ProjectName == p?.Name).OrderByDescending(x => x.ID);
                if (projWITs.Count() > 0)
                {
                    skip = projWITs.First().ID;
                }

                if (skip > 0) //not first time
                {
                    //update open items per project
                    var openProjItems = projWITs.Where(x => IsActiveWorkItem(x)).Select(x => x.ID).ToList();

                    if (openProjItems.Count() > 0)
                    {
                        int count = 199;
                        for (int i = 0; i < openProjItems.Count(); i += count)
                        {
                            if (Aborting)
                                break;
                            List<int> lstIDRange = openProjItems.GetRange(i, Math.Min(openProjItems.Count - i, count));
                            if (lstIDRange?.Count > 0)
                                GetWorkItemsByIDs(dicWorkItems, lstIDRange, true);
                        }
                    }
                }

                while (true)
                {
                    if (Aborting)
                        break;

                    var jsonstr = "{\n \"query\":\""
                        + $"SELECT [System.Id] FROM WorkItems Where (([System.TeamProject] = '{p?.Name}')"
                        + $"{sTypes} AND ([System.Id]>{skip})) "
                        + $"ORDER BY [System.Id] ASC"
                        + "\" \n}";

                    HttpContent body = new StringContent(jsonstr, Encoding.UTF8, "application/json");
                    string uri = $"{AzureEndPoint}/{p?.Name}/_apis/wit/wiql?{API_VERSION}&$top={WIT_PER_CALL}";
                    var responseTask = client.PostAsync(uri, body);
                    sResponse = responseTask.Result.Content.ReadAsStringAsync().Result;

                    success = responseTask.Result.StatusCode == HttpStatusCode.OK;

                    if (success)
                    {
                        JsonNode? json = JsonNode.Parse(sResponse);
                        JsonArray? jsonWITs = json?["workItems"]?.AsArray();
                        
                        if (jsonWITs?.Count == 0)
                            break;

                        var lstWitID = new List<int>();
                        for (int i = 0; i < jsonWITs?.Count; ++i)
                        {
                            JsonNode? jsonWIT = jsonWITs[i];
                            int? nID = jsonWIT?["id"]?.GetValue<int>();

                            if (nID.HasValue)
                            {
                                lstWitID.Add(nID.Value);
                                if (skip < nID.Value)
                                    skip = nID.Value;
                            }
                        }
                        int count = 199;
                        for (int i = 0; i < lstWitID.Count; i += count)
                        {
                            if (Aborting)
                                break;

                            List<int> lstIDRange = lstWitID.GetRange(i, Math.Min(lstWitID.Count - i, count));
                            if (lstIDRange?.Count > 0)
                                GetWorkItemsByIDs(dicWorkItems, lstIDRange, false);
                        }
                    }
                    else
                    {
                        throw new Exception($"GetWorkItemsByProject => {sResponse}");
                    }
                }
            }
        }

        private void GetWorkItemsByIDs(Dictionary<int, AzureObjectBase> dicWorkItems, List<int> witIDs, bool bUpdate)
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
                        WIT wit = ParseWIT(jsonWIT);
                        if (bUpdate)
                        {
                            var item = dicWorkItems[wit.ID];
                            if (item.Status != wit.Status)
                                item.Status = wit.Status;
                        }
                        else
                        {
                            dicWorkItems.Add(wit.ID, wit);
                        }
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
                        
                        GetWorkItemsByIDs(dicWorkItems, witIDs, bUpdate);
                        break;
                    }
                }
            }
            else
            {
                throw new Exception($"GetWorkItemsByIDs => {sResponse}");
            }
        }

        private WIT ParseWIT(JsonNode? jsonWIT)
        {
            WIT wit = new WIT();

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

        #region PR
        private Dictionary<int, AzureObjectBase> GetPRs(PullRequestStatus status)
        {
            Dictionary<int, AzureObjectBase> dicPRs = PRs;
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

        private void GetPRsByProject(Dictionary<int, AzureObjectBase> dicPRs, Project? p, PullRequestStatus status)
        {
            const int PRS_PER_CALL = 101;
            int skip = 0;
            Dictionary<int, AzureObjectBase> activePRs = new Dictionary<int, AzureObjectBase>();
            if (status == PullRequestStatus.Active)
            {
                string sActive = PullRequestStatus.Active.ToString().ToLower();
                activePRs = PRs.Where(
                    pr => pr.Value.Status == sActive && pr.Value.ProjectName == p?.Name).
                    ToDictionary(p=>p.Key, p=>p.Value);
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

                            if (activePRs.Count>0)
                            {
                                if (activePRs.ContainsKey(pr.ID))
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
            foreach (var pr in activePRs)
            {
                string sResponse = string.Empty;
                string uri =
                    $"{AzureEndPoint}/{p?.Name}/_apis/git/pullrequests?{pr.Key}?{API_VERSION}";

                if (AzureGetRequest(uri, out sResponse))
                {
                    JsonNode? json = JsonNode.Parse(sResponse);

                    if (json != null)
                    {
                        activePRs[pr.Key] = ParsePR(json);
                    }
                }
                else
                {
                    throw new Exception($"GetPRsByProject => {sResponse}");
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

            JsonArray? jsonReviewers = jsonPR?["reviewers"]?.AsArray();
            for (int j = 0; j < jsonReviewers?.Count; ++j)
            {
                pr.Reviewers += jsonReviewers[j]?["displayName"]?.ToString() + "; ";
            }

            return pr;
        }

        #endregion

        #region Build

        private Dictionary<int, AzureObjectBase> GetBuilds(DateTime earliestCreateDate, int maxBuildsPerDefinition)
        {
            Dictionary<int, AzureObjectBase> dicBuilds = new Dictionary<int, AzureObjectBase>();
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
        private void GetBuildsByProject(Dictionary<int, AzureObjectBase> dicBuilds, Project? p, 
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
                    IEnumerable<JsonNode?> jsonBuilds = arrBuilds.ToArray()
                        .Where((jsonBuild) => QueueTimeEarlierThan(jsonBuild, earliestCreateDate));

                    foreach (JsonNode? jsonBuild in jsonBuilds)
                    {
                        if (jsonBuild != null)
                        {
                            Build build = ParseBuild(jsonBuild);
                            dicBuilds.Add(build.ID, build);
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
            DateTime dt;
            if (DateTime.TryParse(sDateTime, out dt))
                build.CreatedDate = dt;

            build.Definition = jsonBuild?["definition"]?["name"]?.ToString();

            return build;
        }

        #endregion
        private bool AzureGetRequest(string uri, out string response)
        {
            bool success = false;
            using (HttpClient client = new HttpClient())
            {
                SetClientAuth(client);

                var responseTask = client.GetAsync(uri);

                response = responseTask.Result.Content.ReadAsStringAsync().Result;
                success = responseTask.Result.StatusCode == HttpStatusCode.OK;
            }

            return success;
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
            string uri = string.Empty;
            
            if (aob is PR)
            {
                uri = $"{AzureEndPoint}/{aob?.ProjectName}/_apis/git/pullrequests/{aob?.ID}?{API_VERSION}";
            }
            else if (aob is WIT)
            {
                uri = $"{AzureEndPoint}/{aob?.ProjectName}/_apis/wit/workitems/{aob?.ID}?{API_VERSION}";
            }
            else if (aob is Build)
            {
                uri = $"{AzureEndPoint}/{aob?.ProjectName}/_apis/git/build/builds/{aob?.ID}?{API_VERSION}";
            }

            string sResponse = string.Empty;
            if (AzureGetRequest(uri, out sResponse))
            {
                AzureObjectBase? updatedAob = null;
                Dictionary<int, AzureObjectBase>? dic = null;
                JsonNode? jsonNode = JsonNode.Parse(sResponse);
                if (aob is PR)
                {
                    dic = PRs;
                    updatedAob = ParsePR(jsonNode);
                }
                else if (aob is WIT)
                {
                    dic = WorkItems;
                    updatedAob = ParseWIT(jsonNode);
                }
                else if (aob is Build)
                {
                    dic = Builds;
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
