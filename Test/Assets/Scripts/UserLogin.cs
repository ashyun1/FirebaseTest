using Firebase.Database;
using PimDeWitte.UnityMainThreadDispatcher;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UserLogin : MonoBehaviour
{
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://final-firebase-userdata-default-rtdb.firebaseio.com";

    [Header("UI")]
    [SerializeField] InputField NickNameInput;
    [SerializeField] Text CheckText;

    [Header("Scene")]
    [SerializeField] string NextSceneName = "MainScene";
    [SerializeField] bool LoadNextSceneAfterLogin = false;

    bool isLoggingIn;

    void Start()
    {
        reference = FirebaseDatabaseProvider.GetRootReference(databaseUrl);
        SetupDispatcher();
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

    public void OnClickLogin()
    {
        if (isLoggingIn)
        {
            SetMessage("로그인 처리 중입니다.");
            return;
        }

        if (NickNameInput == null)
        {
            SetMessage("NickNameInput이 연결되지 않았습니다.");
            return;
        }

        string nickName = NickNameInput.text.Trim();

        if (string.IsNullOrEmpty(nickName))
        {
            SetMessage("닉네임을 입력하세요.");
            return;
        }

        isLoggingIn = true;
        SetMessage("로그인 확인 중...");
        Login(nickName);
    }

    void Login(string nickName)
    {
        reference
            .Child("UserInfo")
            .OrderByChild("NickName")
            .EqualTo(nickName)
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    LoginByFullScan(nickName);
                    return;
                }

                DataSnapshot snapshot = task.Result;

                if (!snapshot.HasChildren)
                {
                    LoginByFullScan(nickName);
                    return;
                }

                string selectedUserKey = "";
                int matchedCount = 0;

                foreach (DataSnapshot userSnapshot in snapshot.Children)
                {
                    selectedUserKey = userSnapshot.Key;
                    matchedCount++;
                }

                CompleteLogin(selectedUserKey, nickName, matchedCount);
            });
    }

    void LoginByFullScan(string nickName)
    {
        reference
            .Child("UserInfo")
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        isLoggingIn = false;
                        SetMessage("Firebase 읽기 오류");
                    });
                    return;
                }

                string selectedUserKey = "";
                int matchedCount = 0;

                foreach (DataSnapshot userSnapshot in task.Result.Children)
                {
                    DataSnapshot nickNameSnapshot = userSnapshot.Child("NickName");

                    if (nickNameSnapshot.Value != null && nickNameSnapshot.Value.ToString() == nickName)
                    {
                        selectedUserKey = userSnapshot.Key;
                        matchedCount++;
                    }
                }

                if (string.IsNullOrEmpty(selectedUserKey))
                {
                    dispatcher.Enqueue(() =>
                    {
                        isLoggingIn = false;
                        SetMessage("존재하지 않는 닉네임입니다.");
                    });
                    return;
                }

                CompleteLogin(selectedUserKey, nickName, matchedCount);
            });
    }

    void CompleteLogin(string userKey, string nickName, int matchedCount)
    {
        dispatcher.Enqueue(() =>
        {
            if (string.IsNullOrEmpty(userKey))
            {
                isLoggingIn = false;
                SetMessage("존재하지 않는 닉네임입니다.");
                return;
            }

            PlayerPrefs.SetString("UserKey", userKey);
            PlayerPrefs.SetString("UserNickName", nickName);
            PlayerPrefs.Save();

            isLoggingIn = false;
            SetMessage("로그인 성공");

            if (matchedCount > 1)
            {
                Debug.LogWarning("중복 닉네임 데이터 " + matchedCount + "개 중 " + userKey + "로 로그인했습니다.");
            }

            if (LoadNextSceneAfterLogin)
            {
                SceneManager.LoadScene(NextSceneName);
            }
        });
    }

    void SetMessage(string message)
    {
        if (CheckText != null)
        {
            CheckText.text = message;
        }
    }
}
