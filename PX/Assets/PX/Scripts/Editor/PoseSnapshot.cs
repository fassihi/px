using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PX.EditorTools
{
    /// <summary>
    /// Renders a character prefab in one frame of a clip, from the side and from a front three-quarter view, to PNGs.
    /// For checking poses by eye in edit mode: the clip is played through a PlayableGraph, so humanoid clips retarget
    /// (AnimationClip.SampleAnimation does not). Renders at 768 px; smaller side views make a stance look like a lean.
    /// </summary>
    public static class PoseSnapshot
    {
        private const int Size = 768;

        /// <summary>Renders <paramref name="clip"/> at <paramref name="time"/> seconds (a null clip renders the bind pose).
        /// Writes <paramref name="outputPrefix"/>_side.png and _front.png and returns the side view's path.</summary>
        public static string Render(string prefabPath, AnimationClip clip, float time, string outputPrefix, float distance = 7f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var character = Object.Instantiate(prefab, new Vector3(1000f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
            foreach (MonoBehaviour behaviour in character.GetComponentsInChildren<MonoBehaviour>())
                behaviour.enabled = false;
            Animator animator = character.GetComponentInChildren<Animator>();

            var graph = PlayableGraph.Create("PoseSnapshot");
            if (clip != null)
            {
                var playable = AnimationClipPlayable.Create(graph, clip);
                AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);
                playable.SetTime(time);
                graph.Evaluate(0f);
            }

            var cameraObject = new GameObject("PoseSnapshot Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.55f, 0.58f);
            var lightObject = new GameObject("PoseSnapshot Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            lightObject.transform.rotation = Quaternion.Euler(40f, 30f, 0f);

            var target = new RenderTexture(Size, Size, 24);
            camera.targetTexture = target;
            Vector3 focus = character.transform.position + Vector3.up * 1.1f;
            string side = Shoot(camera, target, focus, Vector3.back, distance, outputPrefix + "_side.png");
            Shoot(camera, target, focus, new Vector3(0.7f, 0f, -0.7f).normalized, distance, outputPrefix + "_front.png");

            graph.Destroy();
            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(lightObject);
            Object.DestroyImmediate(character);
            return side;
        }

        private static string Shoot(Camera camera, RenderTexture target, Vector3 focus, Vector3 from, float distance, string path)
        {
            camera.transform.position = focus + from * distance;
            camera.transform.LookAt(focus);
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return path;
        }
    }
}
