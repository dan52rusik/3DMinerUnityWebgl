using UnityEngine;

namespace SimpleVoxelSystem
{
    [DisallowMultipleComponent]
    public class BlockyMixCharacter : MonoBehaviour
    {
        [Header("Build")]
        public bool hideBaseRenderer = true;
        public bool rebuildOnAwake = true;
        public bool autoAddAnimator = true;
        public string visualRootName = "BlockyMixVisual";

        [Header("Palette")]
        public Color skinColor = new Color(0.96f, 0.80f, 0.64f, 1f);
        public Color shirtColor = new Color(0.95f, 0.58f, 0.16f, 1f);   // miner jacket
        public Color pantsColor = new Color(0.20f, 0.24f, 0.30f, 1f);   // dark work pants
        public Color accentColor = new Color(0.98f, 0.84f, 0.16f, 1f);  // helmet/lamp
        public Color bootColor = new Color(0.10f, 0.10f, 0.12f, 1f);
        public Color gloveColor = new Color(0.26f, 0.26f, 0.30f, 1f);
        public Color stripeColor = new Color(0.96f, 0.96f, 0.90f, 1f);

        [Header("Scale")]
        [Min(0.5f)] public float overallScale = 1.1f;
        public bool fitToCharacterControllerHeight = true;
        [Range(-0.2f, 0.3f)] public float visualYOffset = 0.06f;

        private Transform visualRoot;
        private Material skinMat;
        private Material shirtMat;
        private Material pantsMat;
        private Material accentMat;
        private Material bootMat;
        private Material gloveMat;
        private Material stripeMat;

        private void Awake()
        {
            if (rebuildOnAwake)
                Rebuild();
        }

