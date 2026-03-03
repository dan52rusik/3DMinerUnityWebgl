using UnityEngine;

namespace SimpleVoxelSystem
{
    [DisallowMultipleComponent]
    public class BlockyMixAnimator : MonoBehaviour
    {
        [Header("Walk")]
        public float walkSwingAngle = 28f;
        public float walkCycleSpeed = 8f;
        public float bodyBobAmount = 0.02f;
        public float bodyTiltAmount = 3f;
        public float maxSpeedForAnim = 7f;

        [Header("Turn")]
        public float maxTurnSpeedForAnim = 260f; // deg/sec
        public float turnLeanAngle = 8f;         // side lean while turning
        public float turnTwistAngle = 12f;       // torso yaw twist while turning

        [Header("Mining")]
        public float mineSwingAngle = 75f;
        public float mineSwingDuration = 0.2f;
        public float mineRecoverSpeed = 14f;

        private CharacterController controller;
        private Vector3 lastPos;

        private Transform visualRoot;
        private Transform torso;
        private Transform head;
        private Transform armL;
        private Transform armR;
        private Transform legL;
        private Transform legR;
        private Transform bootL;
        private Transform bootR;
        private Transform handL;
        private Transform handR;
        private Transform pickaxeRoot;

        private Transform torsoPivot;
        private Transform headPivot;
        private Transform armLPivot;
        private Transform armRPivot;
        private Transform legLPivot;
        private Transform legRPivot;

        private Quaternion torsoBaseRot;
        private Quaternion headBaseRot;
        private Quaternion armLBaseRot;
        private Quaternion armRBaseRot;
        private Quaternion legLBaseRot;
        private Quaternion legRBaseRot;

        private Vector3 torsoBasePos;

        private float walkPhase;
        private float mineTimer;
        private float lastYaw;
        private bool bound;

        private void OnEnable()
        {
            PlayerPickaxe.OnMineAttempt += OnMineAttempt;
            RebindNow();
        }

        private void OnDisable()
        {
            PlayerPickaxe.OnMineAttempt -= OnMineAttempt;
        }

        public void RebindNow()
        {
            controller = GetComponent<CharacterController>();
            visualRoot = transform.Find("BlockyMixVisual");
            if (visualRoot == null)
            {
                bound = false;
                return;
            }

            torso = FindIn(visualRoot, "Torso");
            head = FindIn(visualRoot, "Head");
            armL = FindIn(visualRoot, "Arm_L");
            armR = FindIn(visualRoot, "Arm_R");
            legL = FindIn(visualRoot, "Leg_L");
            legR = FindIn(visualRoot, "Leg_R");
            bootL = FindIn(visualRoot, "Boot_L");
            bootR = FindIn(visualRoot, "Boot_R");
            handL = FindIn(visualRoot, "Hand_L");
            handR = FindIn(visualRoot, "Hand_R");
            pickaxeRoot = FindIn(visualRoot, "HandPickaxe");

            if (torso == null || armL == null || armR == null || legL == null || legR == null)
            {
                bound = false;
                return;
            }

            // Create simple rig pivots (bones) and parent parts to them.
            torsoPivot = CreatePivotForPart(torso, "Rig_TorsoPivot", 0.5f);
            headPivot = head != null ? CreatePivotForPart(head, "Rig_HeadPivot", 0.5f) : null;
            armLPivot = CreatePivotForPart(armL, "Rig_ArmLPivot", 1f);
            armRPivot = CreatePivotForPart(armR, "Rig_ArmRPivot", 1f);
            legLPivot = CreatePivotForPart(legL, "Rig_LegLPivot", 1f);
            legRPivot = CreatePivotForPart(legR, "Rig_LegRPivot", 1f);

            // Keep hand and pickaxe on right arm for mine swing.
            if (handL != null && armL != null)
                handL.SetParent(armL, true);
            if (handR != null && armR != null)
                handR.SetParent(armR, true);
            if (bootL != null && legL != null)
                bootL.SetParent(legL, true);
            if (bootR != null && legR != null)
                bootR.SetParent(legR, true);
            if (pickaxeRoot != null && armR != null)
                pickaxeRoot.SetParent(armR, true);

            torsoBaseRot = torsoPivot.localRotation;
            armLBaseRot = armLPivot.localRotation;
            armRBaseRot = armRPivot.localRotation;
            legLBaseRot = legLPivot.localRotation;
            legRBaseRot = legRPivot.localRotation;
            headBaseRot = headPivot != null ? headPivot.localRotation : Quaternion.identity;
            torsoBasePos = torsoPivot.localPosition;

            lastPos = transform.position;
            lastYaw = transform.eulerAngles.y;
            bound = true;
        }

