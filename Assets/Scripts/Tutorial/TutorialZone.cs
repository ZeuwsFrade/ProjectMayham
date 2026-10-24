using ProjectMayham.Player;
using UnityEngine;

namespace ProjectMayham.Tutorial
{
    /// <summary>
    /// A stretch of floor the tutorial wants to know about: the player walking into it tells the
    /// <see cref="TutorialDirector"/> its id (past the guard, through the hole, at the exit).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class TutorialZone : MonoBehaviour
    {
        [SerializeField] private string id;

        public string Id => id;

        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;
            var director = TutorialDirector.Active;
            if (director != null) director.Reached(id);
        }
    }
}
