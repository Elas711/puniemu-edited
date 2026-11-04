using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace Puniemu.Src.DataManager.Logic;

public class GameDataManager
{
    public ConcurrentDictionary<string, string> GamedataCache = new();
    public GameDataManager()
    {
        CacheGamedataFromResources();
        CacheGamedataFromDisk();
    }

    // Loads Resources/*.txt (and *.json tables that have no .txt twin) from disk, on top of the
    // embedded copy. This way the game data can be updated without rebuilding the server, and a
    // published build works with a Resources folder next to it.
    private void CacheGamedataFromDisk()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("PUNIEMU_RESOURCES_PATH"),
            Path.Combine(Directory.GetCurrentDirectory(), "Resources"),
            Path.Combine(AppContext.BaseDirectory, "Resources")
        };
        var dir = candidates.FirstOrDefault(d => !string.IsNullOrEmpty(d) && Directory.Exists(d));
        if (dir == null) return;
        int loaded = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*.txt").Concat(Directory.EnumerateFiles(dir, "*.json")))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(dir, name + ".txt"))) continue;
            try
            {
                GamedataCache[name] = File.ReadAllText(file, Encoding.UTF8).TrimStart('\uFEFF');
                loaded++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not read {file}: {ex.Message}");
            }
        }
        if (loaded > 0) Console.WriteLine($"Loaded {loaded} game data files from {dir}");
    }
    public string GetTableStringFromJson(string tableId)
    {
        var raw = GamedataCache[tableId];
        if (raw.TrimStart().StartsWith("{") == false) return raw;
        var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(raw);
        return (string)json["tableData"];
    }
    private void CacheGamedataFromResources()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string rootNamespace = assembly.GetName().Name!;
        string[] resourceNames = assembly.GetManifestResourceNames();

        foreach (var resourceName in resourceNames)
        {
            using (Stream stream = assembly.GetManifestResourceStream(resourceName)!)
            {
                if (stream != null)
                {
                    using (StreamReader reader = new StreamReader(stream, encoding: Encoding.UTF8))
                    {
                        string content = reader.ReadToEnd();
                        var filteredName = resourceName
                            .Replace($"{rootNamespace}.Resources.", "")
                            .Replace(".txt", "");
                        GamedataCache[filteredName] = content;
                    }
                }
            }
        }
    }
}
