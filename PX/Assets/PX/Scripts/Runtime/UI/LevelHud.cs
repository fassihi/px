using UnityEngine;

namespace PX
{
    /// <summary>
    /// The level's screen: her health, stamina and flasks and the light she carries in the corner; the health of the
    /// enemy she fights along the bottom, or the boss's with its name; a prompt for what Interact or Attack would do;
    /// what the guards know of her, as a mark over their heads; the letterbox and the name of a place for a reveal; a
    /// toast for small news; the shrine's menu; and the titles for her fall and the level's end. IMGUI, no assets.
    /// </summary>
    public sealed class LevelHud : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private LevelFlow flow;
        [SerializeField] private CameraDirector director;
        [SerializeField] private Camera worldCamera;

        [SerializeField] private Color healthColor = new Color(0.78f, 0.16f, 0.14f);
        [SerializeField] private Color staminaColor = new Color(0.24f, 0.7f, 0.42f);
        [SerializeField] private Color trailColor = new Color(0.95f, 0.8f, 0.4f);
        [SerializeField] private Color flaskColor = new Color(0.95f, 0.5f, 0.2f);
        [SerializeField] private Color lightColor = new Color(1f, 0.78f, 0.35f);
        [SerializeField] private Color backColor = new Color(0f, 0f, 0f, 0.6f);

        private Health health;
        private CharacterMotor motor;
        private float healthTrail = 1f;
        private float enemyTrail = 1f;
        private Health lastEnemy;
        private GUIStyle label;
        private GUIStyle small;
        private GUIStyle prompt;
        private GUIStyle title;
        private GUIStyle card;
        private GUIStyle mark;

        private void Awake()
        {
            health = player != null ? player.GetComponent<Health>() : null;
            motor = player != null ? player.GetComponent<CharacterMotor>() : null;
        }

