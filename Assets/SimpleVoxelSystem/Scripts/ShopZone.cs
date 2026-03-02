using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimpleVoxelSystem
{
    public enum ShopZoneType { Mine, Pickaxe, Sell }

    /// <summary>
    /// ÐÐµÐ²Ð¸Ð´Ð¸Ð¼Ñ‹Ð¹ Ñ‚Ñ€Ð¸Ð³Ð³ÐµÑ€-ÐºÑƒÐ± Ð·Ð¾Ð½Ñ‹ Ð¼Ð°Ð³Ð°Ð·Ð¸Ð½Ð°.
    /// Ð¡Ð¾Ð·Ð´Ð°Ñ‘Ñ‚ÑÑ Ð°Ð²Ñ‚Ð¾Ð¼Ð°Ñ‚Ð¸Ñ‡ÐµÑÐºÐ¸ LobbyEditor Ð¿Ñ€Ð¸ Ð²Ñ‹Ð±Ð¾Ñ€Ðµ Ð¸Ð½ÑÑ‚Ñ€ÑƒÐ¼ÐµÐ½Ñ‚Ð° Â«ðŸ›’ Ð—Ð¾Ð½Ð° Ð¼Ð°Ð³Ð°Ð·Ð¸Ð½Ð°Â».
    /// </summary>
    [AddComponentMenu("SimpleVoxelSystem/Shop Zone")]
    public class ShopZone : MonoBehaviour
    {
        [Header("Ð¢Ð¸Ð¿ Ð¼Ð°Ð³Ð°Ð·Ð¸Ð½Ð°")]
        public ShopZoneType zoneType = ShopZoneType.Mine;

        [Header("Ð Ð°Ð·Ð¼ÐµÑ€ Ð·Ð¾Ð½Ñ‹ (Ð² Ð±Ð»Ð¾ÐºÐ°Ñ…)")]
        public int sizeX = 3;
        public int sizeY = 3;
        public int sizeZ = 3;

        [Header("ÐšÐ»Ð°Ð²Ð¸ÑˆÐ°")]
        public KeyCode openKey = KeyCode.B;

        // â”€â”€â”€ Runtime â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private bool          playerInside;
        private MineShopUI    mineShopUI;
        private PickaxeShopUI pickaxeShopUI;
        private PlayerPickaxe playerPickaxe;
        private GameObject    editorVisual;   // полупрозрачный куб в режиме редактора
        private GameObject    gameplayMarker; // visible marker for sell point in normal gameplay
        private Material      visualMat;

        private static readonly Color ColNormal = new Color(0.20f, 0.55f, 1.00f, 0.28f);
        private static readonly Color ColDelete = new Color(1.00f, 0.20f, 0.20f, 0.42f);

        // ÐžÐ´Ð¸Ð½ Ð¿Ñ€Ð¾Ð¼Ð¿Ñ‚ Ð½Ð° Ð²ÑÑŽ ÑÑ†ÐµÐ½Ñƒ
        private static GameObject promptPanel;
        private static Text       promptText;
        private static ShopZone   currentZone;


        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Unity
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        void Start()
        {
            mineShopUI    = FindFirstObjectByType<MineShopUI>();
            pickaxeShopUI = FindFirstObjectByType<PickaxeShopUI>();
            playerPickaxe = FindFirstObjectByType<PlayerPickaxe>();
            EnsurePromptUI();

            // BoxCollider
            var col = GetComponent<BoxCollider>();
            if (col == null) col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size   = new Vector3(sizeX, sizeY, sizeZ);
            col.center = new Vector3(0f, sizeY * 0.5f - 0.5f, 0f);

            // Rigidbody kinematic
            var rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;

            // Ð’Ð¸Ð·ÑƒÐ°Ð»ÑŒÐ½Ñ‹Ð¹ ÐºÑƒÐ± Ð´Ð»Ñ Ñ€ÐµÐ¶Ð¸Ð¼Ð° Ñ€ÐµÐ´Ð°ÐºÑ‚Ð¾Ñ€Ð°
            CreateEditorVisual();
            CreateGameplayMarker();
            SetEditorVisible(false);  // Ð½Ð°Ñ‡Ð°Ð»ÑŒÐ½Ð¾ ÑÐºÑ€Ñ‹Ñ‚ Ð² Ð¸Ð³Ñ€Ðµ
        }

        // Ð’ÐºÐ»ÑŽÑ‡Ð¸Ñ‚ÑŒ/Ð²Ñ‹ÐºÐ»ÑŽÑ‡Ð¸Ñ‚ÑŒ Ð²Ð¸Ð·ÑƒÐ°Ð»ÑŒÐ½Ñ‹Ð¹ ÐºÑƒÐ± Ñ€ÐµÐ´Ð°ÐºÑ‚Ð¾Ñ€Ð°
        public void SetEditorVisible(bool visible)
        {
            if (editorVisual != null) editorVisual.SetActive(visible);
        }

        // ÐŸÐ¾Ð´ÑÐ²ÐµÑ‚Ð¸Ñ‚ÑŒ Ð·Ð¾Ð½Ñƒ ÐºÑ€Ð°ÑÐ½Ñ‹Ð¼ Ð¿Ñ€Ð¸ hover ÑÐ´Ð¸Ñ‚Ð¾Ñ€Ð° (ÑƒÐ´Ð°Ð»ÐµÐ½Ð¸Ðµ)
        public void SetDeleteHover(bool hovered)
        {
            if (visualMat != null)
                visualMat.color = hovered ? ColDelete : ColNormal;
        }

        void CreateEditorVisual()
        {
            editorVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            editorVisual.name = "ShopZoneVisual";
            editorVisual.transform.SetParent(transform, false);

            // Ð Ð°Ð·Ð¼ÐµÑ€ Ð¸ Ñ†ÐµÐ½Ñ‚Ñ€ ÑÐ¾Ð²Ð¿Ð°Ð´Ð°ÑŽÑ‚ Ñ BoxCollider
            editorVisual.transform.localScale  = new Vector3(sizeX, sizeY, sizeZ);
            editorVisual.transform.localPosition = new Vector3(0f, sizeY * 0.5f - 0.5f, 0f);

            // ÐšÐ¾Ð»Ð»Ð°Ð¹Ð´ÐµÑ€ Ð²Ð¸Ð·ÑƒÐ°Ð»ÑŒÐ½Ð¾Ð³Ð¾ ÐºÑƒÐ±Ð° Ð½Ðµ Ð½ÑƒÐ¶ÐµÐ½
            Destroy(editorVisual.GetComponent<Collider>());

            var mr = editorVisual.GetComponent<MeshRenderer>();
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            visualMat = new Material(sh);
            visualMat.color = ColNormal;
            if (visualMat.HasProperty("_Surface"))  visualMat.SetFloat("_Surface",  1f);
            if (visualMat.HasProperty("_SrcBlend")) visualMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (visualMat.HasProperty("_DstBlend")) visualMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (visualMat.HasProperty("_ZWrite"))   visualMat.SetFloat("_ZWrite",   0f);
            visualMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mr.material = visualMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        void CreateGameplayMarker()
        {
            // Keep mine/pickaxe zones invisible in game; show only sell point marker.
            if (zoneType != ShopZoneType.Sell)
                return;

            gameplayMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            gameplayMarker.name = "SellPointMarker";
            gameplayMarker.transform.SetParent(transform, false);
            gameplayMarker.transform.localScale = new Vector3(0.75f, 0.1f, 0.75f);
            gameplayMarker.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            Destroy(gameplayMarker.GetComponent<Collider>());

            var mr = gameplayMarker.GetComponent<MeshRenderer>();
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh);
            mat.color = new Color(1.00f, 0.80f, 0.20f, 0.95f);
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        void Update()
        {
            // Ð Ð°Ð±Ð¾Ñ‚Ð°ÐµÑ‚ Ñ‚Ð¾Ð»ÑŒÐºÐ¾ ÐµÑÐ»Ð¸ Ð¸Ð³Ñ€Ð¾Ðº Ð²Ð½ÑƒÑ‚Ñ€Ð¸ Ð·Ð¾Ð½Ñ‹ Ð¼Ð°Ð³Ð°Ð·Ð¸Ð½Ð°
            if (!playerInside) return;
            if (!IsKeyPressed()) return;

            if (zoneType == ShopZoneType.Mine && mineShopUI != null)
                mineShopUI.TogglePanel();
            else if (zoneType == ShopZoneType.Pickaxe && pickaxeShopUI != null)
                pickaxeShopUI.Toggle();
            else if (zoneType == ShopZoneType.Sell && playerPickaxe != null)
                playerPickaxe.SellResources();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;
            playerInside = true;
            currentZone  = this;
            ShowPrompt(true);
        }

        void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;
            playerInside = false;
            if (currentZone == this)
            {
                currentZone = null;
                ShowPrompt(false);
                if (zoneType == ShopZoneType.Mine && mineShopUI != null)
                    mineShopUI.SetPanelVisible(false);
                else if (zoneType == ShopZoneType.Pickaxe && pickaxeShopUI != null)
                    pickaxeShopUI.SetPanelVisible(false);
            }
        }

        void OnDestroy()
        {
            if (currentZone == this) { currentZone = null; ShowPrompt(false); }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Helpers
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        static bool IsPlayer(Collider other)
        {
            // 1. Ð˜Ñ‰ÐµÐ¼ NetworkObject
            var no = other.GetComponentInParent<NetworkObject>();
            if (no != null)
            {
                // Ð•ÑÐ»Ð¸ ÑÑ‚Ð¾ ÑÐµÑ‚ÐµÐ²Ð¾Ð¹ Ð¾Ð±ÑŠÐµÐºÑ‚ â€” Ð¾Ð½ Ð´Ð¾Ð»Ð¶ÐµÐ½ Ð¿Ñ€Ð¸Ð½Ð°Ð´Ð»ÐµÐ¶Ð°Ñ‚ÑŒ Ð»Ð¾ÐºÐ°Ð»ÑŒÐ½Ð¾Ð¼Ñƒ Ð¸Ð³Ñ€Ð¾ÐºÑƒ
                return no.IsOwner && no.IsPlayerObject;
            }

            // 2. Ð•ÑÐ»Ð¸ ÑÐµÑ‚ÐµÐ²Ð¾Ð³Ð¾ Ð¾Ð±ÑŠÐµÐºÑ‚Ð° Ð½ÐµÑ‚ (Ð¾Ð´Ð¸Ð½Ð¾Ñ‡Ð½Ñ‹Ð¹ Ñ€ÐµÐ¶Ð¸Ð¼) â€” Ð¿Ñ€Ð¾Ð²ÐµÑ€ÑÐµÐ¼ Ñ‚ÐµÐ³/ÐºÐ¾Ð¼Ð¿Ð¾Ð½ÐµÐ½Ñ‚Ñ‹
            return other.CompareTag("Player")
                || other.GetComponentInParent<PlayerPickaxe>() != null
                || other.name.ToLower().Contains("player");
        }

        private char GetOpenKeyDisplay()
        {
            if (zoneType == ShopZoneType.Pickaxe) return 'P';
            if (zoneType == ShopZoneType.Sell) return 'R';
            return 'B';
        }

        bool IsKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            if (zoneType == ShopZoneType.Pickaxe) return kb.pKey.wasPressedThisFrame;
            if (zoneType == ShopZoneType.Sell) return kb.rKey.wasPressedThisFrame;
            return kb.bKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            KeyCode k = KeyCode.B;
            if (zoneType == ShopZoneType.Pickaxe) k = KeyCode.P;
            else if (zoneType == ShopZoneType.Sell) k = KeyCode.R;
            return Input.GetKeyDown(k);
#else
            return false;
#endif
        }

        static void ShowPrompt(bool v) 
        { 
            if (promptPanel != null) 
            {
                if (v && currentZone != null)
                {
                    string keyStr = currentZone.GetOpenKeyDisplay().ToString();
                    string shopName = "магазин шахт";
                    if (currentZone.zoneType == ShopZoneType.Pickaxe) shopName = "магазин кирок";
                    else if (currentZone.zoneType == ShopZoneType.Sell) shopName = "точку продажи";
                    promptText.text = $"Нажмите <color=#FFD700><b>[{keyStr}]</b></color> — открыть {shopName}";
                }
                promptPanel.SetActive(v); 
            } 
        }

        void EnsurePromptUI()
        {
            if (promptPanel != null) return;
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            promptPanel = new GameObject("ShopZonePrompt");
            promptPanel.transform.SetParent(canvas.transform, false);
            var rt = promptPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 60f);
            rt.sizeDelta = new Vector2(370f, 48f);

            var img = promptPanel.AddComponent<Image>();
            img.color = new Color(0.05f, 0.05f, 0.12f, 0.90f);

            var tGo = new GameObject("Label");
            tGo.transform.SetParent(promptPanel.transform, false);
            var trt = tGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;

            promptText = tGo.AddComponent<Text>();
            promptText.font      = RuntimeUiFont.Get();
            promptText.fontSize  = 16;
            promptText.alignment = TextAnchor.MiddleCenter;
            promptText.color     = Color.white;
            promptText.supportRichText = true;
            promptText.text = "Нажмите <color=#FFD700><b>[B]</b></color> — открыть магазин шахт";

            promptPanel.SetActive(false);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Gizmo (Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð² Editor)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        void OnDrawGizmos()
        {
            Vector3 center = transform.position + new Vector3(0f, sizeY * 0.5f - 0.5f, 0f);
            Vector3 size   = new Vector3(sizeX, sizeY, sizeZ);

            Gizmos.color = new Color(0.2f, 0.55f, 1.0f, 0.15f);
            Gizmos.DrawCube(center, size);

            Gizmos.color = new Color(0.2f, 0.55f, 1.0f, 0.85f);
            Gizmos.DrawWireCube(center, size);

#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.white;
            string keyStr = "B";
            string shopName = "Шахты";
            if (zoneType == ShopZoneType.Pickaxe) { keyStr = "P"; shopName = "Кирки"; }
            else if (zoneType == ShopZoneType.Sell) { keyStr = "R"; shopName = "Продажа"; }
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (sizeY + 0.4f),
                $"🛒 {shopName}  {sizeX}×{sizeY}×{sizeZ}  [{keyStr}]");
#endif
        }
    }
}

