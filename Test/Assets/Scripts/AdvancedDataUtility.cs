using Firebase.Database;
using Newtonsoft.Json;
using System.Collections.Generic;

public static class AdvancedDataUtility
{
    public static Dictionary<string, bool> CreateDefaultUnitList()
    {
        Dictionary<string, bool> result = new Dictionary<string, bool>();
        result["Unit1"] = true;

        for (int i = 2; i <= 4; i++)
        {
            result["Unit" + i] = false;
        }

        return result;
    }

    public static Dictionary<string, int> CreateDefaultInventory()
    {
        Dictionary<string, int> result = new Dictionary<string, int>();
        result["HealthPack"] = 0;
        result["BarrierCore"] = 0;
        result["FlameRune"] = 0;
        return result;
    }

    public static Dictionary<string, bool> ParseUnitList(string unitJson)
    {
        Dictionary<string, bool> result = CreateDefaultUnitList();

        if (string.IsNullOrEmpty(unitJson))
        {
            return result;
        }

        Dictionary<string, bool> loadedUnits;
        try
        {
            loadedUnits = JsonConvert.DeserializeObject<Dictionary<string, bool>>(unitJson);
        }
        catch (JsonException)
        {
            return result;
        }

        if (loadedUnits == null)
        {
            return result;
        }

        foreach (KeyValuePair<string, bool> pair in loadedUnits)
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }

    public static Dictionary<string, int> ParseInventory(string inventoryJson)
    {
        Dictionary<string, int> result = CreateDefaultInventory();

        if (string.IsNullOrEmpty(inventoryJson))
        {
            return result;
        }

        Dictionary<string, int> loadedInventory;
        try
        {
            loadedInventory = JsonConvert.DeserializeObject<Dictionary<string, int>>(inventoryJson);
        }
        catch (JsonException)
        {
            return result;
        }

        if (loadedInventory == null)
        {
            return result;
        }

        foreach (KeyValuePair<string, int> pair in loadedInventory)
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }

    public static int GetItemCount(Dictionary<string, int> inventory, string itemName)
    {
        if (inventory.ContainsKey(itemName))
        {
            return inventory[itemName];
        }

        return 0;
    }

    public static int ReadInt(DataSnapshot snapshot, int fallbackValue)
    {
        if (snapshot == null || snapshot.Value == null)
        {
            return fallbackValue;
        }

        int value;
        if (int.TryParse(snapshot.Value.ToString(), out value))
        {
            return value;
        }

        return fallbackValue;
    }

    public static bool ReadBool(DataSnapshot snapshot, bool fallbackValue)
    {
        if (snapshot == null || snapshot.Value == null)
        {
            return fallbackValue;
        }

        bool value;
        if (bool.TryParse(snapshot.Value.ToString(), out value))
        {
            return value;
        }

        return fallbackValue;
    }

    public static string ReadString(DataSnapshot snapshot)
    {
        if (snapshot == null || snapshot.Value == null)
        {
            return "";
        }

        return snapshot.Value.ToString();
    }
}
