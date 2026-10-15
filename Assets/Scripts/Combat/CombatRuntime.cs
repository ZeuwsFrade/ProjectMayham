using ProjectMayham.AI;
using UnityEngine;

namespace ProjectMayham.Combat
{
    /// <summary>
    /// One hidden object that keeps the timers of the fight that belong to no actor: the freeze of a hit and the
    /// slow fall of the alarm. It makes itself when it is first needed and lives through scene changes.
    /// </summary>
    public class CombatRuntime : MonoBehaviour
    {
        private static CombatRuntime instance;
        // Objects that say goodbye while the game shuts down must not make a new one.
        private static bool quitting;

        /// <summary>Makes sure the object exists. Cheap to call.</summary>
        public static void Ensure()
        {
            if (instance != null || quitting || !Application.isPlaying) return;

            var go = new GameObject("CombatRuntime") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CombatRuntime>();
        }

        // Not called Reset: on a MonoBehaviour that name is Unity's own message.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            quitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting() => quitting = true;

        private void Update()
        {
            // Unscaled: the freeze of a hit stops the game clock, but its own end must not wait for it.
            float dt = Time.unscaledDeltaTime;
            HitStop.Tick(dt);
            AlertDirector.Tick(dt);
        }
    }
}
