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
    [SerializeField] string databaseUrl = "https://myproject-76240-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("UI")]
    [SerializeField] Text CoinText;
    [SerializeField] Text MessageText;

    string userKey;
    int currentCoin;
    Dictionary<string, int> inventory = new Dictionary<string, int>();

    void Start()
    {
        database = FirebaseDatabase.GetInstance(databaseUrl);
        reference = database.RootReference;
        dispatcher = UnityMainThreadDispatcher.Instance();

        userKey = PlayerPrefs.GetString("UserKey");

        if (string.IsNullOrEmpty(userKey))
        {
            MessageText.text = "로그인 정보가 없습니다.";
            return;
        }

        LoadUserData();
    }

    void LoadUserData()
    {
        reference
            .Child("UserInfo")
            .Child(userKey)
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "유저 정보 불러오기 실패";
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                currentCoin = int.Parse(snapshot.Child("Coin").Value.ToString());

                string inventoryJson = snapshot.Child("Inventory").Value.ToString();
                inventory = JsonConvert.DeserializeObject<Dictionary<string, int>>(inventoryJson);

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MessageText.text = "유저 정보 불러오기 완료";
                });
            });
    }

    void RefreshUI()
    {
        CoinText.text = "Coin : " + currentCoin;
    }

    public void OnClickBuyPotion()
    {
        BuyItem("Potion", 100);
    }

    public void OnClickBuyBomb()
    {
        BuyItem("Bomb", 200);
    }

    public void OnClickBuyTicket()
    {
        BuyItem("Ticket", 300);
    }

    void BuyItem(string itemName, int price)
    {
        if (currentCoin < price)
        {
            MessageText.text = "코인이 부족합니다.";
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
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "구매 저장 실패";
                    });
                    return;
                }

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MessageText.text = boughtItemName + " 구매 완료";
                });
            });
    }
}