        public void Rebuild()
        {
            EnsureVisualRoot();
            ClearVisualChildren();
            EnsureMaterials();

            if (hideBaseRenderer)
            {
                MeshRenderer baseMr = GetComponent<MeshRenderer>();
                if (baseMr != null)
                    baseMr.enabled = false;
            }

            float s = Mathf.Max(0.5f, overallScale);
            if (fitToCharacterControllerHeight)
            {
                CharacterController cc = GetComponent<CharacterController>();
                if (cc != null && cc.height > 0.1f)
                {
                    // Base generated model height is around 1.56 units.
                    float fit = (cc.height * 0.92f) / 1.56f;
                    s *= Mathf.Clamp(fit, 0.7f, 2.0f);
                }
            }

            // Legs
            CreateCube("Leg_L", new Vector3(-0.15f, -0.67f, 0f) * s, new Vector3(0.24f, 0.66f, 0.24f) * s, pantsMat);
            CreateCube("Leg_R", new Vector3(0.15f, -0.67f, 0f) * s, new Vector3(0.24f, 0.66f, 0.24f) * s, pantsMat);
            CreateCube("Boot_L", new Vector3(-0.15f, -1.02f, 0.03f) * s, new Vector3(0.27f, 0.10f, 0.30f) * s, bootMat);
            CreateCube("Boot_R", new Vector3(0.15f, -1.02f, 0.03f) * s, new Vector3(0.27f, 0.10f, 0.30f) * s, bootMat);

            // Torso
            CreateCube("Torso", new Vector3(0f, -0.10f, 0f) * s, new Vector3(0.54f, 0.76f, 0.28f) * s, shirtMat);
            CreateCube("Belt", new Vector3(0f, -0.42f, 0.01f) * s, new Vector3(0.56f, 0.08f, 0.30f) * s, pantsMat);
            CreateCube("VestStripe_H", new Vector3(0f, -0.05f, 0.145f) * s, new Vector3(0.52f, 0.05f, 0.02f) * s, stripeMat);
            CreateCube("VestStripe_VL", new Vector3(-0.11f, 0.03f, 0.145f) * s, new Vector3(0.05f, 0.24f, 0.02f) * s, stripeMat);
            CreateCube("VestStripe_VR", new Vector3(0.11f, 0.03f, 0.145f) * s, new Vector3(0.05f, 0.24f, 0.02f) * s, stripeMat);

            // Arms
            CreateCube("Arm_L", new Vector3(-0.39f, -0.12f, 0f) * s, new Vector3(0.18f, 0.64f, 0.18f) * s, shirtMat);
            CreateCube("Arm_R", new Vector3(0.39f, -0.12f, 0f) * s, new Vector3(0.18f, 0.64f, 0.18f) * s, shirtMat);
            CreateCube("Hand_L", new Vector3(-0.39f, -0.46f, 0f) * s, new Vector3(0.19f, 0.12f, 0.19f) * s, gloveMat);
            CreateCube("Hand_R", new Vector3(0.39f, -0.46f, 0f) * s, new Vector3(0.19f, 0.12f, 0.19f) * s, gloveMat);

            // Head
            CreateCube("Head", new Vector3(0f, 0.50f, 0f) * s, new Vector3(0.44f, 0.44f, 0.44f) * s, skinMat);
            CreateCube("HelmetTop", new Vector3(0f, 0.74f, 0f) * s, new Vector3(0.52f, 0.12f, 0.52f) * s, accentMat);
            CreateCube("HelmetBand", new Vector3(0f, 0.64f, 0f) * s, new Vector3(0.54f, 0.08f, 0.54f) * s, accentMat);
            CreateCube("HelmetBrim", new Vector3(0f, 0.59f, 0.22f) * s, new Vector3(0.40f, 0.03f, 0.10f) * s, accentMat);
            CreateCube("HeadLampBase", new Vector3(0f, 0.62f, 0.27f) * s, new Vector3(0.10f, 0.07f, 0.05f) * s, bootMat);
            CreateCube("HeadLampGlow", new Vector3(0f, 0.62f, 0.30f) * s, new Vector3(0.07f, 0.05f, 0.02f) * s, stripeMat);
            CreateCube("Eye_L", new Vector3(-0.08f, 0.53f, 0.22f) * s, new Vector3(0.06f, 0.05f, 0.02f) * s, accentMat);
            CreateCube("Eye_R", new Vector3(0.08f, 0.53f, 0.22f) * s, new Vector3(0.06f, 0.05f, 0.02f) * s, accentMat);
            CreateCube("Beard", new Vector3(0f, 0.40f, 0.19f) * s, new Vector3(0.24f, 0.11f, 0.03f) * s, pantsMat);

            // Small triangular shoulder wedges for "mix" style.
            GameObject wedgeL = CreateWedge("ShoulderWedge_L", new Vector3(-0.29f, 0.24f, 0f) * s, new Vector3(0.11f, 0.11f, 0.20f) * s, true, shirtMat);
            if (wedgeL != null)
                wedgeL.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // user-requested flip

            GameObject wedgeR = CreateWedge("ShoulderWedge_R", new Vector3(0.29f, 0.24f, 0f) * s, new Vector3(0.11f, 0.11f, 0.20f) * s, false, shirtMat);
            if (wedgeR != null)
                wedgeR.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // Hand pickaxe (right hand)
            GameObject pickaxeRoot = new GameObject("HandPickaxe");
            pickaxeRoot.transform.SetParent(visualRoot, false);
            pickaxeRoot.transform.localPosition = new Vector3(0.52f, -0.30f, 0.10f) * s;
            pickaxeRoot.transform.localRotation = Quaternion.Euler(18f, 26f, -14f);

            GameObject pickaxeHandle = CreateCube("PickaxeHandle", Vector3.zero, new Vector3(0.05f, 0.62f, 0.05f) * s, accentMat);
            if (pickaxeHandle != null)
            {
                pickaxeHandle.transform.SetParent(pickaxeRoot.transform, false);
                pickaxeHandle.transform.localPosition = Vector3.zero;
            }

            GameObject pickaxeHead = CreateCube("PickaxeHead", new Vector3(0f, 0.28f, 0f) * s, new Vector3(0.30f, 0.08f, 0.08f) * s, bootMat);
            if (pickaxeHead != null)
            {
                pickaxeHead.transform.SetParent(pickaxeRoot.transform, false);
                pickaxeHead.transform.localPosition = new Vector3(0f, 0.28f, 0f) * s;
            }

            if (autoAddAnimator)
            {
                BlockyMixAnimator animator = GetComponent<BlockyMixAnimator>();
                if (animator == null)
                    animator = gameObject.AddComponent<BlockyMixAnimator>();
                animator.RebindNow();
            }

            if (visualRoot != null)
                visualRoot.localPosition = new Vector3(0f, visualYOffset, 0f);
        }

