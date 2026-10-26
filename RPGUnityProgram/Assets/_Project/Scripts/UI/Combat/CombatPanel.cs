
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CombatPanel : MonoBehaviour
{
    [Header("Player 血条")]
    [SerializeField] private HealthBar healthBar;

    [Header("房间管理")]
    [SerializeField] private RoomCombatManager roomManager;
    [SerializeField] private TMP_Text statusText;

    [Header("动态面板资源路径")]
    [SerializeField] private string clearPanelPath = "UI/RoomWinPanel";
    [SerializeField] private string failedPanelPath = "UI/RoomFailedPanel";

    private GameObject clearPanel;
    private GameObject failedPanel;
    private Button retryButton;
    private Button continueButton;

    private RoomState lastState = (RoomState)(-1);

    private void Awake()
    {
        if (roomManager == null)
        {
            roomManager =
                FindAnyObjectByType<RoomCombatManager>();
        }

        if (healthBar == null)
        {
            healthBar = GetComponentInChildren<HealthBar>(true);
        }

        // 找到承载 UI 的 Canvas
        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
            canvas = FindAnyObjectByType<Canvas>();

        if (canvas == null)
        {
            Debug.LogError(
                "CombatPanel：场景中没有找到 Canvas。",
                this
            );
            return;
        }

        // 动态加载并创建两个面板
        clearPanel = LoadPanel(clearPanelPath, canvas.transform);
        clearPanel.SetActive(false);
        failedPanel = LoadPanel(failedPanelPath, canvas.transform);
        failedPanel.SetActive(false);

        // 自动寻找失败面板中的重试按钮
        if (failedPanel != null)
        {
            Button[] failedButtons =
                failedPanel.GetComponentsInChildren<Button>(true);

            foreach (Button button in failedButtons)
            {
                if (button.name == "RetryButton")
                {
                    retryButton = button;
                    break;
                }
            }

            // 如果面板中只有一个按钮，也允许直接使用它
            if (retryButton == null && failedButtons.Length == 1)
                retryButton = failedButtons[0];
        }

        // 自动寻找胜利面板中的继续按钮
        if (clearPanel != null)
        {
            Button[] clearButtons =
                clearPanel.GetComponentsInChildren<Button>(true);

            foreach (Button button in clearButtons)
            {
                if (button.name == "ContinueButton")
                {
                    continueButton = button;
                    break;
                }
            }
        }
    }

    private GameObject LoadPanel(string path, Transform parent)
    {
        GameObject prefab = Resources.Load<GameObject>(path);

        if (prefab == null)
        {
            Debug.LogError(
                $"CombatPanel：无法加载面板 Prefab，路径为 Resources/{path}",
                this
            );
            return null;
        }

        GameObject instance = Instantiate(prefab, parent, false);
        instance.SetActive(false);

        Debug.Log($"CombatPanel：已动态创建面板 {instance.name}");

        return instance;
    }

    private void OnEnable()
    {
        if (retryButton != null)
            retryButton.onClick.AddListener(RestartCurrentScene);

        if (continueButton != null)
            continueButton.onClick.AddListener(HideClearPanel);
    }

    private void Start()
    {
        InitializePlayerHealthBar();
        RefreshRoomState();
    }

    private void Update()
    {
        if (roomManager == null)
            return;

        RoomState currentState = roomManager.State;

        if (currentState != lastState)
        {
            UpdateRoomStateUI(currentState);
            lastState = currentState;
        }

        if (currentState == RoomState.InProgress &&
            statusText != null)
        {
            statusText.text =
                $"战斗中 | 剩余敌人：{roomManager.RemainingEnemies}";
        }
    }

    private void InitializePlayerHealthBar()
    {
        if (healthBar == null)
        {
            Debug.LogError(
                "CombatPanel：没有找到 HealthBar。",
                this
            );
            return;
        }

        GameObject player = CombatTool.GetPlayer();

        if (player == null)
        {
            Debug.LogError(
                "CombatPanel：没有找到 Player。",
                this
            );
            return;
        }

        Health playerHealth = player.GetComponent<Health>();

        if (playerHealth == null)
        {
            Debug.LogError(
                "CombatPanel：Player 没有 Health 组件。",
                player
            );
            return;
        }

        healthBar.Initialize(playerHealth);
        Debug.Log("CombatPanel：主角血条初始化成功。");
    }

    private void RefreshRoomState()
    {
        if (roomManager == null)
        {
            Debug.LogError(
                "CombatPanel：没有找到 RoomCombatManager。",
                this
            );
            return;
        }

        lastState = roomManager.State;
        UpdateRoomStateUI(lastState);
    }

    private void UpdateRoomStateUI(RoomState state)
    {
        if (clearPanel != null)
            clearPanel.SetActive(state == RoomState.Cleared);

        if (failedPanel != null)
            failedPanel.SetActive(state == RoomState.Failed);

        if (statusText == null)
            return;

        switch (state)
        {
            case RoomState.Ready:
                statusText.text = "准备战斗";
                break;

            case RoomState.InProgress:
                statusText.text =
                    $"战斗中 | 剩余敌人：{roomManager.RemainingEnemies}";
                break;

            case RoomState.Cleared:
                statusText.text = "房间已清空！";
                break;

            case RoomState.Failed:
                statusText.text = "挑战失败";
                break;
        }
    }

    public void RestartCurrentScene()
    {
        Time.timeScale = 1f;

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

    private void HideClearPanel()
    {
        if (clearPanel != null)
            clearPanel.SetActive(false);
    }


    private void OnDisable()
    {
        if (retryButton != null)
            retryButton.onClick.RemoveListener(RestartCurrentScene);

        if (continueButton != null)
            continueButton.onClick.RemoveListener(HideClearPanel);
    }
}