        private void Update()
        {
            if (!bound || !IsRigValid())
            {
                RebindNow();
                if (!bound || !IsRigValid())
                    return;
            }

            float speed = GetHorizontalSpeed();
            float speed01 = Mathf.Clamp01(speed / Mathf.Max(0.01f, maxSpeedForAnim));
            walkPhase += Time.deltaTime * walkCycleSpeed * Mathf.Lerp(0.25f, 1.2f, speed01);

            float walkSin = Mathf.Sin(walkPhase);
            float walkArm = walkSin * walkSwingAngle * speed01;
            float walkLeg = -walkSin * walkSwingAngle * speed01;
            float bob = Mathf.Abs(Mathf.Sin(walkPhase * 0.5f)) * bodyBobAmount * speed01;
            float tilt = walkSin * bodyTiltAmount * speed01;

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float yawNow = transform.eulerAngles.y;
            float yawDelta = Mathf.DeltaAngle(lastYaw, yawNow);
            lastYaw = yawNow;
            float turnSpeed = yawDelta / dt;
            float turn01 = Mathf.Clamp(turnSpeed / Mathf.Max(1f, maxTurnSpeedForAnim), -1f, 1f);
            float turnLean = -turn01 * turnLeanAngle;
            float turnTwist = turn01 * turnTwistAngle;

            float mineWeight = 0f;
            if (mineTimer > 0f)
            {
                mineTimer -= Time.deltaTime;
                float t = 1f - Mathf.Clamp01(mineTimer / Mathf.Max(0.01f, mineSwingDuration));
                // sharp attack + smooth release
                mineWeight = t < 0.55f ? Mathf.SmoothStep(0f, 1f, t / 0.55f) : Mathf.SmoothStep(1f, 0f, (t - 0.55f) / 0.45f);
            }

            float mineX = -mineSwingAngle * mineWeight;

            torsoPivot.localPosition = torsoBasePos + new Vector3(0f, bob, 0f);
            torsoPivot.localRotation = torsoBaseRot * Quaternion.Euler(0f, turnTwist, tilt + turnLean);

            if (headPivot != null)
                headPivot.localRotation = Quaternion.Slerp(
                    headPivot.localRotation,
                    headBaseRot * Quaternion.Euler(-tilt * 0.35f, -turnTwist * 0.35f, -turnLean * 0.2f),
                    1f - Mathf.Exp(-8f * Time.deltaTime));

            armLPivot.localRotation = armLBaseRot * Quaternion.Euler(walkArm, 0f, 0f);

            Quaternion armRWalk = armRBaseRot * Quaternion.Euler(-walkArm, 0f, 0f);
            Quaternion armRMine = armRBaseRot * Quaternion.Euler(mineX, 0f, -10f * mineWeight);
            Quaternion armRTarget = mineWeight > 0.001f ? armRMine : armRWalk;
            armRPivot.localRotation = Quaternion.Slerp(armRPivot.localRotation, armRTarget, 1f - Mathf.Exp(-mineRecoverSpeed * Time.deltaTime));

            legLPivot.localRotation = legLBaseRot * Quaternion.Euler(walkLeg, 0f, 0f);
            legRPivot.localRotation = legRBaseRot * Quaternion.Euler(-walkLeg, 0f, 0f);
        }

        private bool IsRigValid()
        {
            return visualRoot != null &&
                   torsoPivot != null &&
                   armLPivot != null &&
                   armRPivot != null &&
                   legLPivot != null &&
                   legRPivot != null;
        }

        private void OnMineAttempt()
        {
            if (!isActiveAndEnabled)
                return;
            mineTimer = Mathf.Max(mineTimer, mineSwingDuration);
        }

        private float GetHorizontalSpeed()
        {
            Vector3 v;
            if (controller != null)
                v = controller.velocity;
            else
            {
                v = (transform.position - lastPos) / Mathf.Max(Time.deltaTime, 0.0001f);
                lastPos = transform.position;
            }
            v.y = 0f;
            return v.magnitude;
        }

        private static Transform FindIn(Transform root, string name)
        {
            if (root == null)
                return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform ch = root.GetChild(i);
                if (ch.name == name)
                    return ch;
                Transform found = FindIn(ch, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private Transform CreatePivotForPart(Transform part, string pivotName, float upFactor)
        {
            if (part == null)
                return null;

            Transform existing = visualRoot != null ? visualRoot.Find(pivotName) : null;
            if (existing != null)
            {
                if (part.parent != existing)
                    part.SetParent(existing, true);
                return existing;
            }

            MeshRenderer mr = part.GetComponent<MeshRenderer>();
            Vector3 worldPos = part.position;
            if (mr != null)
            {
                Bounds b = mr.bounds;
                worldPos = new Vector3(b.center.x, b.center.y + b.extents.y * upFactor, b.center.z);
            }

            GameObject pivotGo = new GameObject(pivotName);
            Transform pivot = pivotGo.transform;
            pivot.SetParent(visualRoot, true);
            pivot.position = worldPos;
            pivot.rotation = part.rotation;
            pivot.localScale = Vector3.one;

            part.SetParent(pivot, true);
            return pivot;
        }
    }
}