        private void Update()
        {
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

        /// <summary>The boss while its fight is on; otherwise the nearest alert enemy that still lives.</summary>
        private Health FightingEnemy()
        {
            if (flow != null && flow.BossActive)
                return flow.Boss.Health;
            Health best = null;
            float bestDistance = float.MaxValue;
            if (player == null)
                return null;
            foreach (EnemyController enemy in EnemyController.All)
            {
                if (enemy == null || enemy.Mood != Mood.Alert || enemy.Health.IsDead)
                    continue;
                float distance = Vector3.Distance(enemy.transform.position, player.transform.position);
                if (distance < bestDistance && distance < 25f)
                {
                    bestDistance = distance;
                    best = enemy.Health;
                }
            }
            return best;
        }

        private void MakeStyles()
        {
            label ??= new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            small ??= new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(1f, 1f, 1f, 0.85f) } };
            prompt ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true, normal = { textColor = new Color(1f, 0.9f, 0.7f) } };
            title ??= new GUIStyle(GUI.skin.label) { fontSize = 54, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.85f, 0.2f, 0.15f) } };
            card ??= new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.92f, 0.75f) } };
            mark ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        private void OnGUI()
        {
            if (player == null || health == null)
                return;
            MakeStyles();

            DrawLetterbox();
            DrawMarks();
            DrawHers();
            DrawEnemy();
            DrawPrompt();
            DrawToast();
            DrawMenu();
            DrawCard();
            DrawTitles();
        }

        private void DrawHers()
        {
            float x = 16f, y = 14f;
            Bar(new Rect(x, y, 300f * health.Max / 100f, 14f), health.Current / health.Max, healthTrail, healthColor);
            Stamina stamina = player.Stamina;
            if (stamina != null)
                Bar(new Rect(x, y + 22f, 260f * stamina.Max / 100f, 9f), stamina.Current / stamina.Max, 0f, staminaColor);

            // Flasks: a row of small bottles, the full ones lit.
            for (int i = 0; i < player.MaxFlasks; i++)
            {
                var rect = new Rect(x + i * 22f, y + 40f, 16f, 20f);
                GUI.color = backColor;
                GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), Texture2D.whiteTexture);
                GUI.color = i < player.Flasks ? flaskColor : new Color(0.3f, 0.25f, 0.2f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(x + Mathf.Max(4, player.MaxFlasks) * 22f + 8f, y + 38f, 120f, 24f), "Q  flask", small);

            if (flow != null)
            {
                GUI.color = lightColor;
                GUI.Label(new Rect(x, y + 66f, 240f, 24f), $"✦ {flow.Light:0}" + (flow.Dropped != null ? $"   ({flow.Dropped.Amount:0} dropped)" : string.Empty), label);
                GUI.color = Color.white;
            }
        }

        private void DrawEnemy()
        {
            Health enemy = FightingEnemy();
            if (enemy == null)
                return;
            bool boss = flow != null && flow.BossActive && enemy == flow.Boss.Health;
            float width = Mathf.Min(boss ? 900f : 620f, Screen.width - 120f);
            float bottom = Screen.height * (1f - 0.11f * (director != null ? director.Letterbox : 0f));
            var rect = new Rect((Screen.width - width) * 0.5f, bottom - 44f, width, boss ? 16f : 11f);
            GUI.Label(new Rect(rect.x, rect.y - 26f, width, 22f), boss ? flow.BossName : DisplayName(enemy), label);
            Bar(rect, enemy.Current / enemy.Max, enemyTrail, healthColor);
            // The stagger meter under the health: when it fills, the enemy is open.
            var controller = enemy.GetComponent<EnemyController>();
            if (controller != null && controller.Config != null)
            {
                float stagger = controller.IsStaggered ? 1f : controller.Stagger / controller.Config.staggerMax;
                Bar(new Rect(rect.x, rect.y + rect.height + 5f, width * 0.5f, 5f), stagger, 0f, controller.IsStaggered ? trailColor : new Color(0.85f, 0.75f, 0.3f));
            }
        }

        private void DrawPrompt()
        {
            string text = flow != null ? flow.Prompt : string.Empty;
            if (player.CanCounter)
                text = "<color=#ffd76a>Counter!</color>   (Attack)";
            if (string.IsNullOrEmpty(text))
                return;
            GUI.Label(new Rect(Screen.width * 0.5f - 200f, Screen.height * 0.68f, 400f, 32f), text, prompt);
        }

        private void DrawToast()
        {
            if (flow == null || string.IsNullOrEmpty(flow.Toast))
                return;
            float age = flow.ToastAge;
            if (age > 3f)
                return;
            float alpha = Mathf.Clamp01((3f - age) / 0.6f);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(Screen.width * 0.5f - 250f, Screen.height * 0.76f, 500f, 26f), flow.Toast, prompt);
            GUI.color = Color.white;
        }

        private void DrawMenu()
        {
            if (flow == null || !flow.MenuOpen || player.Combat == null)
                return;
            var box = new Rect(Screen.width * 0.5f - 190f, Screen.height * 0.3f, 380f, 150f);
            GUI.color = backColor;
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
            float y = box.y + 12f;
            GUI.Label(new Rect(box.x + 16f, y, 340f, 24f), $"{flow.MenuShrine?.AreaName}     ✦ {flow.Light:0}", label);
            y += 34f;
            GUI.Label(new Rect(box.x + 16f, y, 340f, 24f), $"[1]  One more flask        ✦ {player.Combat.flaskUpgradeCost:0}", small);
            y += 26f;
            GUI.Label(new Rect(box.x + 16f, y, 340f, 24f), $"[2]  Health +{player.Combat.healthUpgrade:0}            ✦ {player.Combat.healthUpgradeCost:0}", small);
            y += 34f;
            GUI.Label(new Rect(box.x + 16f, y, 340f, 24f), "[E]  Leave", small);
        }

        private void DrawLetterbox()
        {
            float amount = director != null ? director.Letterbox : 0f;
            if (amount <= 0f)
                return;
            float h = Screen.height * 0.11f * amount;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, Screen.height - h, Screen.width, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawCard()
        {
            if (director == null || director.CardAlpha <= 0f)
                return;
            GUI.color = new Color(1f, 1f, 1f, director.CardAlpha);
            GUI.Label(new Rect(0f, Screen.height * 0.78f, Screen.width, 50f), director.Card.ToUpperInvariant(), card);
            GUI.color = Color.white;
        }

        /// <summary>A mark over each guard's head: a dot while it is calm, a question while it looks, a cry when it knows.</summary>
        private void DrawMarks()
        {
            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null)
                return;
            foreach (EnemyController enemy in EnemyController.All)
            {
                if (enemy == null || enemy.Health.IsDead || enemy.Config == null)
                    continue;
                if (Vector3.Distance(enemy.transform.position, player.transform.position) > 30f)
                    continue;
                DrawMark(cam, enemy.transform.position + Vector3.up * (enemy.Config.isBoss ? 3.3f : 2.4f), enemy.Mood, enemy.IsStaggered);
            }
            foreach (Sentinel sentinel in FindObjectsByType<Sentinel>(FindObjectsSortMode.None))
            {
                if (sentinel.Health.IsDead || Vector3.Distance(sentinel.transform.position, player.transform.position) > 30f)
                    continue;
                DrawMark(cam, sentinel.transform.position + Vector3.up * 1.1f, sentinel.Alert ? Mood.Alert : sentinel.Aiming ? Mood.Suspicious : Mood.Calm, false);
            }
        }

        private void DrawMark(Camera cam, Vector3 world, Mood mood, bool staggered)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f)
                return;
            var rect = new Rect(screen.x - 16f, Screen.height - screen.y - 16f, 32f, 32f);
            string text = staggered ? "★" : mood == Mood.Alert ? "!" : mood == Mood.Suspicious ? "?" : "•";
            Color color = staggered ? trailColor : mood == Mood.Alert ? new Color(1f, 0.3f, 0.25f) : mood == Mood.Suspicious ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 1f, 1f, 0.45f);
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, mark);
            GUI.color = color;
            GUI.Label(rect, text, mark);
            GUI.color = Color.white;
        }

        private void DrawTitles()
        {
            if (player.State == PlayerState.Dead)
            {
                GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(player.StateTime) * 0.5f);
                GUI.DrawTexture(new Rect(0f, Screen.height * 0.4f, Screen.width, 90f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(0f, Screen.height * 0.4f, Screen.width, 90f), "FALLEN", title);
            }
            if (flow != null && flow.IsComplete && flow.CompleteAge > 3.5f)
            {
                float alpha = Mathf.Clamp01((flow.CompleteAge - 3.5f) / 1.5f);
                GUI.color = new Color(0f, 0f, 0f, alpha * 0.55f);
                GUI.DrawTexture(new Rect(0f, Screen.height * 0.36f, Screen.width, 140f), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 0.92f, 0.75f, alpha);
                GUI.Label(new Rect(0f, Screen.height * 0.36f, Screen.width, 80f), "THE GATE OPENS", card);
                GUI.Label(new Rect(0f, Screen.height * 0.36f + 70f, Screen.width, 40f), "end of the draft", prompt);
                GUI.color = Color.white;
            }
        }

        /// <summary>"EnemyA" reads as "Soldier"; the names are placeholders until the enemies have theirs.</summary>
        private static string DisplayName(Health enemy)
        {
            string name = enemy.name;
            if (name.StartsWith("EnemyA"))
                return "Knight";
            if (name.StartsWith("EnemyB"))
                return "Swordsman";
            return name;
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
