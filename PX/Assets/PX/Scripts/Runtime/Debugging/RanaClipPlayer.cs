using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace PX
{
    /// <summary>
    /// Graybox stand-in for the Rana viewer scene: press 1-5 to play a clip on her, to check the rig and skinning.
    /// </summary>
    public sealed class RanaClipPlayer : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationClip[] clips;

        private PlayableGraph graph;

        private void Start() => Play(0);

        private void OnDestroy()
        {
            if (graph.IsValid())
                graph.Destroy();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            for (int i = 0; i < clips.Length && i < 9; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                    Play(i);
            }
        }

        private void Play(int index)
        {
            if (index >= clips.Length || clips[index] == null)
                return;

            if (graph.IsValid())
                graph.Destroy();
            graph = PlayableGraph.Create("Rana");
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Rana", animator);
            output.SetSourcePlayable(AnimationClipPlayable.Create(graph, clips[index]));
            graph.Play();
        }
    }
}
