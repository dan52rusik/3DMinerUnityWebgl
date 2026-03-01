using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Невидимый триггер-куб зоны магазина.
    /// Создаётся автоматически LobbyEditor при выборе инструмента «🛒 Зона магазина».
    /// Никаких настроек в Inspector не нужно — всё задаётся через диалог размера.
    ///
    /// В игре: полностью невидим.
    /// В Editor: синий полупрозрачный куб с подписью.
    /// </summary>
    [AddComponentMenu("SimpleVoxelSystem/Shop Zone")]
    public class ShopZone : MonoBehaviour
    {
        [Header("Размер зоны (в блоках)")]
        public int sizeX = 3;
        public int sizeY = 3;
        public int sizeZ = 3;

        [Header("Клавиша")]
        public KeyCode openKey = KeyCode.B;

        // ─── Runtime ─────────────────────────────────────────────────────────
        private bool        playerInside;
        private MineShopUI  shopUI;
        private GameObject  editorVisual;   // полупрозрачный куб в режиме редактора
        private Material    visualMat;

        private static readonly Color ColNormal = new Color(0.20f, 0.55f, 1.00f, 0.28f);
        private static readonly Color ColDelete = new Color(1.00f, 0.20f, 0.20f, 0.42f);

        // Один промпт на всю сцену
        private static GameObject promptPanel;
        private static Text       promptText;
        private static ShopZone   currentZone;


        // ══════════════════════════════════════════════════════════════════════
        // Unity
        // ══════════════════════════════════════════════════════════════════════

        void Start()
        {
            shopUI = FindFirstObjectByType<MineShopUI>();
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

            // Визуальный куб для режима редактора
            CreateEditorVisual();
            SetEditorVisible(false);  // начально скрыт в игре
        }

        // Включить/выключить визуальный куб редактора
        public void SetEditorVisible(bool visible)
        {
            if (editorVisual != null) editorVisual.SetActive(visible);
        }

        // Подсветить зону красным при hover эдитора (удаление)
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

            // Размер и центр совпадают с BoxCollider
            editorVisual.transform.localScale  = new Vector3(sizeX, sizeY, sizeZ);
            editorVisual.transform.localPosition = new Vector3(0f, sizeY * 0.5f - 0.5f, 0f);

            // Коллайдер визуального куба не нужен
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

        void Update()
        {
            // B работает только если игрок внутри зоны магазина
            if (!playerInside) return;
            if (!IsKeyPressed()) return;
            if (shopUI != null) shopUI.TogglePanel();
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
                if (shopUI != null) shopUI.SetPanelVisible(false);
            }
        }

        void OnDestroy()
        {
            if (currentZone == this) { currentZone = null; ShowPrompt(false); }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Helpers
        // ══════════════════════════════════════════════════════════════════════

        static bool IsPlayer(Collider other)
        {
            // 1. Ищем NetworkObject
            var no = other.GetComponentInParent<NetworkObject>();
            if (no != null)
            {
                // Если это сетевой объект — он должен принадлежать локальному игроку
                return no.IsOwner && no.IsPlayerObject;
            }

            // 2. Если сетевого объекта нет (одиночный режим) — проверяем тег/компоненты
            return other.CompareTag("Player")
                || other.GetComponentInParent<PlayerPickaxe>() != null
                || other.name.ToLower().Contains("player");
        }

        bool IsKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?[Key.B].wasPressedThisFrame ?? false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(openKey);
#else
            return false;
#endif
        }

        static void ShowPrompt(bool v) { if (promptPanel != null) promptPanel.SetActive(v); }

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
            promptText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            promptText.fontSize  = 16;
            promptText.alignment = TextAnchor.MiddleCenter;
            promptText.color     = Color.white;
            promptText.supportRichText = true;
            promptText.text = "Нажмите <color=#FFD700><b>[B]</b></color> — открыть магазин шахт";

            promptPanel.SetActive(false);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Gizmo (только в Editor)
        // ══════════════════════════════════════════════════════════════════════

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
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (sizeY + 0.4f),
                $"🛒 Магазин  {sizeX}×{sizeY}×{sizeZ}  [B]");
#endif
        }
    }
}
