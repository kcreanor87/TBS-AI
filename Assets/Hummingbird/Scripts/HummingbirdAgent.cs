using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using System.Security.Cryptography;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

/// <summary>
/// A hummingbird Machine Learning Agent
/// </summary>
public class HummingbirdAgent : Agent
{
    [Tooltip("Force to apply when moving")]
    public float moveForce = 2f;

    [Tooltip("Speed to pitch up or down")]
    public float pitchSpeed = 100f;

    [Tooltip("Speed to rotate around the up axis")]
    public float yawSpeed = 100f;

    [Tooltip("Transform at the tip of the beak")]
    public Transform beakTip;

    [Tooltip("The agents camera")]
    public Camera agentCamera;

    [Tooltip("Whether this is training mode or gameplay mode")]
    public bool trainingMode;

    // Rigidbody of the agent
    new private Rigidbody rigidbody;

    // Flower area that the agent is in
    private FlowerArea flowerArea;

    // The nearest flower to the agent
    private Flower nearestFlower;

    // Allows for smoother pitch changes
    private float smoothPitchChange = 0f;

    // Allows for smoother yaw changes
    private float smoothYawChange = 0f;

    // Maximum angle the bird can pitch up or down
    private const float MaxPitchAngle = 80f;

    //MAximum distance from the tip to accept nectar collision
    private const float BeakTipRadius = 0.008f;

    // Whether the agent is frozen (intentionally not flying)
    private bool frozen = false;

    /// <summary>
    /// The amount of nectar the agent has obstained this episode
    /// </summary>
    public float NectarObtained { get; private set; }

    public override void Initialize()
    {
        rigidbody = GetComponent<Rigidbody>();
        flowerArea = GetComponentInParent<FlowerArea>();

        if (!trainingMode) MaxStep = 0;
    }

    /// <summary>
    /// Reset the agent when an episode begins
    /// </summary>
    public override void OnEpisodeBegin()
    {
        if (trainingMode)
        {
            // Only reset flowers in training when there is one agent per area
            flowerArea.ResetFlowers();
        }

        // Reset nectar obstained
        NectarObtained = 0f;

        // Note: Important!!
        // Zero out velocity so that movement stops before a new episode begins
        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;

        // Default to spawning in front of a flower
        bool inFrontOfFlower = true;

        if (trainingMode)
        {
            // spawn in front of a flower 50% of the time during training
            inFrontOfFlower = Random.value > 0.5f;
        }

        // Move the agent to a new random position
        MoveToSafeRandomPosition(inFrontOfFlower);

        //Recalculate the nearest flower now that the agent has moved
        UpdateNearestFlower();
    }

    /// <summary>
    /// Called when action is received from either the plater input for the neural network
    /// actions can be continuous or dynamic, and come in (-1 -> 1f range)
    /// </summary>
    /// <param name="actions"></param>
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Don't take actions if frozen;
        if (frozen) return;

        float moveVectorX = actions.ContinuousActions[0];
        float moveVectorY = actions.ContinuousActions[1];
        float moveVectorZ = actions.ContinuousActions[2];

        // Calculate movement vector
        Vector3 move = new Vector3(moveVectorX, moveVectorY, moveVectorZ);

        // Move rigidbody by this amount
        rigidbody.AddForce(move * moveForce);

        // Get the current rotation
        Vector3 rotationVector = transform.eulerAngles;

        // calculate pitch and yaw rotations
        float pitchChange = actions.ContinuousActions[3];
        float yawChange = actions.ContinuousActions[4];

        // Calculate smooth rotaiton changes
        smoothPitchChange = Mathf.MoveTowards(smoothPitchChange, pitchChange, 2f * Time.fixedDeltaTime);
        smoothYawChange = Mathf.MoveTowards(smoothYawChange, yawChange, 2f * Time.fixedDeltaTime);

        // Calculate the new pitch and yaw based on smoothed values
        // Clamp pitch to avoid flipping uppside down
        float pitch = rotationVector.x + smoothPitchChange * Time.fixedDeltaTime * pitchSpeed;
        if (pitch > 180f) pitch -= 360f;
        pitch = Mathf.Clamp(pitch, -MaxPitchAngle, MaxPitchAngle);

        float yaw = rotationVector.y + smoothYawChange * Time.fixedDeltaTime * yawSpeed;

