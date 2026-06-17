using Firebase.Database;
using PimDeWitte.UnityMainThreadDispatcher;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UnitShopSceneManager : MonoBehaviour
{
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;
    UnitPurchaseFeature unitPurchaseFeature;
    AdvancedUserState currentUser = new AdvancedUserState();

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://final-firebase-userdata-default-rtdb.firebaseio.com";

    [Header("UI")]
    [SerializeField] Text statusText;
    [SerializeField] Text unitListText;
    [SerializeField] Text messageText;
    [SerializeField] Button unit2Button;
    [SerializeField] Button unit3Button;
    [SerializeField] Button unit4Button;
    [SerializeField] Text unit2ButtonText;
    [SerializeField] Text unit3ButtonText;
    [SerializeField] Text unit4ButtonText;

    void Start()
    {
        reference = FirebaseDatabaseProvider.GetRootReference(databaseUrl);
        unitPurchaseFeature = new UnitPurchaseFeature(reference);

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
                RefreshUnitText();
                RefreshUnitButtons();
                SetMessage("유닛 데이터 불러오기 완료");
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

    public void OnClickBuyUnit2()
    {
        BuyUnit("Unit2", 150);
    }

    public void OnClickBuyUnit3()
    {
        BuyUnit("Unit3", 300);
    }

    public void OnClickBuyUnit4()
    {
        BuyUnit("Unit4", 450);
    }

    public void OnClickBackToShop()
    {
        SceneManager.LoadScene("ShopScene");
    }

    void BuyUnit(string unitName, int price)
    {
        unitPurchaseFeature.BuyUnit(currentUser.UserKey, unitName, price, (state, message) =>
        {
            RunOnMainThread(() =>
            {
                currentUser = state;
                RefreshStatus();
                RefreshUnitText();
                RefreshUnitButtons();
                SetMessage(message);
            });
        }, message => RunOnMainThread(() => SetMessage(message)));
    }

    void RefreshUnitText()
    {
        SetText(unitListText,
            "Unit1 : 보유\n" +
            "Unit2 : " + GetOwnedText("Unit2") + "\n" +
            "Unit3 : " + GetOwnedText("Unit3") + "\n" +
            "Unit4 : " + GetOwnedText("Unit4"));
    }

    string GetOwnedText(string unitName)
    {
        if (currentUser.UnitList.ContainsKey(unitName) && currentUser.UnitList[unitName])
        {
            return "보유";
        }

        return "미보유";
    }

    void RefreshUnitButtons()
    {
        RefreshUnitButton(unit2Button, unit2ButtonText, "Unit2", 150);
        RefreshUnitButton(unit3Button, unit3ButtonText, "Unit3", 300);
        RefreshUnitButton(unit4Button, unit4ButtonText, "Unit4", 450);
    }

    void RefreshUnitButton(Button targetButton, Text targetText, string unitName, int price)
    {
        bool owned = currentUser.UnitList.ContainsKey(unitName) && currentUser.UnitList[unitName];

        if (targetButton != null)
        {
            targetButton.interactable = !owned;
        }

        if (owned)
        {
            SetText(targetText, unitName + " 보유 완료");
            return;
        }

        SetText(targetText, unitName + " 구매 / " + price + " 코인");
    }

    void RefreshStatus()
    {
        SetText(statusText, "닉네임: " + currentUser.NickName + " / 코인: " + currentUser.Coin + " / 최고 점수: " + currentUser.Score);
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
