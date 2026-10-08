using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Put on a UI Button: clicking it loads the named scene through the SceneLoader.
/// </summary>
[RequireComponent(typeof(Button))]
public class SceneButton : MonoBehaviour
{
    [Tooltip("Scene name as listed in Build Settings, e.g. MainMenu or Game1_Rhythm.")]
    [SerializeField] string sceneName = SceneLoader.MainMenu;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => SceneLoader.Instance.Load(sceneName));
    }
}
