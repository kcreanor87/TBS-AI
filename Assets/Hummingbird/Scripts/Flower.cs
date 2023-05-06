using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a single flower with nectar
/// </summary>
public class Flower : MonoBehaviour
{
    [Tooltip("Color when full")]
    [SerializeField] private Color fullFlowerColor = new Color(1f, 0f, 0.3f);

    [Tooltip("Color when Empty")]
    [SerializeField] private Color emptyFlowerColor = new Color(0.5f, 0f, 1f);

    /// <summary>
    /// Trigger representing the nectar
    /// </summary>
    [SerializeField] private Collider nectarCollider;
    public Collider NectarCollider => nectarCollider;

    // Solid collider representing the flower
    private Collider flowerCollider;

    // The flowers material
    private Material flowerMaterial;

    /// <summary>
    /// Vector point straight out the flower
    /// </summary>
    public Vector3 FlowerUpVector => nectarCollider.transform.up;

    /// <summary>
    /// The centre point of the nectar collider
    /// </summary>
    public Vector3 FlowerCenterPosition => nectarCollider.transform.position;

    // The amount of nectar remaining in the flower
    public float NectarAmount { get; private set; }

    /// <summary>
    /// Whether flowe has any nectar remaining
    /// </summary>
    public bool HasNectar => NectarAmount > 0;

    private void Awake()
    {
        flowerMaterial = GetComponent<MeshRenderer>().material;

        flowerCollider = transform.Find("FlowerCollider").GetComponent<Collider>();
        nectarCollider = transform.Find("FlowerNectarCollider").GetComponent<Collider>();
    }

    /// <summary>
    /// Atthempts to remove nectar from the flower
    /// </summary>
    /// <param name="amount"> The amount of nectar to remove</param>
    /// <returns> The actual amount succesffully removed</returns>
    public float Feed(float amount)
    {
        // Track how much was taken
        float nectarTaken = Mathf.Clamp(amount, 0f, NectarAmount);

        NectarAmount -= amount;

        if (NectarAmount <= 0f)
        {
            // No nectar remains
            NectarAmount = 0f;

            // Disable flower and nectar collider
            flowerCollider.gameObject.SetActive(false);
            nectarCollider.gameObject.SetActive(false);

            // Change the flower color to indicate that it is empty
            flowerMaterial.SetColor("_Color", emptyFlowerColor);
        }

        // Return the amount taken
        return nectarTaken;
    }

    /// <summary>
    /// Resets the flower
    /// </summary>
    public void ResetFlower()
    {
        // Refill the nectar
        NectarAmount = 1f;

        // Enable the flower and nectar colliders
        flowerCollider.gameObject.SetActive(true);
        nectarCollider.gameObject.SetActive(true);

        // Change the flower color to indicate that it is full
        flowerMaterial.SetColor("_Color", fullFlowerColor);
    }
}
