using UnityEngine;

[DefaultExecutionOrder(-1000)]
public sealed class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 1f, 0f);
    [SerializeField] private Vector3 spawnEulerAngles = new Vector3(0f, 270f, 0f);

    private void Awake()
    {
        if (GameObject.FindGameObjectWithTag("Player") != null)
        {
            Debug.LogError("PlayerSpawner：场景中已存在玩家，无法生成新的玩家。", this);
            return;
        }

        if (playerPrefab == null || !playerPrefab.activeSelf || !playerPrefab.CompareTag("Player"))
        {
            Debug.LogError("PlayerSpawner：请配置已激活且 Tag 为 Player 的玩家 Prefab。", this);
            return;
        }

        Instantiate(playerPrefab, spawnPosition, Quaternion.Euler(spawnEulerAngles));
    }
}
