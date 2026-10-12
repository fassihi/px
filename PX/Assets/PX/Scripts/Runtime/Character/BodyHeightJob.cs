using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace PX
{
    /// <summary>
    /// Lowers her body while keeping her feet where the clip put them: the legs bend to make up the difference.
    /// It holds her down to a ceiling, so a move made from a higher stance, like a Mixamo sweep, still plays from her
    /// crouch, and it adds a dip on top of any pose, as for a landing: an additive layer done procedurally.
    /// Runs on the pose after the mixer. Heights are in the stream's own body space, so the ceiling is measured
    /// from a pose (see <see cref="measure"/>) rather than given in metres.
    /// </summary>
    public struct BodyHeightJob : IAnimationJob
    {
        /// <summary>How much of the cap applies, 0 to 1. Faded with the pose that needs it.</summary>
        public float weight;

        /// <summary>The highest the body may be.</summary>
        public float ceiling;

        /// <summary>How far to lower the body regardless of the ceiling, as for a landing. Stream units.</summary>
        public float dip;

        /// <summary>How far the neck and head are tipped up, in muscle units (-1 to 1 across their range). Already scaled by its weight.</summary>
        public float headLift;

        /// <summary>When true, the job writes this frame's body height to <see cref="measured"/> and changes nothing.</summary>
        public bool measure;

        public NativeArray<float> measured;

        public void ProcessRootMotion(AnimationStream stream)
        {
        }

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!stream.isHumanStream)
                return;

            AnimationHumanStream human = stream.AsHuman();
            Vector3 body = human.bodyPosition;
            if (measure)
            {
                measured[0] = body.y;
                return;
            }

            if (headLift != 0f)
            {
                var neck = new MuscleHandle(HeadDof.NeckFrontBack);
                var head = new MuscleHandle(HeadDof.HeadFrontBack);
                human.SetMuscle(neck, Mathf.Clamp(human.GetMuscle(neck) + headLift * 0.4f, -1f, 1f));
                human.SetMuscle(head, Mathf.Clamp(human.GetMuscle(head) + headLift * 0.6f, -1f, 1f));
            }

            float drop = Mathf.Max((body.y - ceiling) * Mathf.Clamp01(weight), dip);
            if (drop <= 0f)
                return;

            // Pin the feet first, then lower the body and let the legs bend to reach them.
            Vector3 leftFoot = human.GetGoalPosition(AvatarIKGoal.LeftFoot);
            Quaternion leftFootRotation = human.GetGoalRotation(AvatarIKGoal.LeftFoot);
            Vector3 rightFoot = human.GetGoalPosition(AvatarIKGoal.RightFoot);
            Quaternion rightFootRotation = human.GetGoalRotation(AvatarIKGoal.RightFoot);

            human.bodyPosition = body - new Vector3(0f, drop, 0f);

            human.SetGoalPosition(AvatarIKGoal.LeftFoot, leftFoot);
            human.SetGoalRotation(AvatarIKGoal.LeftFoot, leftFootRotation);
            human.SetGoalWeightPosition(AvatarIKGoal.LeftFoot, 1f);
            human.SetGoalWeightRotation(AvatarIKGoal.LeftFoot, 1f);
            human.SetGoalPosition(AvatarIKGoal.RightFoot, rightFoot);
            human.SetGoalRotation(AvatarIKGoal.RightFoot, rightFootRotation);
            human.SetGoalWeightPosition(AvatarIKGoal.RightFoot, 1f);
            human.SetGoalWeightRotation(AvatarIKGoal.RightFoot, 1f);
            human.SolveIK();
        }
    }
}
