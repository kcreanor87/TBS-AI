using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GUIManager : MonoBehaviour
{
    public static GUIManager instance;

    [Header("Harvester Panel")]
    [SerializeField] private Button btnSpawnHarvester;
    [SerializeField] private Slider woodSlider;
    [SerializeField] private Slider stoneSlider;
    [SerializeField] private TextMeshProUGUI txtCurrentWood;
    [SerializeField] private TextMeshProUGUI txtCurrentStone;

    private HarvestableArea area;
    private HarvesterManager harvesterManager;

    private bool slidersUpdated;

    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this) Destroy(gameObject);

        btnSpawnHarvester.onClick.AddListener(() => OnBuyHarvester());

        woodSlider.SetValueWithoutNotify(0.5f);
        stoneSlider.SetValueWithoutNotify(0.5f);
    }

    private void Update()
    {
        if (slidersUpdated && Input.GetMouseButtonUp(0))
        {
            slidersUpdated = false;
            area.UpdatePriorities(woodSlider.value, stoneSlider.value);
        }
    }

    public void OnHarvestUpdated(HarvestableArea area, HarvesterManager manager)
    {
        if (harvesterManager == null) harvesterManager = manager;

        this.area = area;

        int wood = area.GetCurrentAmount(HarvestableType.WOOD);
        int stone = area.GetCurrentAmount(HarvestableType.STONE);

        txtCurrentWood.text = wood.ToString();
        txtCurrentStone.text = stone.ToString();

        btnSpawnHarvester.interactable = harvesterManager.harvesterCost.CanAfford(wood, stone);
    }

    public void OnBuyHarvester()
    {
        if (harvesterManager != null && area != null)
        {
            // Remove cost from area manager
            area.DeductFromCurrentHarvest(HarvestableType.WOOD, harvesterManager.harvesterCost.woodCost);
            area.DeductFromCurrentHarvest(HarvestableType.STONE, harvesterManager.harvesterCost.stoneCost);

            // Spawn harvester
            harvesterManager.SpawnHarvester(HarvestableType.WOOD);
        }
    }

    public void OnWoodSliderValueChanged()
    {
        // Change priority of harvesters
        stoneSlider.SetValueWithoutNotify(1f - woodSlider.value);
        slidersUpdated = true;

    }
    public void OnStoneSliderValueChanged()
    {
        // Change priority of harvesters
        woodSlider.SetValueWithoutNotify(1f - stoneSlider.value);
        slidersUpdated = true;

    }
}
