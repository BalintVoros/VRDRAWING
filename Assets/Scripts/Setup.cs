using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class Setup : MonoBehaviour
{
    [Tooltip("The first scene to load")]
    private static readonly string firstSceneName = "Login";

    void Awake()
    {
        DatabaseManager.Instance.Initialize();
    }

    void Start()
    {
        StartCoroutine(LoadFirstSceneWithDelay());
    }

    private IEnumerator LoadFirstSceneWithDelay()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        yield return new WaitForSeconds(0.2f);
        SceneManager.LoadScene(firstSceneName);
    }
}
