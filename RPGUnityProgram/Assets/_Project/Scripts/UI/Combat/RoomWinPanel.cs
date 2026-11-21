using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoomWinPanel : MonoBehaviour
{
    public Button continueButton;

    public Button ReturnButton;

    public void ShowUpgrade(cfg.Upgrade upgrade, int level)
    {
        Transform detail = transform.Find("Card/Detail");
        TMP_Text detailText = detail != null ? detail.GetComponent<TMP_Text>() : null;
        if (detailText == null)
        {
            Debug.LogError("RoomWinPanel：找不到强化结果文本 Card/Detail。", this);
            return;
        }

        detailText.text = $"强化生效：{upgrade.Name} Lv.{level}\n{upgrade.Description}";
        detailText.color = new Color(1f, 0.85f, 0.35f);
        detailText.enableAutoSizing = true;
    }

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
