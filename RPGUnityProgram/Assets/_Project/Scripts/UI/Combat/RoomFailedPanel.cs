using UnityEngine;
using UnityEngine.UI;

public class RoomFailedPanel : MonoBehaviour
{
    public Button retryButton;

    public Button backButton;
    private void OnEnable()
    {
        if (retryButton != null)
            retryButton.onClick.AddListener(RestartCurrentScene);

        if (backButton != null)
            backButton.onClick.AddListener(ReturnToMainMenu);
    }

    public void RestartCurrentScene()
    {
        LevelManager.Instance.RetryCurrentLevel();
    }

    public void ReturnToMainMenu()
    {
        LevelManager.Instance.ReturnToMainMenu();
    }

    private void OnDisable()
    {
        if (retryButton != null)
            retryButton.onClick.RemoveListener(RestartCurrentScene);

        if (backButton != null)
            backButton.onClick.RemoveListener(ReturnToMainMenu);
    }
}
