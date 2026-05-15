using GridEmpire.Visuals;
using System.Collections.Generic;
using UnityEngine;

public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager Instance { get; private set; }

    [SerializeField] private GameObject fogPrefab;

    [Header("Fog Settings")]
    [Range(0.1f, 5f)][SerializeField] private float startSize = 1f;
    [Range(0f, 1f)][SerializeField] private float startOpacity = 0.5f;
    [Range(0f, 3f)][SerializeField] private float startSpeed = 0.1f;
    [Range(1, 50)][SerializeField] private int maxParticles = 10;
    [Range(0.1f, 5f)][SerializeField] private float emissionRate = 2f;

    private Dictionary<CellVisual, GameObject> fogObjects = new();

    void Awake()
    {
        Instance = this;
    }

    public void SetFog(CellVisual cell, bool hidden)
    {
        if (hidden) ShowFog(cell);
        else HideFog(cell);
    }

    private void ShowFog(CellVisual cell)
    {
        if (fogObjects.ContainsKey(cell)) return;

        var fog = Instantiate(fogPrefab,
            cell.transform.position + Vector3.up * 0.1f,
            Quaternion.identity);

        fog.transform.SetParent(cell.transform);

        // Beállítások alkalmazása
        var ps = fog.GetComponent<ParticleSystem>();
        if (ps != null) ApplySettings(ps);

        fogObjects[cell] = fog;
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

    private void OnValidate()
    {
        foreach (var fog in fogObjects.Values)
        {
            if (fog == null) continue;
            var ps = fog.GetComponent<ParticleSystem>();
            if (ps != null) ApplySettings(ps);
        }
    }

    private void HideFog(CellVisual cell)
    {
        if (!fogObjects.TryGetValue(cell, out var fog)) return;
        Destroy(fog);
        fogObjects.Remove(cell);
    }
}