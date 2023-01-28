using UnityEngine.SceneManagement;

public class Global : MonoSingleton<Global>
{
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
