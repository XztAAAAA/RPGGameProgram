using UnityEngine;

public enum CombatFaction
{
    Player,
    Enemy
}

public class Combatant : MonoBehaviour
{
    [SerializeField]
    private CombatFaction faction = CombatFaction.Enemy;

    public CombatFaction Faction => faction;

    void Awake()
    {
        Health health = GetComponent<Health>();
        HealthBar healthBar = GetComponentInChildren<HealthBar>(true);

        if (healthBar != null)
            healthBar.Initialize(health);
    }
}