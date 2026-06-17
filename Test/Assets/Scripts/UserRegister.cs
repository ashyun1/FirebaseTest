using Firebase.Database;
using PimDeWitte.UnityMainThreadDispatcher;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UserRegister : MonoBehaviour
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
    [SerializeField] bool LoadNextSceneAfterRegister = false;

    bool isRegistering;

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

    public void OnClickRegister()
    {
        if (isRegistering)
        {
            SetMessage("회원가입 처리 중입니다.");
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

        isRegistering = true;
        SetMessage("닉네임 확인 중...");
        CheckDuplicateNickName(nickName);
    }

    void CheckDuplicateNickName(string nickName)
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
                    CheckDuplicateNickNameByFullScan(nickName);
                    return;
                }

                DataSnapshot snapshot = task.Result;

                if (snapshot.HasChildren)
                {
                    dispatcher.Enqueue(() =>
                    {
                        isRegistering = false;
                        SetMessage("이미 사용 중인 닉네임입니다.");
                    });
                    return;
                }

                CheckDuplicateNickNameByFullScan(nickName);
            });
    }

    void CheckDuplicateNickNameByFullScan(string nickName)
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
                        isRegistering = false;
                        SetMessage("Firebase 읽기 오류");
                    });
                    return;
                }

                foreach (DataSnapshot userSnapshot in task.Result.Children)
                {
                    DataSnapshot nickNameSnapshot = userSnapshot.Child("NickName");

                    if (nickNameSnapshot.Value != null && nickNameSnapshot.Value.ToString() == nickName)
                    {
                        dispatcher.Enqueue(() =>
                        {
                            isRegistering = false;
                            SetMessage("이미 사용 중인 닉네임입니다.");
                        });
                        return;
                    }
                }

                CreateUser(nickName);
            });
    }

    void CreateUser(string nickName)
    {
        DatabaseReference newUserRef = reference.Child("UserInfo").Push();
        string userKey = newUserRef.Key;

        UserData userData = new UserData(nickName);
        string json = JsonUtility.ToJson(userData);

        newUserRef.SetRawJsonValueAsync(json).ContinueWith(task =>
        {
            dispatcher.Enqueue(() =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    isRegistering = false;
                    SetMessage("회원가입 실패");
                    return;
                }

                PlayerPrefs.SetString("UserKey", userKey);
                PlayerPrefs.SetString("UserNickName", nickName);
                PlayerPrefs.Save();

                SetMessage("회원가입 완료");
                isRegistering = false;

                if (LoadNextSceneAfterRegister)
                {
                    SceneManager.LoadScene(NextSceneName);
                }
            });
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
