using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoomFailedPanel : MonoBehaviour
{
    public Button retryButton;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnEnable()
    {
        retryButton.onClick.AddListener(RestartCurrentScene);
    }

    public void RestartCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError(
                "CombatPanel：当前场景没有有效路径。",
                this
            );
            return;
        }

        SceneManager.LoadSceneAsync(scene.path);
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnDisable()
    {
        retryButton.onClick.RemoveListener(RestartCurrentScene);
    }
}
