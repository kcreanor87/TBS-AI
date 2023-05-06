using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Harvestable : MonoBehaviour
{
    [SerializeField] private float radiusTrigger = 1.5f;
    public float RadiusTrigger => radiusTrigger;

    [SerializeField] private float resourceAmount = 100f;
    public bool IsActive => resourceAmount > 0f;

    [SerializeField] private GameObject objMesh;

    private HarvestableArea parentArea;

    [SerializeField] private HarvestableType type;
    public HarvestableType Type => type;

    private void Start()
    {
        parentArea = GetComponentInParent<HarvestableArea>();
        if (parentArea != null) parentArea.AddToHarvestables(this);
    }

    public float Harvest(float amount)
    {
        float amountTaken = Mathf.Clamp(amount, 0f, resourceAmount);

        resourceAmount -= amountTaken;

        if (resourceAmount <= 0f)
        {
            objMesh.SetActive(false);
        }

        return amountTaken;
    }

    public void ResetHarvestable()
    {
        resourceAmount = 100f;

        objMesh.SetActive(true);
    }
}

public enum HarvestableType
{
    WOOD,
    STONE
}
