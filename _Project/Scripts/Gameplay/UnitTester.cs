using UnityEngine;
using System.Collections.Generic;
using GridEmpire.Core;
using GridEmpire.Gameplay;

public class UnitTester : MonoBehaviour
{
    public GridManager gridManager;
    public UnitData unitData;
    public GameObject unitPrefab;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) RunStandardDuel();
        if (Input.GetKeyDown(KeyCode.F2)) RunGankTest();
        if (Input.GetKeyDown(KeyCode.F3)) RunAmbushTest();
        if (Input.GetKeyDown(KeyCode.F10)) ClearAllUnits();
    }

    void RunStandardDuel()
    {
        ClearAllUnits();
        Debug.Log("--- TESZT F1: 1v1 DUEL ---");
        CellData c1 = gridManager.GetCell(0, 0);
        CellData c2 = gridManager.GetCell(2, 0);

        // Path: [0] a start, [1] ahova lépni akar. 
        // A rendszer a start mezõt azonnal a sajátjává teszi.
        SpawnTestUnit(c1, 0, Color.blue, new List<CellData> { c1, gridManager.GetCell(1, 0) });
        SpawnTestUnit(c2, 1, Color.red, new List<CellData> { c2, gridManager.GetCell(1, 0) });
    }

    void RunGankTest()
    {
        ClearAllUnits();
        Debug.Log("--- TESZT F2: 2v1 GANK ---");
        CellData targetCell = gridManager.GetCell(0, 0);
        if (targetCell == null) return;

        // P1: Célpont középen
        SpawnTestUnit(targetCell, 1, Color.red, null);

        var neighbors = gridManager.GetNeighbors(targetCell);
        if (neighbors.Count < 2) return;

        // P0: Már szomszéd
        SpawnTestUnit(neighbors[0], 0, Color.blue, null);

        // P2: Távolabbról jön
        var farNeighbors = gridManager.GetNeighbors(neighbors[1]);
        CellData startCellP2 = farNeighbors.Find(c => c != targetCell);
        if (startCellP2 != null)
        {
            SpawnTestUnit(startCellP2, 2, Color.green, new List<CellData> { startCellP2, neighbors[1] });
        }
    }

    void RunAmbushTest()
    {
        ClearAllUnits();
        Debug.Log("--- TESZT F3: AMBUSH ---");
        CellData center = gridManager.GetCell(0, 0);
        var neighbors = gridManager.GetNeighbors(center);
        if (neighbors.Count < 2) return;

        CellData p0Cell = center;
        CellData p1Cell = neighbors[0];

        SpawnTestUnit(p0Cell, 0, Color.blue, null);
        SpawnTestUnit(p1Cell, 1, Color.red, null);

        var neighborsOfP1 = gridManager.GetNeighbors(p1Cell);
        CellData p2TargetCell = neighborsOfP1.Find(c => c != p0Cell && c != p1Cell);
        if (p2TargetCell == null) p2TargetCell = neighborsOfP1[1];

        var p2StartNeighbors = gridManager.GetNeighbors(p2TargetCell);
        CellData p2StartCell = p2StartNeighbors.Find(c => c != p1Cell && c != p0Cell);

        if (p2StartCell != null)
        {
            SpawnTestUnit(p2StartCell, 2, Color.green, new List<CellData> { p2StartCell, p2TargetCell });
        }
    }

    void ClearAllUnits()
    {
        UnitController[] units = FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        foreach (var u in units) u.ExecuteDeath();

        // Opcionális: a pálya színeit is visszaállíthatod alaphelyzetbe, 
        // ha tiszta lappal akarsz indulni minden F gombnál.
    }

    void SpawnTestUnit(CellData startCell, int ownerId, Color color, List<CellData> path)
    {
        if (startCell == null) return;

        // --- ÚJ RÉSZ: A START MEZÕ KÉNYSZERÍTETT ELFOGLALÁSA ---
        // Az egység csak saját területen állhat stabilan a kör elején.
        gridManager.FinalizeCapture(startCell, ownerId);

        Vector3 pos = gridManager.GetWorldPosition(startCell.Q, startCell.R);
        GameObject go = Instantiate(unitPrefab, pos, Quaternion.identity);
        go.name = $"Unit_P{ownerId}";

        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.material.color = color;

        UnitController controller = go.GetComponent<UnitController>();
        if (controller == null) controller = go.AddComponent<UnitController>();

        // Inicializálás: az egység megkapja az adatait és az útvonalát
        controller.Initialize(unitData, path, gridManager, ownerId);

        // Logikai regisztráció
        controller._currentCell = startCell;
        startCell.RegisterOccupier(controller);

        Debug.Log($"[SPAWN] {go.name} -> Cell ({startCell.Q},{startCell.R}) | Owner: {ownerId}");
    }
}