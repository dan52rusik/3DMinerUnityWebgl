using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimpleVoxelSystem
{
    public enum ShopZoneType { Mine, Pickaxe, Sell, Minion }

    /// <summary>
    /// Invisible trigger cube for a shop zone.
    /// Created automatically by LobbyEditor when selecting the "🛒 Shop Zone" tool.
    /// </summary>
    [AddComponentMenu("SimpleVoxelSystem/Shop Zone")]
    public class ShopZone : MonoBehaviour
    {
        [Header("Shop Type")]
        public ShopZoneType zoneType = ShopZoneType.Mine;

        [Header("Zone Size (in blocks)")]
        public int sizeX = 3;
        public int sizeY = 3;
        public int sizeZ = 3;

        [Header("Key")]
        public KeyCode openKey = KeyCode.B;

        // ─── Runtime ─────────────────────────────────────────────────────────
        private bool          playerInside;
        private MineShopUI    mineShopUI;
        private PickaxeShopUI pickaxeShopUI;
        private MinionShopUI  minionShopUI;
        private PlayerPickaxe playerPickaxe;
        private MobileTouchControls mobileControls;
        private bool mobileControlsLookupDone;
        private GameObject    editorVisual;   // semi-transparent cube in editor mode
        private GameObject    gameplayMarker; // visible marker for sell point in normal gameplay
        private Material      visualMat;

        private static readonly Color ColNormal = new Color(0.20f, 0.55f, 1.00f, 0.28f);
        private static readonly Color ColDelete = new Color(1.00f, 0.20f, 0.20f, 0.42f);

        // One prompt for the entire scene
        private static GameObject promptPanel;
        private static Text       promptText;
        private static ShopZone   currentZone;


        // ═══════════════════════════════════════════════════════════════════════
        // Unity
        // ═══════════════════════════════════════════════════════════════════════

        void Start()
        {
            mineShopUI    = FindFirstObjectByType<MineShopUI>();
            pickaxeShopUI = FindFirstObjectByType<PickaxeShopUI>();
            minionShopUI  = FindFirstObjectByType<MinionShopUI>();
            playerPickaxe = FindFirstObjectByType<PlayerPickaxe>();
            TryResolveMobileControls();
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

            // Visual cube for editor mode
            CreateEditorVisual();
            CreateGameplayMarker();
            SetEditorVisible(false);  // initially hidden in game
        }

        // Enable/disable the editor visual cube
        public void SetEditorVisible(bool visible)
        {
            if (editorVisual != null) editorVisual.SetActive(visible);
        }

        // Highlight zone red on hover in editor (deletion mode)
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

            // Size and center match BoxCollider
            editorVisual.transform.localScale  = new Vector3(sizeX, sizeY, sizeZ);
            editorVisual.transform.localPosition = new Vector3(0f, sizeY * 0.5f - 0.5f, 0f);

            // Collider for visual cube is not needed
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
            // Only works if player is inside the shop zone
            if (!playerInside) return;
            if (currentZone != this) return;
            if (OnboardingTutorial.IsShopInteractionBlocked(zoneType))
            {
                ShowPrompt(false);
                return;
            }

            // Refresh prompt visibility every frame while inside
            ShowPrompt(true);

            TryResolveMobileControls();
            if (mobileControls != null && mobileControls.IsActive)
                mobileControls.RequestInteractHint(GetMobileActionLabel(), 80, true);

            if (!IsKeyPressed()) return;

            if (zoneType == ShopZoneType.Mine && mineShopUI != null)
                mineShopUI.TogglePanel();
            else if (zoneType == ShopZoneType.Pickaxe && pickaxeShopUI != null)
                pickaxeShopUI.Toggle();
            else if (zoneType == ShopZoneType.Minion)
            {
                if (minionShopUI == null) minionShopUI = FindFirstObjectByType<MinionShopUI>();
                if (minionShopUI != null) minionShopUI.Toggle();
            }
            else if (zoneType == ShopZoneType.Sell && playerPickaxe != null)
                playerPickaxe.SellResources();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;
            if (playerInside) return;

            // Switching from one zone to another must close any previously opened shop panel.
            if (currentZone != null && currentZone != this)
                currentZone.CloseAllShopPanels();

            playerInside = true;
            currentZone  = this;
            ShowPrompt(true);
        }

        void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;
            if (!playerInside) return;

            playerInside = false;
            CloseAllShopPanels();

            if (currentZone == this)
            {
                ShowPrompt(false);
                currentZone = null;
            }
            else if (!AnyZoneHasPlayerInside())
            {
                // Safety net: if we left the last zone, clear stale prompt state.
                currentZone = null;
                ShowPrompt(false);
            }
        }

        void OnDestroy()
        {
            if (currentZone == this) { currentZone = null; ShowPrompt(false); }
        }

        void CloseAllShopPanels()
        {
            if (mineShopUI != null)
                mineShopUI.SetPanelVisible(false);
            if (pickaxeShopUI != null)
                pickaxeShopUI.SetPanelVisible(false);
            if (minionShopUI != null)
                minionShopUI.SetPanelVisible(false);
        }

        static bool AnyZoneHasPlayerInside()
        {
            var zones = FindObjectsByType<ShopZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null && zones[i].playerInside)
                    return true;
            }
            return false;
        }

        public static bool IsAnyLocalPlayerInsideZone => currentZone != null && currentZone.playerInside;

        // ═══════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════

        static bool IsPlayer(Collider other)
        {
            // 1. Search for NetworkObject
            var no = other.GetComponentInParent<NetworkObject>();
            if (no != null)
            {
                // If it's a network object — it must belong to the local player
                return no.IsOwner && no.IsPlayerObject;
            }

            // 2. If no network object (single player mode) — check tags/components
            return other.CompareTag("Player")
                || other.GetComponentInParent<PlayerPickaxe>() != null
                || other.name.ToLower().Contains("player");
        }

        private char GetOpenKeyDisplay()
        {
            if (zoneType == ShopZoneType.Pickaxe) return 'P';
            if (zoneType == ShopZoneType.Minion) return 'M';
            if (zoneType == ShopZoneType.Sell) return 'R';
            return 'B';
        }

        private string GetMobileActionLabel()
        {
            if (zoneType == ShopZoneType.Pickaxe) return "PICK";
            if (zoneType == ShopZoneType.Minion) return "MINION";
            if (zoneType == ShopZoneType.Sell) return "SELL";
            return "MINE";
        }

        bool IsKeyPressed()
        {
            if (currentZone == this && mobileControls != null && mobileControls.IsActive && mobileControls.InteractPressedThisFrame)
                return true;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            if (zoneType == ShopZoneType.Pickaxe) return kb.pKey.wasPressedThisFrame;
            if (zoneType == ShopZoneType.Minion) return kb.mKey.wasPressedThisFrame;
            if (zoneType == ShopZoneType.Sell) return kb.rKey.wasPressedThisFrame;
            return kb.bKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            KeyCode k = KeyCode.B;
            if (zoneType == ShopZoneType.Pickaxe) k = KeyCode.P;
            else if (zoneType == ShopZoneType.Minion) k = KeyCode.M;
            else if (zoneType == ShopZoneType.Sell) k = KeyCode.R;
            return Input.GetKeyDown(k);
#else
            return false;
#endif
        }

        private void TryResolveMobileControls()
        {
            if (mobileControls != null || mobileControlsLookupDone)
                return;

            mobileControls = MobileTouchControls.GetOrCreateIfNeeded();
            mobileControlsLookupDone = true;
        }

        static void ShowPrompt(bool v) 
        { 
            if (promptPanel != null) 
            {
                bool mobileActive = MobileTouchControls.Instance != null && MobileTouchControls.Instance.IsActive;
                bool anyShopOpen = false;
                if (currentZone != null)
                {
                    if (currentZone.mineShopUI != null && currentZone.mineShopUI.IsVisible) anyShopOpen = true;
                    if (currentZone.pickaxeShopUI != null && currentZone.pickaxeShopUI.IsVisible) anyShopOpen = true;
                    if (currentZone.minionShopUI != null && currentZone.minionShopUI.IsVisible) anyShopOpen = true;
                }

                if (v && currentZone != null && !anyShopOpen)
                {
                    string keyStr = currentZone.GetOpenKeyDisplay().ToString();
                    string shopName = "Mine Shop";
                    if (currentZone.zoneType == ShopZoneType.Pickaxe) shopName = "Pickaxe Shop";
                    else if (currentZone.zoneType == ShopZoneType.Minion) shopName = "Minion Shop";
                    else if (currentZone.zoneType == ShopZoneType.Sell) shopName = "Sell Point";
                    promptText.text = mobileActive
                        ? $"Tap <color=#FFD700><b>[ACT]</b></color> to open {shopName}"
                        : $"Press <color=#FFD700><b>[{keyStr}]</b></color> to open {shopName}";
                    promptPanel.SetActive(true);
                }
                else
                {
                    promptPanel.SetActive(false);
                }
            } 
        }

        void EnsurePromptUI()
        {
            if (promptPanel != null && promptPanel.gameObject != null) return;
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            promptPanel = RuntimeUIFactory.MakePanel("ShopZonePrompt", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(370f, 48f), new Color(0.05f, 0.05f, 0.12f, 0.90f));
            promptText = RuntimeUIFactory.MakeLabel(promptPanel.transform, "Label", "Press [B] to open", 16, TextAnchor.MiddleCenter);

            promptPanel.SetActive(false);
        }

        // ────────────────────────────────────────────────────────────────────
        // Gizmo (Editor only)
        // ────────────────────────────────────────────────────────────────────

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
            string kStr = "B";
            string sName = "Mines";
            if (zoneType == ShopZoneType.Pickaxe) { kStr = "P"; sName = "Pickaxes"; }
            else if (zoneType == ShopZoneType.Minion) { kStr = "M"; sName = "Minions"; }
            else if (zoneType == ShopZoneType.Sell) { kStr = "R"; sName = "Sell"; }
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (sizeY + 0.4f),
                $"🛒 {sName}  {sizeX}x{sizeY}x{sizeZ}  [{kStr}]");
#endif
        }
    }
}
