using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;

namespace Puniemu.Src.Server.Infrastructure
{
    // Downloads the game data from the official servers, the way the game itself does:
    //  * master tables: init.nhn lists them (ywp_mst_version_master), getMaster.nhn returns them
    //  * assets: every file listed in ywp_mst_version_resource from resource.yw-p.com
    //
    //   Puniemu --fetch-game-data [--version 4.177.0] [--resources Resources] [--assets dataDownload]
    //
    // Master tables overwrite the ones in --resources (Puniemu's own files there - stage_data,
    // gacha_pool, mission_cfg... - are not touched). Assets are only downloaded when --assets is
    // given; files that are already there with the right size are skipped, so a second run only
    // fetches what is new. The official server only answers the current client version.
    public static class GameDataFetcher
    {
        private const string GAMESERVER = "https://gameserver.yw-p.com/";
        private const string RESOURCE = "https://resource.yw-p.com/eal";
        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var h = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None }) { Timeout = TimeSpan.FromMinutes(3) };
            h.DefaultRequestHeaders.UserAgent.ParseAdd("Dalvik/2.1.0 (Linux; U; Android 9; SM-G965N Build/QP1A.190711.020)");
            return h;
        }

        public static async Task<int> RunAsync(string[] args)
        {
            string Arg(string name, string def)
            {
                var i = Array.IndexOf(args, name);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : def;
            }
            var version = Arg("--version", Environment.GetEnvironmentVariable("GameVersion") ?? "4.177.0");
            var resources = Arg("--resources", Environment.GetEnvironmentVariable("PUNIEMU_RESOURCES_PATH") ?? "Resources");
            var assets = Arg("--assets", "");
            try
            {
                var n = await FetchMasterAsync(version, resources);
                Console.WriteLine($"{n} master tables saved to {resources}");
                if (assets.Length > 0) await FetchAssetsAsync(resources, assets);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Game data download failed: " + ex.Message);
                return 1;
            }
        }

        private static async Task<JObject> CallAsync(string endpoint, JObject body)
        {
            var content = new StringContent(NHNCrypt.Logic.NHNCrypt.EncryptRequest(body.ToString(Formatting.None)));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            using var res = await Http.PostAsync(GAMESERVER + endpoint, content);
            var text = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode || text.Length == 0) throw new Exception($"{endpoint}: HTTP {(int)res.StatusCode}");
            return JObject.Parse(NHNCrypt.Logic.NHNCrypt.DecryptResponse(text));
        }

        private static string TableText(JToken t) => t.Type == JTokenType.String ? t.ToString() : t.ToString(Formatting.None);

        private static string TableData(JToken t)
        {
            if (t is JObject o) return o.Value<string>("tableData") ?? "";
            var s = t.ToString();
            if (s.TrimStart().StartsWith("{")) try { return JObject.Parse(s).Value<string>("tableData") ?? ""; } catch { }
            return s;
        }

        public static async Task<int> FetchMasterAsync(string version, string outDir)
        {
            Directory.CreateDirectory(outDir);
            var baseReq = new JObject
            {
                ["appVer"] = version, ["deviceId"] = "d-822a61c56b60d03dd373aab9826210e1", ["level5UserId"] = "0",
                ["osType"] = 2, ["userId"] = "0", ["ywpToken"] = "0"
            };
            Console.WriteLine($"Asking gameserver.yw-p.com for the master tables of {version}...");
            var init = await CallAsync("init.nhn", baseReq);
            var vm = init["ywp_mst_version_master"];
            if (vm == null)
                throw new Exception("the official server did not list its tables" +
                    (init.Value<string>("dialogMsg") is string m && m.Length > 0 ? $" (it said: {m.Replace('\n', ' ')})" : "") +
                    " - is GameVersion the current client version?");
            var names = TableData(vm).Split('*', StringSplitOptions.RemoveEmptyEntries).Select(r => r.Split('|')[0]).Where(n => n.Length > 0).Distinct().ToList();
            Console.WriteLine($"{names.Count} tables listed, mstVersionMaster {init["mstVersionMaster"]}");

            void Save(string name, string text) => File.WriteAllText(Path.Combine(outDir, name + ".txt"), text);
            Save("ywp_mst_version_master", TableText(vm));
            Save("mstVersionMaster", init["mstVersionMaster"]?.ToString() ?? "0");
            foreach (var k in new[] { "noticePageList", "ymoneyShopSaleList" })
                if (init[k] != null) Save(k, init[k]!.ToString(Formatting.None));

            int got = 0;
            for (int i = 0; i < names.Count; i += 20)
            {
                var batch = names.Skip(i).Take(20).ToList();
                JObject j;
                try { j = await CallAsync("getMaster.nhn", new JObject(baseReq) { ["tableNames"] = string.Join("|", batch) }); }
                catch (Exception ex) { Console.WriteLine($"  batch from {batch[0]} failed: {ex.Message}"); continue; }
                foreach (var n in batch)
                    if (j[n] is JToken t) { Save(n, TableText(t)); got++; }
                await Task.Delay(500); // gently
            }
            return got;
        }

        public static async Task FetchAssetsAsync(string masterDir, string outDir)
        {
            var path = Path.Combine(masterDir, "ywp_mst_version_resource.txt");
            if (!File.Exists(path)) throw new Exception("ywp_mst_version_resource is missing - download the master tables first");
            var rows = TableData(JToken.FromObject(File.ReadAllText(path))).Split('*', StringSplitOptions.RemoveEmptyEntries).Select(r => r.Split('|')).Where(r => r.Length >= 6).ToList();
            var jobs = new List<(string rel, string dest)>();
            foreach (var r in rows)
            {
                var rel = $"{r[5]}/{r[3]}/{r[1]}";
                var dest = Path.Combine(outDir, r[5], r[3], r[1]);
                long size = r.Length > 6 && long.TryParse(r[6], out var sz) ? sz : -1;
                if (File.Exists(dest) && (size < 0 || new FileInfo(dest).Length == size)) continue;
                jobs.Add((rel, dest));
            }
            Console.WriteLine($"{rows.Count} asset files listed, {jobs.Count} to download");
            int done = 0, failed = 0;
            await Parallel.ForEachAsync(jobs, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (job, ct) =>
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        using var res = await Http.GetAsync($"{RESOURCE}/{job.rel}", ct);
                        if (res.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) { Interlocked.Increment(ref failed); break; }
                        res.EnsureSuccessStatusCode();
                        Directory.CreateDirectory(Path.GetDirectoryName(job.dest)!);
                        var bytes = await res.Content.ReadAsByteArrayAsync(ct);
                        await File.WriteAllBytesAsync(job.dest + ".part", bytes, ct);
                        File.Move(job.dest + ".part", job.dest, true);
                        break;
                    }
                    catch when (attempt < 2) { await Task.Delay(2000 * (attempt + 1), ct); }
                    catch { Interlocked.Increment(ref failed); }
                }
                var d = Interlocked.Increment(ref done);
                if (d % 2000 == 0) Console.WriteLine($"  {d} / {jobs.Count}");
            });
            Console.WriteLine($"assets: {done - failed} downloaded, {failed} not available (old files the CDN deleted)");
        }
    }
}
