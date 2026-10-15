using UnityEngine;

namespace ProjectMayham.AI
{
    /// <summary>
    /// Everything that makes one kind of enemy what it is, as numbers: how tough, how fast, how much it notices and
    /// how it hits. The behaviour (<see cref="EnemyAI"/>) is the same for all; a new enemy is mostly a new definition.
    /// </summary>
    [CreateAssetMenu(menuName = "Mayham/Enemy", fileName = "NewEnemy")]
    public class EnemyDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Враг";

        [Header("Body")]
        [SerializeField, Min(1f)] private float maxHealth = 30f;
        [Tooltip("Share of the stagger of a blow that is ignored. 1 = never staggers.")]
        [SerializeField, Range(0f, 1f)] private float staggerResistance;
        [Tooltip("Share of the knockback of a blow that is ignored.")]
        [SerializeField, Range(0f, 1f)] private float knockbackResistance;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float patrolSpeed = 0.9f;
        [Tooltip("Speed when it goes to look into something.")]
        [SerializeField, Min(0.1f)] private float investigateSpeed = 1.8f;
        [SerializeField, Min(0.1f)] private float chaseSpeed = 2.7f;
        [SerializeField, Min(1f)] private float acceleration = 10f;
        [SerializeField, Min(30f)] private float turnSpeed = 300f;
        [Tooltip("How far from its post it wanders while on patrol.")]
        [SerializeField, Min(0f)] private float patrolRadius = 7f;
        [Tooltip("Seconds it stands between two walks, from and to.")]
        [SerializeField] private Vector2 patrolPause = new Vector2(1.5f, 4f);

        [Header("Senses")]
        [SerializeField, Min(1f)] private float sightRange = 7f;
        [Tooltip("Width of the field of view in degrees.")]
        [SerializeField, Range(10f, 360f)] private float sightAngle = 110f;
        [Tooltip("Inside this distance it notices the player from any side (but not through a wall).")]
        [SerializeField, Min(0f)] private float senseRange = 2.2f;
        [Tooltip("Multiplies how far the noise carries to it. 0 = deaf.")]
        [SerializeField, Min(0f)] private float hearing = 1f;
        [Tooltip("Seconds of looking at the player from close by before it is sure what it sees.")]
        [SerializeField, Min(0.05f)] private float noticeSeconds = 1f;
        [Tooltip("Seconds without a sign of the player before the suspicion is forgotten.")]
        [SerializeField, Min(0.5f)] private float forgetSeconds = 8f;

        [Header("Hunt")]
        [Tooltip("Seconds it goes on hunting after losing sight, before it starts to search.")]
        [SerializeField, Min(0f)] private float loseTargetSeconds = 4f;
        [Tooltip("Seconds it looks around where it last saw the player.")]
        [SerializeField, Min(0f)] private float searchSeconds = 4f;

        [Header("Attack")]
        [Tooltip("Distance, centre to centre, from which it strikes.")]
        [SerializeField, Min(0.3f)] private float attackRange = 1.15f;
        [Tooltip("Width of the strike in degrees.")]
        [SerializeField, Range(10f, 360f)] private float attackArc = 110f;
        [SerializeField, Min(0f)] private float damage = 7f;
        [SerializeField, Min(0f)] private float attackKnockback = 2.5f;
        [Tooltip("Seconds between starting to swing and the blow. The warning the player has.")]
        [SerializeField, Min(0.05f)] private float windupSeconds = 0.65f;
        [Tooltip("Seconds it stands after the blow.")]
        [SerializeField, Min(0f)] private float recoverSeconds = 0.5f;
        [Tooltip("Seconds after the blow before it may begin another.")]
        [SerializeField, Min(0f)] private float cooldownSeconds = 1f;
        [Tooltip("How far it lunges forward in the blow, in world units.")]
        [SerializeField, Min(0f)] private float lunge = 0.35f;

        [Header("Pack")]
        [Tooltip("Whether it shouts to wake the others when it finds the player.")]
        [SerializeField] private bool shouts = true;
        [SerializeField, Min(0f)] private float shoutRadius = 14f;
        [Tooltip("Within this distance of the player it goes round to its own place in the ring instead of straight at them.")]
        [SerializeField, Min(0f)] private float surroundRange = 5f;
        [SerializeField, Min(0.1f)] private float separationRadius = 0.9f;

        public string DisplayName => displayName;
        public float MaxHealth => maxHealth;
        public float StaggerResistance => staggerResistance;
        public float KnockbackResistance => knockbackResistance;
        public float PatrolSpeed => patrolSpeed;
        public float InvestigateSpeed => investigateSpeed;
        public float ChaseSpeed => chaseSpeed;
        public float Acceleration => acceleration;
        public float TurnSpeed => turnSpeed;
        public float PatrolRadius => patrolRadius;
        public Vector2 PatrolPause => patrolPause;
        public float SightRange => sightRange;
        public float SightAngle => sightAngle;
        public float SenseRange => senseRange;
        public float Hearing => hearing;
        public float NoticeSeconds => noticeSeconds;
        public float ForgetSeconds => forgetSeconds;
        public float LoseTargetSeconds => loseTargetSeconds;
        public float SearchSeconds => searchSeconds;
        public float AttackRange => attackRange;
        public float AttackArc => attackArc;
        public float Damage => damage;
        public float AttackKnockback => attackKnockback;
        public float WindupSeconds => windupSeconds;
        public float RecoverSeconds => recoverSeconds;
        public float CooldownSeconds => cooldownSeconds;
        public float Lunge => lunge;
        public bool Shouts => shouts;
        public float ShoutRadius => shoutRadius;
        public float SurroundRange => surroundRange;
        public float SeparationRadius => separationRadius;
    }
}
