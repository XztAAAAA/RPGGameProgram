using UnityEngine;

public sealed class CombatFactionMarker : MonoBehaviour
{
    [SerializeField]
    private CombatFaction faction;

    public CombatFaction Faction => faction;
}
