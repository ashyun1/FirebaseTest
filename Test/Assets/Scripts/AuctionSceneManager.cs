using Firebase.Database;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AuctionSceneManager : MonoBehaviour
{
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;
    AuctionFeature auctionFeature;
    AdvancedUserState currentUser = new AdvancedUserState();
    List<AuctionItemData> currentItems = new List<AuctionItemData>();

    readonly Dictionary<string, int> sellPrices = new Dictionary<string, int>
    {
        { "HealthPack", 80 },
        { "BarrierCore", 160 },
        { "FlameRune", 260 }
    };

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://final-firebase-userdata-default-rtdb.firebaseio.com";

    [Header("UI")]
    [SerializeField] Text statusText;
    [SerializeField] Text inventoryText;
    [SerializeField] Text auctionStatusText;
    [SerializeField] Text messageText;
    [SerializeField] Button auctionItemButton1;
    [SerializeField] Button auctionItemButton2;
    [SerializeField] Button auctionItemButton3;
    [SerializeField] Button auctionItemButton4;
    [SerializeField] Button auctionItemButton5;
    [SerializeField] Text auctionItemText1;
    [SerializeField] Text auctionItemText2;
    [SerializeField] Text auctionItemText3;
    [SerializeField] Text auctionItemText4;
    [SerializeField] Text auctionItemText5;

    void Start()
    {
        reference = FirebaseDatabaseProvider.GetRootReference(databaseUrl);
        auctionFeature = new AuctionFeature(reference);

        SetupDispatcher();
        LoadUserFromPlayerPrefs();
        LoadCurrentUserData();
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

    void LoadUserFromPlayerPrefs()
    {
        currentUser.UserKey = PlayerPrefs.GetString("UserKey");
        currentUser.NickName = PlayerPrefs.GetString("UserNickName");
        RefreshStatus();
    }

    void LoadCurrentUserData()
    {
        if (string.IsNullOrEmpty(currentUser.UserKey))
        {
            SetMessage("로그인 정보가 없습니다. 다시 로그인하세요.");
            return;
        }

        reference.Child("UserInfo").Child(currentUser.UserKey).GetValueAsync().ContinueWith(task =>
        {
            if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
            {
                RunOnMainThread(() => SetMessage("유저 데이터 불러오기 실패"));
                return;
            }

            AdvancedUserState loadedState = CreateStateFromSnapshot(currentUser.UserKey, task.Result);

            RunOnMainThread(() =>
            {
                currentUser = loadedState;
                RefreshStatus();
                RefreshInventoryText();
                SetMessage("거래소 데이터 불러오기 완료");
                LoadAuctionItems();
            });
        });
    }

    AdvancedUserState CreateStateFromSnapshot(string userKey, DataSnapshot snapshot)
    {
        AdvancedUserState state = new AdvancedUserState();
        state.UserKey = userKey;
        state.NickName = AdvancedDataUtility.ReadString(snapshot.Child("NickName"));
        state.Coin = AdvancedDataUtility.ReadInt(snapshot.Child("Coin"), 0);
        state.Score = AdvancedDataUtility.ReadInt(snapshot.Child("Score"), 0);
        state.UnitList = AdvancedDataUtility.ParseUnitList(AdvancedDataUtility.ReadString(snapshot.Child("UnitList")));
        state.Inventory = AdvancedDataUtility.ParseInventory(AdvancedDataUtility.ReadString(snapshot.Child("Inventory")));
        return state;
    }

    public void OnClickSellHealthPack()
    {
        RegisterItem("HealthPack");
    }

    public void OnClickSellBarrierCore()
    {
        RegisterItem("BarrierCore");
    }

    public void OnClickSellFlameRune()
    {
        RegisterItem("FlameRune");
    }

    public void OnClickRefreshAuction()
    {
        LoadAuctionItems();
    }

    public void OnClickBuyAuctionItem1()
    {
        BuyAuctionItemByIndex(0);
    }

    public void OnClickBuyAuctionItem2()
    {
        BuyAuctionItemByIndex(1);
    }

    public void OnClickBuyAuctionItem3()
    {
        BuyAuctionItemByIndex(2);
    }

    public void OnClickBuyAuctionItem4()
    {
        BuyAuctionItemByIndex(3);
    }

    public void OnClickBuyAuctionItem5()
    {
        BuyAuctionItemByIndex(4);
    }

    public void OnClickBackToShop()
    {
        SceneManager.LoadScene("ShopScene");
    }

    void RegisterItem(string itemName)
    {
        int price = sellPrices[itemName];
        auctionFeature.RegisterItem(currentUser.UserKey, currentUser.NickName, itemName, price, (state, message) =>
        {
            RunOnMainThread(() =>
            {
                currentUser = state;
                RefreshStatus();
                RefreshInventoryText();
                SetMessage(message);
                LoadAuctionItems();
            });
        }, message => RunOnMainThread(() => SetMessage(message)));
    }

    void LoadAuctionItems()
    {
        SetAuctionStatus("판매 목록 불러오는 중...");
        auctionFeature.LoadOpenItems(items => RunOnMainThread(() => DrawAuctionItems(items)), message => RunOnMainThread(() => SetAuctionStatus(message)));
    }

    void DrawAuctionItems(List<AuctionItemData> items)
    {
        currentItems = items;
        RefreshAuctionButtons();

        if (items.Count == 0)
        {
            SetAuctionStatus("판매 중인 아이템이 없습니다.");
            return;
        }

        SetAuctionStatus("판매 중인 아이템: " + items.Count + "개");
    }

    void BuyAuctionItemByIndex(int index)
    {
        if (index < 0 || index >= currentItems.Count)
        {
            SetMessage("선택한 판매 아이템이 없습니다.");
            return;
        }

        AuctionItemData item = currentItems[index];
        auctionFeature.BuyItem(currentUser.UserKey, item, (state, message) =>
        {
            RunOnMainThread(() =>
            {
                currentUser = state;
                RefreshStatus();
                RefreshInventoryText();
                SetMessage(message);
                LoadAuctionItems();
            });
        }, message => RunOnMainThread(() => SetMessage(message)));
    }

    void RefreshInventoryText()
    {
        SetText(inventoryText,
            "HealthPack : " + AdvancedDataUtility.GetItemCount(currentUser.Inventory, "HealthPack") + "\n" +
            "BarrierCore : " + AdvancedDataUtility.GetItemCount(currentUser.Inventory, "BarrierCore") + "\n" +
            "FlameRune : " + AdvancedDataUtility.GetItemCount(currentUser.Inventory, "FlameRune"));
    }

    void RefreshAuctionButtons()
    {
        RefreshAuctionButton(auctionItemButton1, auctionItemText1, 0);
        RefreshAuctionButton(auctionItemButton2, auctionItemText2, 1);
        RefreshAuctionButton(auctionItemButton3, auctionItemText3, 2);
        RefreshAuctionButton(auctionItemButton4, auctionItemText4, 3);
        RefreshAuctionButton(auctionItemButton5, auctionItemText5, 4);
    }

    void RefreshAuctionButton(Button button, Text label, int index)
    {
        if (button == null)
        {
            return;
        }

        bool hasItem = index < currentItems.Count;
        button.gameObject.SetActive(hasItem);

        if (!hasItem || label == null)
        {
            return;
        }

        AuctionItemData item = currentItems[index];
        string sellerText = string.IsNullOrEmpty(item.SellerNickName) ? item.SellerKey : item.SellerNickName;
        bool isMine = item.SellerKey == currentUser.UserKey;
        button.interactable = !isMine;
        label.text = item.ItemName + " / " + item.Price + " 코인 / 판매자: " + sellerText;

        if (isMine)
        {
            label.text += " / 내 물품";
        }
    }

    void RefreshStatus()
    {
        SetText(statusText, "닉네임: " + currentUser.NickName + " / 코인: " + currentUser.Coin + " / 최고 점수: " + currentUser.Score);
    }

    void SetAuctionStatus(string message)
    {
        SetText(auctionStatusText, message);
    }

    void RunOnMainThread(System.Action action)
    {
        if (dispatcher == null)
        {
            action();
            return;
        }

        dispatcher.Enqueue(action);
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
        SetText(messageText, message);
    }
}
