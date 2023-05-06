using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class Harvester : Agent
{
    new private Rigidbody   rigidbody;
    private HarvestableArea harvestableArea;
    private Harvestable     nearestHarvestable;
    private bool            frozen;

    [Header("Movement Settings")]
    [SerializeField] 
    private float           moveSpeed = 2f;
    [SerializeField] 
    private float           rotationSpeed = 2f;

    [Header("Agent Settings")]
    [SerializeField] 
    private bool            trainingMode;

    [Header("Harvester Settings")]
    [SerializeField]
    private HarvestableType targetType = HarvestableType.WOOD;
    // Used when changing types, only switch when empty of resources
    private HarvestableType scheduledType = HarvestableType.WOOD;
    private bool scheduledForChange;

    [Header("Visuals")]
    [SerializeField] private GameObject woodcutterVisuals;
    [SerializeField] private GameObject stonecutterVisuals;

    public Harvestable      NearestHarvestable => nearestHarvestable;
    public float            ResourcesHarvested { get; private set; }

    #region Agent Override Functions

    public override void Initialize()
    {
        rigidbody = GetComponent<Rigidbody>();
        harvestableArea = GetComponentInParent<HarvestableArea>();
        harvestableArea.AddToHarvesters(this);

        OnTypeChanged();

        if (!trainingMode) MaxStep = 0;
    }

    public override void OnEpisodeBegin()
    {
        if (trainingMode)
        {
            harvestableArea.ResetHarvestables();
        }

        ResourcesHarvested = 0f;

        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;

        MoveToSafeRandomPosition();
        UpdateNearestHarvestable();
    }

    // Input from player
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continousActions = actionsOut.ContinuousActions;

        float x = Input.GetAxis("Mouse X");
        float y = Input.GetAxis("Vertical");        

        continousActions[0] = x;
        continousActions[1] = y;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Don't take actions if frozen;
        if (frozen) return;

        float x = actions.ContinuousActions[0];
        float y = actions.ContinuousActions[1];

        transform.Rotate(0f, x * rotationSpeed, 0f);
        rigidbody.MovePosition(transform.position + transform.forward * y * moveSpeed * Time.fixedDeltaTime);
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        // If nearestFlower is null, observe an empty array and return early
        if (nearestHarvestable == null)
        {
            sensor.AddObservation(new float[9]);
            return;
        }

        Vector3 targetPos = ResourcesHarvested >= 100f ? harvestableArea.transform.position : nearestHarvestable.transform.position;

        // Observe the agent's local rotation (4 observations)
        sensor.AddObservation(transform.rotation.normalized);

        // Get a vector from the agent to the nearest harvestable
        Vector3 toTarget = targetPos
            - transform.position;
        Vector3 toTargetNormalised = toTarget.normalized;

        // Observe the normalised vector pointing to the nearest harvestable (3 observations)
        sensor.AddObservation(toTargetNormalised);

        //  Observe a dot product that indicates whether the agent is in front of the harvestable
        // 1 observation
        sensor.AddObservation(Vector3.Dot(transform.forward, toTargetNormalised));

        // Observe the relative distance from the agent to the harvestable
        // 1 observation
        sensor.AddObservation(toTarget.sqrMagnitude / (HarvestableArea.radius * HarvestableArea.radius));

        // 9 Total observations
    }

    #endregion

    void MoveToSafeRandomPosition()
    {
        float xOffset = harvestableArea.transform.position.x;
        float zOffset = harvestableArea.transform.position.z;
        float x;
        float z;

        int ticks = 0;

        bool isOverlapping = true;
        Vector3 position = Vector3.zero;

        while (isOverlapping && ticks < 100)
        {
            x = Random.Range(-10f, 10f);
            z = Random.Range(-10f, 10f);
            position = new Vector3(x + xOffset, 0.75f, z + zOffset);

            isOverlapping = Physics.OverlapSphere(position, 0.7f).Length > 0;
            ticks++;
        }

        Debug.Assert(!isOverlapping, "Safe Position Not Found!");

        transform.position = position;

        UpdateNearestHarvestable();

        if (nearestHarvestable) transform.LookAt(nearestHarvestable.transform.position);
    }

    void UpdateNearestHarvestable()
    {
        foreach (Harvestable harvestable in harvestableArea.Harvestables)
        {
            // Ignore any harvestables not of the correct type
            if (harvestable.Type != targetType) continue;

            if (nearestHarvestable == null && harvestable.IsActive)
            {
                nearestHarvestable = harvestable;
                break;
            }
            else if (harvestable.IsActive)
            {
                // Check if closer
                float distanceToHarvestable = (harvestable.transform.position - transform.position).sqrMagnitude;
                float distanceToCurrentNearestHarvestable = (nearestHarvestable.transform.position - transform.position).sqrMagnitude;

                if (!nearestHarvestable.IsActive 
                    || distanceToHarvestable < distanceToCurrentNearestHarvestable 
                    || harvestableArea.IsHarvestableTargeted(nearestHarvestable, this))
                {
                    nearestHarvestable = harvestable;
                }
            }
        }
    }

    /// <summary>
    /// Prevent the agent from moving and taking actions
    /// </summary>
    public void FreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreeze not suported in training");
        frozen = true;
        rigidbody.Sleep();
    }

    /// <summary>
    /// Allow the agent to move and take actions
    /// </summary>
    public void UnfreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreeze not suported in training");
        frozen = false;
        rigidbody.WakeUp();
    }

    void CheckProximity()
    {
        if (nearestHarvestable != null && ResourcesHarvested < 100f && !scheduledForChange)
        {
            if (!nearestHarvestable.IsActive)
            {
                UpdateNearestHarvestable();
            }
            else
            {
                Vector3 direction = nearestHarvestable.transform.position - transform.position;
                float distance = direction.sqrMagnitude;
                float facingValue = Vector3.Dot(transform.forward, direction);
                if (distance <= Mathf.Pow(nearestHarvestable.RadiusTrigger, 2) && facingValue > 0.65f)
                {
                    float harvestedAmount = nearestHarvestable.Harvest(1f);
                    ResourcesHarvested += harvestedAmount;

                    if (trainingMode)
                    {
                        AddReward(0.005f);
                    }

                    if (!nearestHarvestable.IsActive) UpdateNearestHarvestable();

                    Debug.DrawLine(transform.position, nearestHarvestable.transform.position, Color.green);
                }
                else
                {
                    Debug.DrawLine(transform.position, nearestHarvestable.transform.position, Color.red);
                }
            }
        }
        else if (ResourcesHarvested >= 100f || scheduledForChange)
        {
            Vector3 direction = harvestableArea.transform.position - transform.position;
            float distance = direction.sqrMagnitude;

            if (distance <= Mathf.Pow(7f, 2))
            {
                harvestableArea.DepositHarvest(targetType, ResourcesHarvested);
                ResourcesHarvested = 0f;

                if (trainingMode)
                {
                    AddReward(0.5f);
                }
                // Switch type when scheduled now we have no harvest left
                if (scheduledForChange)
                {
                    targetType = scheduledType;
                    scheduledForChange = false;
                    OnTypeChanged();
                }

                UpdateNearestHarvestable();
            }
            else
            {
                Debug.DrawLine(transform.position, harvestableArea.transform.position, Color.red);
            }
        }
    }

    public void SetType(HarvestableType type)
    {
        // Already set to this type, ignore
        if (targetType == type) return;

        // Nothing held, ignore
        if (ResourcesHarvested <= 0f)
        {
            targetType = type;
            nearestHarvestable = null;
            UpdateNearestHarvestable();
            OnTypeChanged();
        }
        else
        {
            // Schedule switch so this harvester returns home with current harvest before switching
            scheduledForChange = true;
            scheduledType = type;
        }
    }

    private void OnTypeChanged()
    {
        woodcutterVisuals.SetActive(targetType == HarvestableType.WOOD);
        stonecutterVisuals.SetActive(targetType == HarvestableType.STONE);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (trainingMode)
        {
            if (collision.collider.CompareTag("boundary"))
            {
                // Collided with the area bounary, give a negative reward
                AddReward(-0.5f);
            }
            else if (collision.collider.CompareTag("Wall"))
            {
                AddReward(-0.001f);
            }
        }
    }

    void FixedUpdate()
    {
        if (nearestHarvestable != null && !nearestHarvestable.IsActive)
        {
            UpdateNearestHarvestable();
        }

        CheckProximity();
    }
}
