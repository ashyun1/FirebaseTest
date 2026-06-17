using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneNavigator : MonoBehaviour
{
    [SerializeField] string targetSceneName;

    public void LoadTargetScene()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("이동할 씬 이름이 비어 있습니다.");
            return;
        }

        SceneManager.LoadScene(targetSceneName);
    }

    public void LogoutAndLoadTargetScene()
    {
        PlayerPrefs.DeleteKey("UserKey");
        PlayerPrefs.DeleteKey("UserNickName");
        PlayerPrefs.Save();

        LoadTargetScene();
    }
}
