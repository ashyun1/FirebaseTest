using Firebase.Database;
using Newtonsoft.Json;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://final-firebase-userdata-default-rtdb.firebaseio.com";

    [Header("UI")]
    [SerializeField] Text CoinText;
    [SerializeField] Text MessageText;
    [SerializeField] Text HealthPackPriceText;
    [SerializeField] Text BarrierCorePriceText;
    [SerializeField] Text FlameRunePriceText;

    string userKey;
    int currentCoin;
    Dictionary<string, int> inventory = new Dictionary<string, int>();

    void Start()
    {
        database = FirebaseDatabase.GetInstance(databaseUrl);
        reference = database.RootReference;
        SetupDispatcher();

        SetItemPriceText();

        userKey = PlayerPrefs.GetString("UserKey");

        if (string.IsNullOrEmpty(userKey))
        {
            SetMessage("로그인 정보가 없습니다.");
            return;
        }

        LoadUserData();
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

    void LoadUserData()
    {
        reference
            .Child("UserInfo")
            .Child(userKey)
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        SetMessage("유저 정보 불러오기 실패");
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                if (!snapshot.Exists)
                {
                    dispatcher.Enqueue(() =>
                    {
                        SetMessage("유저 정보가 없습니다. 다시 로그인하세요.");
                    });
                    return;
                }

                currentCoin = ReadInt(snapshot.Child("Coin"), 0);

                string inventoryJson = snapshot.Child("Inventory").Value == null ? "" : snapshot.Child("Inventory").Value.ToString();
                inventory = ParseInventory(inventoryJson);

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    SetMessage("유저 정보 불러오기 완료");
                });
            });
    }

    void SetItemPriceText()
    {
        SetText(HealthPackPriceText, "HealthPack : 120 Coin");
        SetText(BarrierCorePriceText, "BarrierCore : 240 Coin");
        SetText(FlameRunePriceText, "FlameRune : 360 Coin");
    }

    void RefreshUI()
    {
        SetText(CoinText, "Coin : " + currentCoin);
        SetItemPriceText();
    }

    public void OnClickBuyHealthPack()
    {
        BuyItem("HealthPack", 120);
    }

    public void OnClickBuyBarrierCore()
    {
        BuyItem("BarrierCore", 240);
    }

    public void OnClickBuyFlameRune()
    {
        BuyItem("FlameRune", 360);
    }

    void BuyItem(string itemName, int price)
    {
        if (string.IsNullOrEmpty(userKey))
        {
            SetMessage("로그인 정보가 없습니다.");
            return;
        }

        if (currentCoin < price)
        {
            SetMessage("코인이 부족합니다.");
            return;
        }

        currentCoin -= price;

        if (inventory.ContainsKey(itemName))
        {
            inventory[itemName]++;
        }
        else
        {
            inventory[itemName] = 1;
        }

        SaveUserData(itemName);
    }

    void SaveUserData(string boughtItemName)
    {
        string inventoryJson = JsonConvert.SerializeObject(inventory);

        Dictionary<string, object> updateData = new Dictionary<string, object>();
        updateData["Coin"] = currentCoin;
        updateData["Inventory"] = inventoryJson;

        reference
            .Child("UserInfo")
            .Child(userKey)
            .UpdateChildrenAsync(updateData)
            .ContinueWith(task =>
            {
                dispatcher.Enqueue(() =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        SetMessage("구매 저장 실패");
                        return;
                    }

                    RefreshUI();
                    SetMessage(boughtItemName + " 구매 완료");
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

    int ReadInt(DataSnapshot snapshot, int fallbackValue)
    {
        if (snapshot.Value == null)
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
