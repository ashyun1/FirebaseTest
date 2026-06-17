using Firebase.Database;
using Newtonsoft.Json;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://final-firebase-userdata-default-rtdb.firebaseio.com";

    [Header("UI")]
    [SerializeField] Text HealthPackCountText;
    [SerializeField] Text BarrierCoreCountText;
    [SerializeField] Text FlameRuneCountText;
    [SerializeField] Text MessageText;

    string userKey;
    Dictionary<string, int> inventory = new Dictionary<string, int>();

    void Start()
    {
        database = FirebaseDatabase.GetInstance(databaseUrl);
        reference = database.RootReference;
        SetupDispatcher();

        userKey = PlayerPrefs.GetString("UserKey");

        if (string.IsNullOrEmpty(userKey))
        {
            SetMessage("로그인 정보가 없습니다.");
            return;
        }

        LoadInventory();
    }

    void SetupDispatcher()
    {
        if (!UnityMainThreadDispatcher.Exists())
        {
            GameObject dispatcherObject = new GameObject("UnityMainThreadDispatcher");
            dispatcherObject.AddComponent<UnityMainThreadDispatcher>();
        }

        dispatcher = UnityMainThreadDispatcher.Instance();
    }

    void LoadInventory()
    {
        reference
            .Child("UserInfo")
            .Child(userKey)
            .Child("Inventory")
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        SetMessage("인벤토리 불러오기 실패");
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                if (snapshot.Value == null)
                {
                    dispatcher.Enqueue(() =>
                    {
                        SetMessage("인벤토리 데이터가 없습니다.");
                    });
                    return;
                }

                string inventoryJson = snapshot.Value.ToString();
                inventory = ParseInventory(inventoryJson);

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    SetMessage("인벤토리 불러오기 완료");
                });
            });
    }

    void RefreshUI()
    {
        SetText(HealthPackCountText, "HealthPack : " + GetItemCount("HealthPack"));
        SetText(BarrierCoreCountText, "BarrierCore : " + GetItemCount("BarrierCore"));
        SetText(FlameRuneCountText, "FlameRune : " + GetItemCount("FlameRune"));
    }

    int GetItemCount(string itemName)
    {
        if (inventory.ContainsKey(itemName))
        {
            return inventory[itemName];
        }

        return 0;
    }

    public void OnClickUseHealthPack()
    {
        UseItem("HealthPack");
    }

    public void OnClickUseBarrierCore()
    {
        UseItem("BarrierCore");
    }

    public void OnClickUseFlameRune()
    {
        UseItem("FlameRune");
    }

    void UseItem(string itemName)
    {
        if (!inventory.ContainsKey(itemName) || inventory[itemName] <= 0)
        {
            SetMessage(itemName + " 개수가 부족합니다.");
            return;
        }

        inventory[itemName]--;
        SaveInventory(itemName);
    }

    void SaveInventory(string usedItemName)
    {
        string inventoryJson = JsonConvert.SerializeObject(inventory);

        reference
            .Child("UserInfo")
            .Child(userKey)
            .Child("Inventory")
            .SetValueAsync(inventoryJson)
            .ContinueWith(task =>
            {
                dispatcher.Enqueue(() =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        SetMessage("인벤토리 저장 실패");
                        return;
                    }

                    RefreshUI();
                    SetMessage(GetUseMessage(usedItemName));
                });
            });
    }

    Dictionary<string, int> ParseInventory(string inventoryJson)
    {
        Dictionary<string, int> result = new Dictionary<string, int>();
        result["HealthPack"] = 0;
        result["BarrierCore"] = 0;
        result["FlameRune"] = 0;

        if (string.IsNullOrEmpty(inventoryJson))
        {
            return result;
        }

        Dictionary<string, int> loadedInventory = JsonConvert.DeserializeObject<Dictionary<string, int>>(inventoryJson);

        if (loadedInventory == null)
        {
            return result;
        }

        if (loadedInventory.ContainsKey("HealthPack"))
        {
            result["HealthPack"] = loadedInventory["HealthPack"];
        }

        if (loadedInventory.ContainsKey("BarrierCore"))
        {
            result["BarrierCore"] = loadedInventory["BarrierCore"];
        }

        if (loadedInventory.ContainsKey("FlameRune"))
        {
            result["FlameRune"] = loadedInventory["FlameRune"];
        }

        return result;
    }

    string GetUseMessage(string itemName)
    {
        if (itemName == "HealthPack")
        {
            return "HealthPack 사용 완료 - 체력을 회복했습니다.";
        }

        if (itemName == "BarrierCore")
        {
            return "BarrierCore 사용 완료 - 보호막을 활성화했습니다.";
        }

        if (itemName == "FlameRune")
        {
            return "FlameRune 사용 완료 - 화염 룬을 발동했습니다.";
        }

        return itemName + " 사용 완료";
    }

    void SetText(Text targetText, string message)
    {
        if (targetText != null)
        {
            targetText.text = message;
        }
    }

    void SetMessage(string message)
    {
        SetText(MessageText, message);
        Debug.Log(message);
    }
}
