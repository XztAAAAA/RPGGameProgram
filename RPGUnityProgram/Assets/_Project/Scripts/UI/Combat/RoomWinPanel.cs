using UnityEngine;
using UnityEngine.UI;

public class RoomWinPanel : MonoBehaviour
{
    public Button continueButton;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnEnable()
    {
        continueButton.onClick.AddListener(HideClearPanel);
    }

    private void HideClearPanel()
    {
        gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnDisable()
    {
        continueButton.onClick.RemoveListener(HideClearPanel);
    }
}
