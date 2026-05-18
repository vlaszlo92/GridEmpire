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

        var fog = Instantiate(fogPrefab, cell.transform.position, Quaternion.identity);
        fog.transform.SetParent(cell.transform);

        // Beállítások alkalmazása
        var ps = fog.GetComponent<ParticleSystem>();

        fogObjects[cell] = fog;
    }

    private void HideFog(CellVisual cell)
    {
        if (!fogObjects.TryGetValue(cell, out var fog)) return;
        Destroy(fog);
        fogObjects.Remove(cell);
    }
}