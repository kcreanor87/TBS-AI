using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HarvestableArea : MonoBehaviour
{
    public const float radius = 210f;

    private List<Harvestable> harvestables;
    public List<Harvestable> Harvestables => harvestables;

    private List<Harvester> harvesters;

    private Dictionary<HarvestableType, float> harvestedTotals;

    private HarvesterManager harvesterManager;

    private float woodPriority = 0.5f;
    private float stonePriority = 0.5f;

    // TEMP

    private GUIStyle style;

    private void Awake()
    {
        harvesterManager = GetComponent<HarvesterManager>();

        harvestables = new List<Harvestable>();
        harvestedTotals = new Dictionary<HarvestableType, float>();

        style = new GUIStyle();
        style.normal = new GUIStyleState();
        style.normal.textColor = Color.black;
        style.normal.background = Texture2D.whiteTexture;
    }

    private void Start()
    {
        GUIManager.instance.OnHarvestUpdated(this, harvesterManager);
    }

    public void AddToHarvestables(Harvestable harvestable)
    {
        if (!harvestables.Contains(harvestable)) harvestables.Add(harvestable);
    }

    public void AddToHarvesters(Harvester harvester)
    {
        if (harvesters == null) harvesters = new List<Harvester>();
        if (!harvesters.Contains(harvester)) harvesters.Add(harvester);
    }

    public void ResetHarvestables()
    {
        foreach (Harvestable harvestable in harvestables)
        {
            harvestable.ResetHarvestable();
        }
    }

    public bool IsHarvestableTargeted(Harvestable harvestable, Harvester currentHarvester)
    {
        foreach (Harvester harvester in harvesters)
        {
            if (currentHarvester == harvester) continue;
            if (harvester.NearestHarvestable == harvestable) return true;
        }

        return false;
    }

    public void DepositHarvest(HarvestableType type, float amount)
    {
        if (harvestedTotals.ContainsKey(type))
        {
            harvestedTotals[type] += amount;
        }
        else
        {
            harvestedTotals.Add(type, amount);
        }

        // Update GUI
        GUIManager.instance.OnHarvestUpdated(this, harvesterManager);
    }

    public int GetCurrentAmount(HarvestableType type)
    {
        if (harvestedTotals.ContainsKey(type)) return (int)harvestedTotals[type];
        else return 0;
    }

    public void DeductFromCurrentHarvest(HarvestableType type, int count)
    {
        if (harvestedTotals.ContainsKey(type)) harvestedTotals[type] = Mathf.Max(0, harvestedTotals[type] - count);

        // Update GUI
        GUIManager.instance.OnHarvestUpdated(this, harvesterManager);
    }

    public void UpdatePriorities(float woodValue, float stoneValue)
    {
        // Note, this needs to be a bit smarter, only changing agents without any current resources as a priority, and only changing those already harvesting if needed
        Debug.Log($"Updating Priorities: Wood = {Mathf.Round(woodValue * 100)}%, Stone = {Mathf.Round(stoneValue * 100)}%");
        woodPriority = woodValue;
        stonePriority = stoneValue;

        int targetWoodCount = Mathf.RoundToInt(harvesters.Count * woodPriority);
        int targetStoneCount = Mathf.RoundToInt(harvesters.Count * stonePriority);

        int woodCount = 0;
        int stoneCount = 0;

        foreach (Harvester harvester in harvesters)
        {
            if (woodCount < targetWoodCount)
            {
                harvester.SetType(HarvestableType.WOOD);
                woodCount++;
            }
            else if (stoneCount < targetStoneCount)
            {
                harvester.SetType(HarvestableType.STONE);
                stoneCount++;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Color col = Color.magenta;
        col.a = 0.1f;
        Gizmos.color = col;
        Gizmos.DrawSphere(transform.position, radius);
    }
}
