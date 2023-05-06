using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HarvesterManager : MonoBehaviour
{
    [Header("Harvester Settings")]
    [SerializeField] private Harvester harvesterPrefab;

    public Cost harvesterCost = new Cost(200, 200);

    private HarvestableArea area;

    private void Awake()
    {
        area = GetComponent<HarvestableArea>();
    }

    public void SpawnHarvester(HarvestableType type)
    {
        Harvester harvester = Instantiate(harvesterPrefab, area.transform.position, Quaternion.identity, area.transform);
        harvester.SetType(type);
    }
}

public struct Cost
{
    public int woodCost;
    public int stoneCost;

    public Cost(int woodCost, int stoneCost)
    {
        this.woodCost = woodCost;
        this.stoneCost = stoneCost;
    }

    public bool CanAfford(int woodCost, int stoneCost)
    {
        return woodCost >= this.woodCost && stoneCost >= this.stoneCost;
    }
}
