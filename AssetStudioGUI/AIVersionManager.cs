using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using AssetStudio;
using System.Threading.Tasks;

namespace AssetStudioGUI
{
    public class VersionIndex
    {
        public string MappedPath;
        public string RawPath;
        public string Version;
        public double Coverage;
    }

    public class AIVersionManager
    {
        public bool Online = true;
        public List<VersionIndex> Versions;
        public readonly string BaseAIFolder = Path.Combine(Application.StartupPath, "AI");
        public readonly string VersionsPath;
        public const string BaseUrl = "https://raw.githubusercontent.com/radioegor146/gi-asset-indexes/master/";
        public const string CommitsUrl = "https://api.github.com/repos/radioegor146/gi-asset-indexes/commits?path=";
        public AIVersionManager()
        {
            VersionsPath = Path.Combine(BaseAIFolder, "versions.json");
            var versions = DownloadString(BaseUrl + "version-index.json");
            if (string.IsNullOrEmpty(versions))
            {
                Logger.Warning("Could not load AI versions !!");
                Online = false;
                VersionsPath = null;
                return;
            }
            Versions = JsonConvert.DeserializeObject<List<VersionIndex>>(versions);
        }

        public static Uri CreateUri(string source, out Uri result) => Uri.TryCreate(source, UriKind.Absolute, out result) && result.Scheme == Uri.UriSchemeHttps ? result : null;

        public static string DownloadString(string url)
        {
            string json = "";
            using (var webClient = new System.Net.WebClient())
            {
                if (CreateUri(url, out var uri) != null)
                {
                    try
                    {
                        json = webClient.DownloadString(uri);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Failed to fetch version_index.json, {ex.Message}");
                    }
                }
            }
            return json;
        }

        public static string DownloadResponse(string url)
        {
            string json = "";
            if (CreateUri(url, out var uri) != null)
            {
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
                var webRequest = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
                webRequest.UserAgent = "Mozilla/5.0 (compatible; MSIE 6.0; Windows 98; Trident/5.1)";
                webRequest.Accept = "true";
                try
                {
                    System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)webRequest.GetResponse();
                    using (var responseStream = response.GetResponseStream())
                    {
                        using (var reader = new StreamReader(responseStream))
                        {
                            json = reader.ReadToEnd();
                        }
                    }
                }
                catch (System.Net.WebException ex)
                {
                    if (ex.Response != null)
                    {
                        var response = ex.Response;
                        var dataStream = response.GetResponseStream();
                        var reader = new StreamReader(dataStream);
                        var details = reader.ReadToEnd();
                        Logger.Error("Failed to fetch version_index.json\n" + details);
                    }
                }
            }
            return json;
        }

        public async Task<string> DownloadAI(string version)
        {
            var versionIndex = Versions.FirstOrDefault(x => x.Version == version);
            string json = "";

            Logger.Info("Downloading....");
            using (var webClient = new System.Net.WebClient())
            {
                if (CreateUri(BaseUrl + versionIndex.MappedPath, out var uri) != null)
                {
                    Progress.Reset();
                    webClient.DownloadProgressChanged += (sender, evt) =>
                    {
                        double bytesIn = double.Parse(evt.BytesReceived.ToString());
                        double totalBytes = double.Parse(evt.TotalBytesToReceive.ToString());
                        double percentage = bytesIn / totalBytes * 100;
                        Progress.Report(int.Parse(Math.Truncate(percentage).ToString()), 100);
                    };
                    webClient.DownloadStringCompleted += (sender, evt) =>
                    {
                        if (evt.Cancelled)
                            Logger.Info("Download Canceled!");
                        else if (evt.Error != null)
                            Logger.Error("Download Error!");
                        else
                            Logger.Info("Download Finished!");
                    };
                    try
                    {
                        json = await webClient.DownloadStringTaskAsync(uri);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to fetch {Path.GetFileName(versionIndex.MappedPath)}", ex);
                    }
                    if (!StoreCommit(version))
                    {
                        throw new Exception("Failed to store AIVersion");
                    }
                }
            }
            return json;
        }

        public bool NeedDownload(string version)
        {
            var path = GetAIPath(version);
            if (!File.Exists(path)) return true;
            var latestCommit = GetLatestCommit(version);
            if (string.IsNullOrEmpty(latestCommit)) return true;
            var dict = LoadVersions();
            if (dict.TryGetValue(version, out var commit))
            {
                if (commit == latestCommit) return false;
            }
            return true;
        }

        public bool StoreCommit(string version)
        {
            var latestCommit = GetLatestCommit(version);
            if (string.IsNullOrEmpty(latestCommit)) return false;
            var dict = LoadVersions();
            if (dict.TryGetValue(version, out var commit))
            {
                if (commit != latestCommit)
                    dict[version] = latestCommit;
            }
            else dict.Add(version, latestCommit);
            StoreVersions(dict);
            return true;
        }

        public Dictionary<string, string> CreateVersions()
        {
            var dict = new Dictionary<string, string>();
            var dir = Path.GetDirectoryName(VersionsPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            using (var stream = File.Create(VersionsPath))
            {
                var serializer = new JsonSerializer();

                using (StreamWriter writer = new StreamWriter(stream))
                using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
                {
                    JsonSerializer ser = new JsonSerializer();
                    ser.Serialize(jsonWriter, dict);
                    jsonWriter.Flush();
                }
            }
            return dict;
        }

        public Dictionary<string, string> LoadVersions()
        {
            if (!File.Exists(VersionsPath))
            {
                return CreateVersions();
            }

            var file = File.ReadAllText(VersionsPath);
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(file);
        }

        public void StoreVersions(Dictionary<string, string> dict)
        {
            var json = JsonConvert.SerializeObject(dict, Formatting.Indented);
            File.WriteAllText(VersionsPath, json);
        }

        public string GetAIPath(string version)
        {
            if (!Online) return "";
            var versionIndex = Versions.FirstOrDefault(x => x.Version == version);
            return Path.Combine(BaseAIFolder, Path.GetFileName(versionIndex.MappedPath));
        }

        public string GetLatestCommit(string version)
        {
            var versionIndex = Versions.FirstOrDefault(x => x.Version == version);
            string commit = "";
            try
            {
                string json = DownloadResponse(CommitsUrl + versionIndex.MappedPath);
                JArray data = JsonConvert.DeserializeObject<JArray>(json);
                commit = data[0]["sha"].ToString();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to fetch latest commit", ex);
            }
            return commit;
        }

        public string[] GetVersions()
        {
            if (!Online) return new string[0];
            return Versions.Select(x => x.Version).ToArray();
        }
    }
}