        public void ClearVisual()
        {
            Transform root = transform.Find(visualRootName);
            if (root == null)
                return;

            if (Application.isPlaying)
                Destroy(root.gameObject);
            else
                DestroyImmediate(root.gameObject);
        }

        private void EnsureVisualRoot()
        {
            Transform existing = transform.Find(visualRootName);
            if (existing != null)
            {
                visualRoot = existing;
                return;
            }

            GameObject root = new GameObject(visualRootName);
            root.transform.SetParent(transform, false);
            visualRoot = root.transform;
        }

        private void ClearVisualChildren()
        {
            if (visualRoot == null)
                return;

            for (int i = visualRoot.childCount - 1; i >= 0; i--)
            {
                Transform ch = visualRoot.GetChild(i);
                if (Application.isPlaying)
                    Destroy(ch.gameObject);
                else
                    DestroyImmediate(ch.gameObject);
            }
        }

        private void EnsureMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            skinMat = NewMat(shader, skinColor);
            shirtMat = NewMat(shader, shirtColor);
            pantsMat = NewMat(shader, pantsColor);
            accentMat = NewMat(shader, accentColor);
            bootMat = NewMat(shader, bootColor);
            gloveMat = NewMat(shader, gloveColor);
            stripeMat = NewMat(shader, stripeColor);
        }

        private static Material NewMat(Shader shader, Color color)
        {
            Material mat = new Material(shader);
            mat.color = color;
            return mat;
        }

        private GameObject CreateCube(string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(visualRoot, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            Collider c = go.GetComponent<Collider>();
            if (c != null)
            {
                if (Application.isPlaying) Destroy(c);
                else DestroyImmediate(c);
            }

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.sharedMaterial = mat;

            return go;
        }

        private GameObject CreateWedge(string name, Vector3 localPos, Vector3 scale, bool left, Material mat)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(visualRoot, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            Mesh mesh = new Mesh();
            mf.sharedMesh = mesh;
            mesh.name = name + "_Mesh";

            float sx = scale.x * 0.5f;
            float sy = scale.y * 0.5f;
            float sz = scale.z * 0.5f;

            // Triangular prism (simple wedge)
            Vector3[] v = new Vector3[]
            {
                new Vector3(-sx, -sy, -sz),
                new Vector3( sx, -sy, -sz),
                new Vector3(-sx,  sy, -sz),
                new Vector3(-sx, -sy,  sz),
                new Vector3( sx, -sy,  sz),
                new Vector3(-sx,  sy,  sz),
            };

            int[] t = new int[]
            {
                0,2,1, 3,4,5,   // front/back
                0,1,4, 0,4,3,   // bottom
                0,3,5, 0,5,2,   // left rectangle
                2,5,4, 2,4,1    // slope
            };

            if (!left)
            {
                for (int i = 0; i < v.Length; i++)
                    v[i].x = -v[i].x;
                // Mirroring flips handedness; keep normals/front-faces correct.
                for (int i = 0; i < t.Length; i += 3)
                {
                    int tmp = t[i + 1];
                    t[i + 1] = t[i + 2];
                    t[i + 2] = tmp;
                }
            }

            mesh.vertices = v;
            mesh.triangles = t;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return go;
        }
    }
}
