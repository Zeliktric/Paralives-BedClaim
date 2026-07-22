using System.Collections.Generic;
using System.IO;
using BepInEx;

using Newtonsoft.Json;

namespace Zeliktric.BedClaim;

public class BedData
{
    public int bedID = -1;
    public List<ulong> slotIDs;
}

public static class DataManager
{
    private static string OldDataPath => Path.Combine(Paths.BepInExRootPath, "data", "Zeliktric", MyPluginInfo.PLUGIN_NAME, "Data.json");
    public static Dictionary<ulong, BedData> Beds { get; set; } = new Dictionary<ulong, BedData>();

    private static void DeleteOldData()
    {
        if (File.Exists(OldDataPath))
        {
            Beds = JsonConvert.DeserializeObject<Dictionary<ulong, BedData>>(File.ReadAllText(OldDataPath));
            File.Delete(OldDataPath);
        }
    }

    public static void Save(ulong charID, BedData bedData)
    {
        string dataPath = Path.Combine(Paths.BepInExRootPath, "data", "Zeliktric", MyPluginInfo.PLUGIN_NAME, "Game Data", $"{Start.SavedGameName}.json");
        DeleteOldData();

        if (!File.Exists(dataPath)) Directory.CreateDirectory(Path.GetDirectoryName(dataPath));
        
        Beds[charID] = bedData;
        
        File.WriteAllText(dataPath, JsonConvert.SerializeObject(Beds, Formatting.Indented));
    }

    public static void Delete(ulong charID)
    {
        string dataPath = Path.Combine(Paths.BepInExRootPath, "data", "Zeliktric", MyPluginInfo.PLUGIN_NAME, "Game Data", $"{Start.SavedGameName}.json");
        DeleteOldData();

        Beds.Remove(charID);
        File.WriteAllText(dataPath, JsonConvert.SerializeObject(Beds, Formatting.Indented));
    }

    public static BedData Load(ulong charID)
    {
        string dataPath = Path.Combine(Paths.BepInExRootPath, "data", "Zeliktric", MyPluginInfo.PLUGIN_NAME, "Game Data", $"{Start.SavedGameName}.json");
        DeleteOldData();

        if (!File.Exists(dataPath)) return new BedData();
        Beds = JsonConvert.DeserializeObject<Dictionary<ulong, BedData>>(File.ReadAllText(dataPath));

        if (Beds == null) return new BedData();

        if (Beds.TryGetValue(charID, out BedData bed)) return bed;
        return new BedData();
    }
}