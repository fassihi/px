using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace PX
{
    /// <summary>
    /// Graybox stand-in for the Rana viewer scene: lists her animations on screen so each one can be played to
    /// check the rig and skinning. Click a clip or press 1-9; Up/Down steps through them, Space pauses.
    /// Playback speed follows Time.timeScale, set by <see cref="RanaOrbitView"/>.
    /// </summary>
    public sealed class RanaClipPlayer : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationClip[] clips;
        [SerializeField, Tooltip("What to show for each clip. Falls back to the clip's name.")]
        private string[] labels;

        private PlayableGraph graph;
        private AnimationClipPlayable playable;
        private int index = -1;
        private double time;
        private bool loop = true;
        private bool paused;

        private void Start() => Play(0);

        private void OnDestroy()
        {
            if (graph.IsValid())
                graph.Destroy();
        }

        private void Update()
        {
            HandleKeys();

            if (!graph.IsValid() || clips[index] == null)
                return;

            double length = clips[index].length;
            if (!paused)
                time += Time.deltaTime; // follows Time.timeScale, which the panel on the right sets
            time = loop && length > 0.0 ? time % length : System.Math.Min(time, length);
            playable.SetTime(time);
        }

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            for (int i = 0; i < clips.Length && i < 9; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                    Play(i);
            }

            if (keyboard.downArrowKey.wasPressedThisFrame)
                Play((index + 1) % clips.Length);
            if (keyboard.upArrowKey.wasPressedThisFrame)
                Play((index + clips.Length - 1) % clips.Length);
            if (keyboard.spaceKey.wasPressedThisFrame)
                paused = !paused;
        }

        private void Play(int newIndex)
        {
            if (newIndex < 0 || newIndex >= clips.Length || clips[newIndex] == null)
                return;

            if (graph.IsValid())
                graph.Destroy();
            graph = PlayableGraph.Create("Rana");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime); // time is set by hand in Update, but the graph must still evaluate
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Rana", animator);
            playable = AnimationClipPlayable.Create(graph, clips[newIndex]);
            playable.SetSpeed(0.0);
            output.SetSourcePlayable(playable);
            graph.Play();

            index = newIndex;
            time = 0.0;
        }

        private string LabelFor(int i)
        {
            string label = labels != null && i < labels.Length && !string.IsNullOrEmpty(labels[i]) ? labels[i] : clips[i].name;
            return $"{(i < 9 ? (i + 1).ToString() : "-")}  {label}";
        }

        private void OnGUI()
        {
            float scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            const float width = 260f;
            const float row = 24f;
            GUILayout.BeginArea(new Rect(12f, 12f, width, Screen.height / scale - 24f), GUI.skin.box);
            GUILayout.Label("Rana animations");
            for (int i = 0; i < clips.Length; i++)
            {
                bool selected = i == index;
                if (GUILayout.Toggle(selected, LabelFor(i), GUI.skin.button, GUILayout.Height(row)) && !selected)
                    Play(i);
            }

            GUILayout.Space(8f);
            loop = GUILayout.Toggle(loop, "Loop");
            paused = GUILayout.Toggle(paused, "Paused (Space)");
            if (index >= 0)
                GUILayout.Label($"{time:0.00} / {clips[index].length:0.00} s");
            GUILayout.EndArea();
        }
    }
}
