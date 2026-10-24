using ProjectMayham.Core;
using UnityEngine;

namespace ProjectMayham.Tutorial
{
    /// <summary>
    /// The way into the tutorial and out of it, and the stage a restart begins at. A new game that takes the
    /// tutorial starts it from here; dying in it loads it again at the start of the stage the player had reached
    /// (with what they had then: the game was saved there); finishing or skipping it goes on to the shelter.
    /// </summary>
    public static class TutorialRun
    {
        // How the player starts: hurt, hungry and thirsty, so that food, bandages, the cooler and the toilet show what they do.
        private const float StartHealth = 50f;
        private const float StartHunger = 0.7f;
        private const float StartThirst = 0.65f;
        private const float StartNature = 0.45f;

        /// <summary>The stage the tutorial starts at when its scene loads: 0, 1 or 2.</summary>
        public static int Checkpoint { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Checkpoint = 0;

        /// <summary>A new game that begins with the tutorial.</summary>
        public static void Begin()
        {
            GameSession.NewGame();
            var data = GameSession.Data;
            data.health = StartHealth;
            data.needs = new NeedsData { hunger = StartHunger, thirst = StartThirst, nature = StartNature };
            GameSession.Save();
            Checkpoint = 0;
            SceneLoader.Load(SceneLoader.Tutorial, "Обучение", save: false);
        }

        /// <summary>Back to the start of the stage reached, as the player was there (after a death).</summary>
        public static void Restart() => SceneLoader.Load(SceneLoader.Tutorial, "Обучение", save: false);

        /// <summary>The tutorial is over (done or skipped): the real game starts in the shelter, with what the player carries.</summary>
        public static void Finish()
        {
            Checkpoint = 0;
            SceneLoader.Load(SceneLoader.Shelter, "Убежище");
        }
    }
}
