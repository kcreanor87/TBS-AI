using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a collection of flower plants and attached flowers
/// </summary>
public class FlowerArea : MonoBehaviour
{
    // The diameter of the area the agent and flowers can be
    // used for observing realtive distance from agent to flower
    public const float AreaDiameter = 20f;

    // The list of all the flower plants in this flower area (flower plants have multiple flowers)
    public GameObject[] flowersPlants;

    // A lookup dictionary for looking up a flower from a nectar collider
    private Dictionary<Collider, Flower> nectarFlowerDictionary;

    /// <summary>
    /// The list of all flowers in the flower area
    /// </summary>
    public List<Flower> Flowers { get; private set; }

    private void Awake()
    {
        // Initialise variables
        nectarFlowerDictionary= new Dictionary<Collider, Flower>();
        Flowers = new List<Flower>();
    }

    private void Start()
    {
        // Find all flowers that are children of this GameObject/Transform
        FindChildFlowers(transform);
    }

    /// <summary>
    /// Finds all flowers and flower plants that are children of a parent transform
    /// </summary>
    /// <param name="parent">Parent of the children to check</param>
    private void FindChildFlowers(Transform parent)
    {
        flowersPlants = GameObject.FindGameObjectsWithTag("flower_plant");

        Flower[] flowers = FindObjectsOfType<Flower>();

        foreach (Flower f in flowers)
        {
            Flowers.Add(f);
            nectarFlowerDictionary.Add(f.NectarCollider, f);
        }
    }

    /// <summary>
    /// Reset the flowers and flower plants
    /// </summary>
    public void ResetFlowers()
    {
        // Rotate each flower around the Y axis and subtly around X and Z
        foreach (GameObject flowerPlant in flowersPlants)
        {
            float xRotation = Random.Range(-5f, 5f);
            float yRotation = Random.Range(-180f, 180f);
            float zRotation = Random.Range(-5f, 5f);

            flowerPlant.transform.localRotation = Quaternion.Euler(xRotation, yRotation, zRotation);
        }

        // Reset each flower
        foreach (Flower flower in Flowers)
        {
            flower.ResetFlower();
        }
    }

    /// <summary>
    ///  Gets the <see cref="Flower"/> that a nectar collider belongs to
    /// </summary>
    /// <param name="collider">The nectar collider</param>
    /// <returns>The matching flower</returns>
    public Flower GetFlowerFromNectar(Collider collider)
    {
        if (nectarFlowerDictionary.ContainsKey(collider))
        {
            return nectarFlowerDictionary[collider];
        }

        return null;
    }
}
