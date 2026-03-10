using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SimpleVoxelSystem
{
    [DisallowMultipleComponent]
    public sealed class WorldSkyboxController : MonoBehaviour
    {
        private static Material _runtimeSkybox;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<WorldSkyboxController>() != null)
                return;

            GameObject go = new GameObject("WorldSkyboxController");
            DontDestroyOnLoad(go);
            go.AddComponent<WorldSkyboxController>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            EnsureSkybox();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene _, LoadSceneMode __)
        {
            EnsureSkybox();
        }

        private static void EnsureSkybox()
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
                return;

            if (_runtimeSkybox == null)
            {
                _runtimeSkybox = new Material(shader)
                {
                    name = "Runtime Procedural Skybox"
                };

                _runtimeSkybox.SetColor("_SkyTint", new Color(0.58f, 0.76f, 1f));
                _runtimeSkybox.SetColor("_GroundColor", new Color(0.34f, 0.30f, 0.26f));
                _runtimeSkybox.SetFloat("_SunSize", 0.028f);
                _runtimeSkybox.SetFloat("_SunSizeConvergence", 6f);
                _runtimeSkybox.SetFloat("_AtmosphereThickness", 1.15f);
                _runtimeSkybox.SetFloat("_Exposure", 1.2f);
            }

            RenderSettings.skybox = _runtimeSkybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.60f, 0.72f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.60f, 0.66f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.24f, 0.24f);
            RenderSettings.fog = false;
            DynamicGI.UpdateEnvironment();

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == null)
                    continue;

                cameras[i].clearFlags = CameraClearFlags.Skybox;
            }
        }
    }
}
