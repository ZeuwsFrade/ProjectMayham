using UnityEngine;

namespace ProjectMayham.Npcs
{
    /// <summary>
    /// A place in a shelter scene where a rescued NPC stands. With an id it is that NPC's place; without one it is
    /// taken by any rescued NPC that has no place of their own.
    /// </summary>
    public class NpcSpot : MonoBehaviour
    {
        [Tooltip("The id of the NPC (the name of their asset in Resources/Npcs). Empty: any NPC.")]
        [SerializeField] private string npcId;

        public string NpcId => npcId != null ? npcId.Trim() : string.Empty;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 0.5f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
