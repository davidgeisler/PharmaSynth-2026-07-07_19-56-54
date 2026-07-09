using UnityEngine;

[RequireComponent(typeof(LiquidPhysics))]
public class LiquidPourer : MonoBehaviour
{
    [Header("Setup")]
    public Transform spout;
    public LineRenderer streamLine;

    [Header("Settings")]
    public float pourThreshold = 45f;
    public float maxFlowRate = 35f;   // ml/s at full inversion (finite ~150 ml bottles must survive 2-3 pours)

    [Header("Stream Animation")]
    public int streamSegments = 10;
    public float streamDropStrength = 0.75f;
    public float flowSmoothingSpeed = 8f;
    public float minStreamWidth = 0.0025f;
    public float maxStreamWidth = 0.015f;

    [Header("Hazard Settings")]
    [Tooltip("Drag your AcidSpill prefab here")]
    public GameObject acidSpillPrefab;
    [Tooltip("Minimum time between spawning spills (prevents lag)")]
    public float spillCooldown = 1.0f;

    private LiquidPhysics sourceContainer;
    private float lastSpillTime = 0f;
    private float smoothedFlow01;

    void Start()
    {
        sourceContainer = GetComponent<LiquidPhysics>();

        if (streamLine)
        {
            streamLine.positionCount = Mathf.Max(2, streamSegments);
            streamLine.enabled = false;
        }
    }

    void Update()
    {
        float tiltAngle = Vector3.Angle(Vector3.up, transform.up);

        if (tiltAngle > pourThreshold && sourceContainer.currentLiquidVolume > 0)
        {
            Pour(tiltAngle);
        }
        else
        {
            smoothedFlow01 = Mathf.Lerp(smoothedFlow01, 0f, Time.deltaTime * flowSmoothingSpeed);
            if (streamLine) streamLine.enabled = false;
        }
    }

    void Pour(float currentTilt)
    {
        // Calculate Amount
        float tiltDelta = Mathf.InverseLerp(pourThreshold, 180f, currentTilt);
        smoothedFlow01 = Mathf.Lerp(smoothedFlow01, tiltDelta, Time.deltaTime * flowSmoothingSpeed);
        float currentFlowRate = maxFlowRate * tiltDelta;
        float amountToPour = currentFlowRate * Time.deltaTime;

        // Visuals
        if (streamLine)
        {
            streamLine.enabled = true;

            if (sourceContainer.currentChemical != null)
            {
                Color c = sourceContainer.currentChemical.liquidColor;
                streamLine.startColor = c;
                streamLine.endColor = c;
            }

            float width = Mathf.Lerp(minStreamWidth, maxStreamWidth, smoothedFlow01);
            streamLine.startWidth = width;
            streamLine.endWidth = width * 0.65f;
        }

        // Raycast Physics
        RaycastHit hit;
        if (Physics.Raycast(spout.position, Vector3.down, out hit, 2.0f))
        {
            if (streamLine) DrawCurvedStream(spout.position, hit.point, smoothedFlow01);

            LiquidPhysics target = hit.collider.GetComponentInParent<LiquidPhysics>();

            if (target != null)
            {
                // TRANSFER LOGIC (Pouring into another flask)
                ChemicalData pouredLiquid = sourceContainer.PourOut(amountToPour);
                if (pouredLiquid != null) target.AddLiquid(pouredLiquid, amountToPour);
            }
            else
            {
                // 1. Check if the chemical causes a hazard
                // (Make sure your Chemical Data name actually contains "H2SO4")
                if (sourceContainer.currentChemical != null && sourceContainer.currentChemical.isDangerous)
                {
                    SpawnHazard(hit.point, hit.normal);
                }

                // 2. Waste the liquid
                sourceContainer.PourOut(amountToPour);
            }
        }
        else
        {
            // Poured into void
            if (streamLine) DrawCurvedStream(spout.position, spout.position + Vector3.down * 0.5f, smoothedFlow01);
            sourceContainer.PourOut(amountToPour);
        }
    }

    void DrawCurvedStream(Vector3 start, Vector3 end, float flow01)
    {
        if (streamLine == null)
            return;

        int segments = Mathf.Max(2, streamSegments);
        if (streamLine.positionCount != segments)
            streamLine.positionCount = segments;

        Vector3 middle = (start + end) * 0.5f + Vector3.down * Mathf.Lerp(0.02f, streamDropStrength, flow01);

        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);
            Vector3 p0 = Vector3.Lerp(start, middle, t);
            Vector3 p1 = Vector3.Lerp(middle, end, t);
            Vector3 point = Vector3.Lerp(p0, p1, t);
            streamLine.SetPosition(i, point);
        }
    }

    void SpawnHazard(Vector3 hitPoint, Vector3 hitNormal)
    {
        // Don't spawn if we just spawned
        if (Time.time - lastSpillTime < spillCooldown) return;
        lastSpillTime = Time.time;

        if (acidSpillPrefab != null)
        {
            Vector3 spawnPos = hitPoint + (hitNormal * 0.1f);
            Quaternion spawnRot = Quaternion.FromToRotation(Vector3.up, hitNormal);
            Instantiate(acidSpillPrefab, spawnPos, spawnRot);
        }
    }
}