        // Apply the new rotation
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    /// <summary>
    /// Collect vector observations from the environment
    /// </summary>
    /// <param name="sensor"></param>
    public override void CollectObservations(VectorSensor sensor)
    {

        // If nearestFlower is null, observe an empty array and return early
        if (nearestFlower == null)
        {
            sensor.AddObservation(new float[10]);
            return;
        }

        // Observe the agent's local rotation (4 observations)
        sensor.AddObservation(transform.localRotation.normalized);

        // Get a vector from the beak ti[ to the nearest flower
        Vector3 toFlower = nearestFlower.FlowerCenterPosition - beakTip.position;

        // Observe the normalised vector pointing to the nearest flower (3 observations)
        sensor.AddObservation(toFlower.normalized);

        //  Observe a dot product that indicates wether the bird (via beak tip position) in front of the flower
        // +1 means that the bird is directly in front, -1 means directly behind
        // 1 observation
        sensor.AddObservation(Vector3.Dot(toFlower.normalized, -nearestFlower.FlowerUpVector.normalized));

        // Observe a dot product that indicates whether the beak itself is pointing towards the flower
        // +1 means that the beak is poitning direction at the flower
        // 1 observation
        sensor.AddObservation(Vector3.Dot(beakTip.forward.normalized, -nearestFlower.FlowerUpVector.normalized));

        // Observe the relative distance from teh beak tip to the flower
        // 1 observation
        sensor.AddObservation(toFlower.magnitude / FlowerArea.AreaDiameter);

        // 10 Total observations
    }

    /// <summary>
    /// When Behaviour Type is set to "Heuristic Only" on the agent's behaviour Parameters,
    /// this function will be called. Its return values will be fed into
    /// ><see cref="OnActionReceived(ActionBuffers)"/>
    /// </summary>
    /// <param name="actionsOut"></param>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continousActions = actionsOut.ContinuousActions;

        Vector3 forward = Vector3.zero;
        Vector3 left = Vector3.zero;
        Vector3 up = Vector3.zero;
        float pitch = 0f;
        float yaw = 0f;

        // Forward/Backward
        if (Input.GetKey(KeyCode.W)) forward = transform.forward;
        else if (Input.GetKey(KeyCode.S)) forward = -transform.forward;

        // Left/Right
        if (Input.GetKey(KeyCode.A)) left = -transform.right;
        else if (Input.GetKey(KeyCode.D)) left = transform.right;

        // Up/Down
        if (Input.GetKey(KeyCode.E)) up = transform.up;
        else if (Input.GetKey(KeyCode.C)) up = -transform.up;

        // Pitch Up/Down
        if (Input.GetKey(KeyCode.UpArrow)) pitch = -1f;
        else if (Input.GetKey(KeyCode.DownArrow)) pitch = 1f;

        // Turn left/right
        if (Input.GetKey(KeyCode.LeftArrow)) yaw = -1f;
        else if (Input.GetKey(KeyCode.RightArrow)) yaw = 1f;

        // Combine the movement vectors and normalise
        Vector3 combined = (forward + left + up).normalized;

