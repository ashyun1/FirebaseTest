using Firebase.Database;
using PimDeWitte.UnityMainThreadDispatcher;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameResultSceneManager : MonoBehaviour
{
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;
    GameResultFeature gameResultFeature;
    AdvancedUserState currentUser = new AdvancedUserState();

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://final-firebase-userdata-default-rtdb.firebaseio.com";

    [Header("UI")]
    [SerializeField] Text statusText;
    [SerializeField] Text resultInfoText;
    [SerializeField] Text messageText;
    [SerializeField] Button trainingBattleButton;
    [SerializeField] Button fieldBattleButton;
    [SerializeField] Button bossBattleButton;
    [SerializeField] Text trainingBattleButtonText;
    [SerializeField] Text fieldBattleButtonText;
    [SerializeField] Text bossBattleButtonText;

    void Start()
    {
        reference = FirebaseDatabaseProvider.GetRootReference(databaseUrl);
        gameResultFeature = new GameResultFeature(reference);

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
        RefreshResultInfo();
        RefreshBattleButtons();
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
                RefreshResultInfo();
                RefreshBattleButtons();
                SetMessage("유닛 전투 데이터 불러오기 완료");
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

    public void OnClickStartTrainingBattle()
    {
        PlayUnitBattle("연습 전투", 1, 20, 10, 20);
    }

    public void OnClickStartFieldBattle()
    {
        PlayUnitBattle("일반 전투", 2, 70, 25, 35);
    }

    public void OnClickStartBossBattle()
    {
        PlayUnitBattle("보스 전투", 3, 140, 45, 60);
    }

    public void OnClickBackToShop()
    {
        SceneManager.LoadScene("ShopScene");
    }

    void PlayUnitBattle(string battleName, int requiredUnitLevel, int stageScoreBonus, int rewardBase, int rewardRandomMax)
    {
        int bestUnitLevel = GetBestOwnedUnitLevel();
        if (bestUnitLevel < requiredUnitLevel)
        {
            SetMessage(battleName + "는 Unit" + requiredUnitLevel + " 이상 보유해야 진행할 수 있습니다.");
            return;
        }

        string unitName = "Unit" + bestUnitLevel;
        int unitPower = GetUnitPower(bestUnitLevel);
        int battleScore = unitPower + stageScoreBonus + Random.Range(0, 31);
        int rewardCoin = rewardBase + (battleScore / 10) + Random.Range(0, rewardRandomMax + 1);

        SaveResult(battleName + " - " + unitName + " 출전", battleScore, rewardCoin);
    }

    void SaveResult(string resultName, int score, int rewardCoin)
    {
        gameResultFeature.SaveResult(currentUser.UserKey, resultName, score, rewardCoin, (state, message) =>
        {
            RunOnMainThread(() =>
            {
                currentUser = state;
                RefreshStatus();
                RefreshResultInfo();
                RefreshBattleButtons();
                SetMessage(message);
            });
        }, message => RunOnMainThread(() => SetMessage(message)));
    }

    void RefreshResultInfo()
    {
        SetText(resultInfoText,
            "현재 코인 : " + currentUser.Coin + "\n" +
            "최고 점수 : " + currentUser.Score + "\n\n" +
            "보유 유닛 : " + GetOwnedUnitText() + "\n" +
            "출전 유닛 : Unit" + GetBestOwnedUnitLevel() + "\n\n" +
            "연습 전투 : Unit1 이상\n" +
            "일반 전투 : Unit2 이상\n" +
            "보스 전투 : Unit3 이상");
    }

    void RefreshBattleButtons()
    {
        int bestUnitLevel = GetBestOwnedUnitLevel();
        RefreshBattleButton(trainingBattleButton, trainingBattleButtonText, "연습 전투 시작", bestUnitLevel >= 1);
        RefreshBattleButton(fieldBattleButton, fieldBattleButtonText, "일반 전투 시작", bestUnitLevel >= 2);
        RefreshBattleButton(bossBattleButton, bossBattleButtonText, "보스 전투 시작", bestUnitLevel >= 3);
    }

    void RefreshBattleButton(Button targetButton, Text targetText, string label, bool canPlay)
    {
        if (targetButton != null)
        {
            targetButton.interactable = canPlay;
        }

        if (canPlay)
        {
            SetText(targetText, label);
            return;
        }

        SetText(targetText, label + " / 유닛 부족");
    }

    int GetBestOwnedUnitLevel()
    {
        for (int i = 4; i >= 1; i--)
        {
            string unitName = "Unit" + i;
            if (currentUser.UnitList.ContainsKey(unitName) && currentUser.UnitList[unitName])
            {
                return i;
            }
        }

        return 1;
    }

    int GetUnitPower(int unitLevel)
    {
        if (unitLevel == 4)
        {
            return 240;
        }

        if (unitLevel == 3)
        {
            return 170;
        }

        if (unitLevel == 2)
        {
            return 110;
        }

        return 60;
    }

    string GetOwnedUnitText()
    {
        string result = "";
        for (int i = 1; i <= 4; i++)
        {
            string unitName = "Unit" + i;
            if (currentUser.UnitList.ContainsKey(unitName) && currentUser.UnitList[unitName])
            {
                if (!string.IsNullOrEmpty(result))
                {
                    result += ", ";
                }

                result += unitName;
            }
        }

        if (string.IsNullOrEmpty(result))
        {
            return "Unit1";
        }

        return result;
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
