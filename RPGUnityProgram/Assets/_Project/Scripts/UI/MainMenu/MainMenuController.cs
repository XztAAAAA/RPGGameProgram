using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private Button startButton;

    private void Awake()
    {
        if (startButton == null)
            startButton = GetComponentInChildren<Button>(true);
        FrameRateSettings.Configure();
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
        LevelManager.Instance.StartNewRun();
    }

    public static class FrameRateSettings
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Configure()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120;
    }
}
}

