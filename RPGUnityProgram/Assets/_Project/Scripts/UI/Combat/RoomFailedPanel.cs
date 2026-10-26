using Unity.Burst.Intrinsics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoomFailedPanel : MonoBehaviour
{
    public Button retryButton;

    public Button backButton;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnEnable()
    {
        retryButton.onClick.AddListener(RestartCurrentScene);
        backButton.onClick.AddListener(ReturnToMainMenu);
    }

    public void RestartCurrentScene()
    {
        CommonTool.SetTimeScale(1f);

        Scene scene = SceneManager.GetActiveScene();

        if (string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError(
                "CombatPanel：当前场景没有有效路径。",
                this
            );
            return;
        }

        CommonTool.LoadScene(scene.path);
    }

    public void ReturnToMainMenu()
    {
        CommonTool.SetTimeScale(1f);
        CommonTool.LoadScene("MainMenu");
    }

    private void OnDisable()
    {
        retryButton.onClick.RemoveListener(RestartCurrentScene);
        backButton.onClick.RemoveListener(ReturnToMainMenu);
    }
}
