using UnityEngine;
using UnityEngine.UI;

public class RoomWinPanel : MonoBehaviour
{
    public Button continueButton;

    public Button ReturnButton;

    private void OnEnable()
    {
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(
                !LevelManager.Instance.IsLastLevel());
        }

        if (continueButton != null)
            continueButton.onClick.AddListener(NextLevel);

        if (ReturnButton != null)
            ReturnButton.onClick.AddListener(ReturnMainMenu);
    }

    private void NextLevel()
    {
        LevelManager.Instance.GoToNextLevel();
    }

    private void ReturnMainMenu()
    {
        LevelManager.Instance.ReturnToMainMenu();
    }

    private void OnDisable()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(NextLevel);

        if (ReturnButton != null)
            ReturnButton.onClick.RemoveListener(ReturnMainMenu);
    }
}
