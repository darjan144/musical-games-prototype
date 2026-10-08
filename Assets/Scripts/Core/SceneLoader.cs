using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Moves between the menu and the game scenes.
/// Creates itself on first use and survives scene loads, so no scene needs one placed in it.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public const string MainMenu = "MainMenu";

    static SceneLoader instance;

    public static SceneLoader Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("SceneLoader");
                instance = go.AddComponent<SceneLoader>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    bool loading;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        loading = false;
    }

    public void Load(string sceneName)
    {
        // Children double-click; one load at a time.
        if (loading) return;
        loading = true;

        // The music source lives on the persistent AudioManager and would keep playing in the next scene.
        if (AudioManager.Instance != null) AudioManager.Instance.StopMusic();

        SceneManager.LoadScene(sceneName);
    }

    public void LoadMainMenu()
    {
        Load(MainMenu);
    }
}
