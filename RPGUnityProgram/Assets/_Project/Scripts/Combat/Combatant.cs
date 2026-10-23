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
}