        continousActions[0] = combined.x;
        continousActions[1] = combined.y;
        continousActions[2] = combined.z;
        continousActions[3] = pitch;
        continousActions[4] = yaw;
    }

    /// <summary>
    /// Prevent the agent from moving and taking actions
    /// </summary>
    public void FreezeAgent()
    {
        UnityEngine.Debug.Assert(trainingMode == false, "Freeze/Unfreeze not suported in training");
        frozen = true;
        rigidbody.Sleep();
    }

    /// <summary>
    /// Allow the agent to move and take actions
    /// </summary>
    public void UnfreezeAgent()
    {
        UnityEngine.Debug.Assert(trainingMode == false, "Freeze/Unfreeze not suported in training");
        frozen = false;
        rigidbody.WakeUp();
    }

    /// <summary>
    /// Move the agent to a sage random position (i.e. does not collider with anything
    /// If in front of a flower, also point the beak at the flowe
    /// </summary>
    /// <param name="inFrontOfFlower">Whether to choose a spot in front of a flower</param>
    private void MoveToSafeRandomPosition(bool inFrontOfFlower)
    {
        bool safePositionFound = false;
        int attemptsRemaining = 100;
        Vector3 potentialPosition = Vector3.zero;
        Quaternion potentialRotation = Quaternion.identity;

        // Loop until a safe position is found for we run out of attempts
        while (!safePositionFound && attemptsRemaining > 0)
        {
            attemptsRemaining--;
            if (inFrontOfFlower)
            {
                // Pick a random flower
                Flower randomFlower = flowerArea.Flowers[Random.Range(0, flowerArea.Flowers.Count)];

                // Position 10-20cm in front of the flower
                float distanceFromFlower = Random.Range(0.1f, 0.2f);
                potentialPosition = randomFlower.transform.position + randomFlower.FlowerUpVector * distanceFromFlower;

                // Point beak at flower (bird's head is centre of transform)
                Vector3 toFlower = randomFlower.FlowerCenterPosition - potentialPosition;
                potentialRotation = Quaternion.LookRotation(toFlower, Vector3.up);
            }
            else
            {
                // Pick a random height from the ground
                float height = Random.Range(1.2f, 2.5f);

                // Pick a random radius from the centre of the area
                float radius = Random.Range(2f, 7f);

                // Pick a random direction rotated around the y-axis
                Quaternion direction = Quaternion.Euler(0f, Random.Range(-180f, 180f), 0f);

                // Combine height, radius and direction to pick a potential position
                potentialPosition = flowerArea.transform.position + Vector3.up * height + direction * Vector3.forward * radius;

                // Chose and set arandom starting pitch and yaw 
                float pitch = Random.Range(-60f, 60f);
                float yaw = Random.Range(-180f, 180f);

                potentialRotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            // Check to see if the agent will collide with anything
            Collider[] colliders = Physics.OverlapSphere(potentialPosition, 0.05f);

            safePositionFound = colliders.Length == 0;
        }

        Debug.Assert(safePositionFound, "Could not find a safe position to spawn");
        // set position/rotation
        transform.position = potentialPosition;
        transform.rotation = potentialRotation;
    }

    /// <summary>
    /// Updates the nearest flower to the agent
    /// </summary>
    private void UpdateNearestFlower()
    {
        foreach (Flower flower in flowerArea.Flowers)
        {
            if (nearestFlower == null && flower.HasNectar)
            {
                // No current nearest flower and this flower has nectar so set to this flower
                nearestFlower = flower;
                break;
            }
            else if (flower.HasNectar)
            {
                // Claculate distance to this flower and distance to teh current nearest flower
                float distanceToFlower = Vector3.Distance(flower.transform.position, beakTip.position);
                float distanceToCurrentNearestFlower = Vector3.Distance(nearestFlower.transform.position, beakTip.position);

                // If current neearest flowe is empty OR this flower is closer, update the nearest flower
                if (!nearestFlower.HasNectar || distanceToFlower < distanceToCurrentNearestFlower)
                {
                    nearestFlower = flower;
                }
            }
        }
    }

    /// <summary>
    /// Called when agent's collider enters a trigger collider
    /// </summary>
    /// <param name="other">The trigger Collider</param>
    private void OnTriggerEnter(Collider other)
    {
        TriggerEnterOrStay(other);
    }

    /// <summary>
    /// Called while agent's collider stays inside a trigger collider
    /// </summary>
    /// <param name="other"></param>
    private void OnTriggerStay(Collider other)
    {
        TriggerEnterOrStay(other);
    }

    /// <summary>
    /// Handles when the agent's colider enters or stays in a trigger collider
    /// </summary>
    /// <param name="collider"></param>
    private void TriggerEnterOrStay(Collider collider)
    {
        // Check if agent is colliding with nectar
        if (collider.CompareTag("nectar"))
        {
            Vector3 closestPointToBeakTip = collider.ClosestPoint(beakTip.position);

            // Check if the closest collider point is close to the beak tip
            // Note: a collision with anything but the beak tip should not count
            if (Vector3.Distance(beakTip.position, closestPointToBeakTip) < BeakTipRadius)
            {
                // Look up the flower for this nectar collider
                Flower flower = flowerArea.GetFlowerFromNectar(collider);

                // Attempt to take 0.01 nectar 
                // Note this is per fixed time step, meaning it happens every 0.02 seconds, or 50x a second
                float nectarReceived = flower.Feed(0.01f);

                // Keep track of nectar obtained
                NectarObtained += nectarReceived;

                if (trainingMode)
                {
                    // Calculate reward for getting nectar
                    float bonus = 0.02f * Mathf.Clamp01(Vector3.Dot(transform.forward.normalized, -nearestFlower.FlowerUpVector.normalized));
                    AddReward(0.01f + bonus);
                }

                // If flower is empty, update the nearest flower
                if (!flower.HasNectar) UpdateNearestFlower();
            }
        }
    }

    /// <summary>
    /// Called when the agent collides with something solid (not a trigger)
    /// </summary>
    /// <param name="collision">The collision info</param>
    private void OnCollisionEnter(Collision collision)
    {
        if (trainingMode && collision.collider.CompareTag("boundary"))
        {
            // Collided with the area bounary, give a negative reward
            AddReward(-0.5f);
        }
    }

    //Called every frame
    private void Update()
    {
        // Draw a line from the beaktip to the nearest flower
        if (nearestFlower != null)
        {
            Debug.DrawLine(beakTip.position, nearestFlower.FlowerCenterPosition, Color.green);
        }
    }

    private void FixedUpdate()
    {
        // Avoid scenario where nearest flower is stolen by opponent and not updated
        if (nearestFlower != null && !nearestFlower.HasNectar)
        {
            UpdateNearestFlower();
        }
    }
}
