#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PX.Tests
{
    /// <summary>The Rana viewer scene is not part of the build, so it is loaded by path.</summary>
    public sealed class RanaViewerTests
    {
        private const string ScenePath = "Assets/PX/Scenes/RanaViewer.unity";

        [UnityTest]
        public IEnumerator TheSelectedClip_ActuallyPlaysOnHer()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var rana = GameObject.Find("Rana").GetComponent<Animator>();
            Transform hand = rana.GetBoneTransform(HumanBodyBones.RightHand);
            Assert.That(hand, Is.Not.Null, "Rana has no humanoid right hand bone.");

            // The viewer starts on its first clip (a boxing stance), whose hands move.
            yield return new WaitForSeconds(0.3f);
            Vector3 first = rana.transform.InverseTransformPoint(hand.position);
            yield return new WaitForSeconds(0.5f);
            Vector3 second = rana.transform.InverseTransformPoint(hand.position);

            Assert.That((second - first).magnitude, Is.GreaterThan(0.01f), "Her hand does not move: the clip is not playing in the viewer.");
        }
    }
}
#endif
