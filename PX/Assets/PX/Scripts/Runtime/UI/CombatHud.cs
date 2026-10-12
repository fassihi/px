using UnityEngine;

namespace PX
{
    /// <summary>
    /// The fight's numbers on screen, soulslike: her health and stamina bars in the corner, with a trailing bar that
    /// shows what the last blow took; the health of the enemy she is fighting along the bottom, with its name; a prompt
    /// while a counter is on offer; and a title when she falls. Drawn with IMGUI for now, so it needs no assets.
    /// </summary>
    public sealed class CombatHud : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private EnemyBrain[] enemies = System.Array.Empty<EnemyBrain>();

        [SerializeField] private Color healthColor = new Color(0.78f, 0.16f, 0.14f);
        [SerializeField] private Color staminaColor = new Color(0.24f, 0.7f, 0.42f);
        [SerializeField] private Color trailColor = new Color(0.95f, 0.8f, 0.4f);
        [SerializeField] private Color backColor = new Color(0f, 0f, 0f, 0.6f);

        private Health health;
        private float healthTrail = 1f;
        private float enemyTrail = 1f;
        private Health lastEnemy;
        private GUIStyle label;
        private GUIStyle title;

        private void Awake()
        {
            health = player != null ? player.GetComponent<Health>() : null;
        }

        private void Update()
        {
            // The trailing bar holds a moment, then drains to the real value.
            float dt = Time.unscaledDeltaTime;
            if (health != null)
                healthTrail = Mathf.MoveTowards(healthTrail, health.Current / health.Max, dt * 0.6f);
            Health enemy = FightingEnemy();
            if (enemy != lastEnemy)
                enemyTrail = enemy != null ? enemy.Current / enemy.Max : 1f;
            lastEnemy = enemy;
            if (enemy != null)
                enemyTrail = Mathf.MoveTowards(enemyTrail, enemy.Current / enemy.Max, dt * 0.6f);
        }

        /// <summary>The enemy she is fighting: the nearest one following her that is still alive.</summary>
        private Health FightingEnemy()
        {
            Health best = null;
            float bestDistance = float.MaxValue;
            if (player == null)
                return null;
            foreach (EnemyBrain brain in enemies)
            {
                if (brain == null || !brain.Engaged)
                    continue;
                var enemyHealth = brain.GetComponent<Health>();
                if (enemyHealth == null || enemyHealth.IsDead)
                    continue;
                float distance = Vector3.Distance(brain.transform.position, player.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = enemyHealth;
                }
            }
            return best;
        }

        private void OnGUI()
        {
            if (player == null || health == null)
                return;
            label ??= new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            title ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 54, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.85f, 0.2f, 0.15f) },
            };

            // Hers in the top left corner, the enemy's along the bottom: the usual soulslike layout.
            float x = 16f, y = 14f;
            Bar(new Rect(x, y, 360f * health.Max / 100f, 14f), health.Current / health.Max, healthTrail, healthColor);
            Stamina stamina = player.Stamina;
            if (stamina != null)
                Bar(new Rect(x, y + 22f, 300f * stamina.Max / 100f, 9f), stamina.Current / stamina.Max, 0f, staminaColor);

            Health enemy = FightingEnemy();
            if (enemy != null)
            {
                float width = Mathf.Min(720f, Screen.width - 120f);
                var rect = new Rect((Screen.width - width) * 0.5f, Screen.height - 40f, width, 12f);
                GUI.Label(new Rect(rect.x, rect.y - 24f, width, 22f), DisplayName(enemy), label);
                Bar(rect, enemy.Current / enemy.Max, enemyTrail, healthColor);
            }

            if (player.CanCounter)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 120f, Screen.height * 0.3f, 240f, 30f),
                    "<color=#ffd76a>Counter!</color>  (Attack)", new GUIStyle(label) { richText = true, alignment = TextAnchor.MiddleCenter });
            }

            if (player.State == PlayerState.Dead)
            {
                GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(player.StateTime) * 0.5f);
                GUI.DrawTexture(new Rect(0f, Screen.height * 0.4f, Screen.width, 90f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(0f, Screen.height * 0.4f, Screen.width, 90f), "FALLEN", title);
            }
        }

        /// <summary>"EnemyA" reads as "Enemy A".</summary>
        private static string DisplayName(Health enemy)
        {
            string name = enemy.name;
            return name.StartsWith("Enemy") && name.Length > 5 ? "Enemy " + name.Substring(5) : name;
        }

        private void Bar(Rect rect, float fill, float trail, Color color)
        {
            GUI.color = backColor;
            GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), Texture2D.whiteTexture);
            if (trail > fill)
            {
                GUI.color = trailColor;
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * trail, rect.height), Texture2D.whiteTexture);
            }
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
