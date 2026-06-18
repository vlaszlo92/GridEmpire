using GridEmpire.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace GridEmpire.Visuals
{

    public class FogOfWarManager : MonoBehaviour
    {
        public static FogOfWarManager Instance { get; private set; }

        [SerializeField] private GameObject fogPrefab;
        private GameObject gridManagerObject;

        [Header("Fog Settings")]
        [Range(0.1f, 50f)][SerializeField] private float startSize = 1f;
        [Range(0f, 1f)][SerializeField] private float startOpacity = 0.5f;
        [Range(0f, 3f)][SerializeField] private float startSpeed = 0.1f;
        [Range(1, 500)][SerializeField] private int maxParticles = 10;
        [Range(0.1f, 500f)][SerializeField] private float emissionRate = 2f;
        private bool _initialized = false;

        private Dictionary<CellVisual, GameObject> fogObjects = new();

        void Awake() => Instance = this;
        private IEnumerator Start()
        {
            yield return new WaitUntil(() => GridManager.Instance != null && GridManager.Instance.IsReady);
            ApplySettingsToAll();
        }
        public void ApplySettingsToAll()
        {
            if (gridManagerObject == null)
            {
                Debug.LogError("[FOW] gridManagerObject null!");
                return;
            }

            int found = 0;
            foreach (Transform hex in gridManagerObject.transform)
            {
                var fogTransform = hex.Find("Gray Volume Fog(Clone)");
                if (fogTransform == null) continue;

                var ps = fogTransform.GetComponentInChildren<ParticleSystem>();
                if (ps == null) continue;

                found++;
                ApplySettings(ps);
            }

            Debug.Log($"[FOW] ApplySettingsToAll: {found} PS frissítve");
        }

        private void ApplySettings(ParticleSystem ps)
        {
            var main = ps.main;
            main.startSize = startSize;
            main.startSpeed = startSpeed;
            main.maxParticles = maxParticles;

            var startColor = main.startColor.color;
            startColor.a = (byte)(startOpacity * 255);
            main.startColor = startColor;

            var emission = ps.emission;
            emission.rateOverTime = emissionRate;
        }

        public void SetFog(CellVisual cell, bool hidden)
        {
            if (hidden) ShowFog(cell);
            else HideFog(cell);
        }

        private void ShowFog(CellVisual cell)
        {
            if (fogObjects.ContainsKey(cell)) return;

            var fog = Instantiate(fogPrefab, cell.transform.position, Quaternion.identity);
            fog.transform.SetParent(cell.transform);

            var ps = fog.GetComponent<ParticleSystem>();
            if (ps != null) ApplySettings(ps);

            fogObjects[cell] = fog;
        }

        private void HideFog(CellVisual cell)
        {
            if (!fogObjects.TryGetValue(cell, out var fog)) return;
            Destroy(fog);
            fogObjects.Remove(cell);
        }
        void OnValidate()
        {
            if (!Application.isPlaying) return;

            if (gridManagerObject != null && !_initialized)
            {
                InitFromFirstPS();
                _initialized = true;
            }
        }

        private void InitFromFirstPS()
        {
            foreach (Transform hex in gridManagerObject.transform)
            {
                var ps = hex.GetComponentInChildren<ParticleSystem>();
                if (ps == null) continue;

                var main = ps.main;
                startSize = main.startSize.constant;
                startSpeed = main.startSpeed.constant;
                maxParticles = main.maxParticles;
                startOpacity = main.startColor.color.a / 255f;
                emissionRate = ps.emission.rateOverTime.constant;
                break;
            }
        }
    }
}