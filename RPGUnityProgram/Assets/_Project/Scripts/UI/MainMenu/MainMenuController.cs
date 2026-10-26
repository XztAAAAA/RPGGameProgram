using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private string arenaSceneName = "Prototype_Arena";

    private void Awake()
    {
        if (startButton == null)
            startButton = GetComponentInChildren<Button>(true);
    }

    private void OnEnable()
    {
        if (startButton != null)
            startButton.onClick.AddListener(StartGame);
    }

    private void OnDisable()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(StartGame);
    }

    public void StartGame()
    {
        if (!Application.CanStreamedLevelBeLoaded(arenaSceneName))
        {
            Debug.LogError(
                $"MainMenuController：场景 {arenaSceneName} 未加入 Build Settings。",
                this
            );
            return;
        }

        SceneManager.LoadScene(arenaSceneName);
    }
}

