using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(MeshFilter))]
public class LiquidPhysics : MonoBehaviour
{
    // Chemistry is reported as events; experiment logic (task completion, wrong-reagent
    // grading) lives in bindings that know the current context — not hardcoded here.
    public event Action<ChemicalData, float> LiquidAdded;          // (chemical, amount)
    public event Action<ReactionRule> ReactionOccurred;            // a registered reaction fired
    public event Action<ChemicalData, ChemicalData> WrongReagentMixed; // (current, incoming) with no rule

    [Header("Components")]
    public Renderer mainRenderer;
    public Renderer precipitateRenderer;

    [Header("Volume Settings")]
    public float maxVolume = 1000f;
    public float currentLiquidVolume = 500f;
    public float currentPptVolume = 0f;
    public float HorizonalFloatAdj = 0.13f;

    [Header("Chemical Content")]
    public ChemicalData currentChemical;
    public ChemicalData currentPptChemical;
    public ReactionRegistry registry;

    [Header("Visual Smoothness")]
    public float colorChangeSpeed = 2.0f;

    private Coroutine liquidChangeRoutine;
    private Coroutine pptChangeRoutine;

    [Header("Wobble Settings")]
    public float MaxWobble = 0.03f;
    public float WobbleSpeed = 1f;
    public float Recovery = 1f;

    private const float MinMovementThreshold = 0.001f;

    // Internal variables
    private Mesh mesh;

    // Shader Property IDs
    private static readonly int FillID = Shader.PropertyToID("_Fill");
    private static readonly int LiquidColorID = Shader.PropertyToID("_LiquidColour");
    private static readonly int SceneColorAmtID = Shader.PropertyToID("_SceneColourAmount");
    private static readonly int UpVectorID = Shader.PropertyToID("_UpVector");
    private static readonly int LocalYMinID = Shader.PropertyToID("_LocalYMin");
    private static readonly int LocalYMaxID = Shader.PropertyToID("_LocalYMax");
    private static readonly int WobbleXID = Shader.PropertyToID("_WobbleX");
    private static readonly int WobbleZID = Shader.PropertyToID("_WobbleZ");

    // Wobble Physics Variables
    private Vector3 lastPos;
    private Vector3 lastRot;
    private float wobbleAmountX;
    private float wobbleAmountZ;
    private float wobbleAmountToAddX;
    private float wobbleAmountToAddZ;
    private float pulse;
    private float time = 0.5f;

    // State
    private bool isWobbling = true; // Start active to settle initial state

    void Start()
    {
        if (mainRenderer == null) mainRenderer = GetComponent<Renderer>();
        mesh = GetComponent<MeshFilter>().mesh;

        SendMeshBounds();
        UpdateAllVisuals();

        lastPos = transform.position;
        lastRot = transform.rotation.eulerAngles;
    }

    void SendMeshBounds()
    {
        if (mesh == null) return;
        Bounds bounds = mesh.bounds;

        if (mainRenderer != null)
        {
            mainRenderer.material.SetFloat(LocalYMinID, bounds.min.y);
            mainRenderer.material.SetFloat(LocalYMaxID, bounds.max.y);
        }
        if (precipitateRenderer != null)
        {
            precipitateRenderer.material.SetFloat(LocalYMinID, bounds.min.y);
            precipitateRenderer.material.SetFloat(LocalYMaxID, bounds.max.y);
        }
    }

    void Update()
    {
        // 1. Clamp Volumes
        currentLiquidVolume = Mathf.Clamp(currentLiquidVolume, 0, maxVolume);
        currentPptVolume = Mathf.Clamp(currentPptVolume, 0, maxVolume - currentLiquidVolume);

        // 2. Calculate Fill & Tilt (Must run every frame for rotation accuracy)
        UpdateFillPhysics();

        // 3. WOBBLE PHYSICS
        // First, calculate movement speed
        Vector3 currentPos = transform.position;
        Vector3 currentRot = transform.rotation.eulerAngles;

        Vector3 velocity = (lastPos - currentPos) / Time.deltaTime;
        Vector3 angularVelocity = currentRot - lastRot;

        // Check if we are moving enough to matter (using sqrMagnitude is faster)
        bool isMoving = velocity.sqrMagnitude > MinMovementThreshold || angularVelocity.sqrMagnitude > MinMovementThreshold;

        // Check if we still have leftover wobble energy
        bool hasWobbleEnergy = Mathf.Abs(wobbleAmountToAddX) > MinMovementThreshold || Mathf.Abs(wobbleAmountToAddZ) > MinMovementThreshold;

        if (isMoving || hasWobbleEnergy || isWobbling)
        {
            UpdateWobble(velocity, angularVelocity);

            if (!isMoving && !hasWobbleEnergy)
            {
                isWobbling = false;
                // Force Zero one last time to ensure it looks perfect
                if (mainRenderer)
                {
                    mainRenderer.material.SetFloat(WobbleXID, 0);
                    mainRenderer.material.SetFloat(WobbleZID, 0);
                }
            }
            else
            {
                isWobbling = true;
            }
        }

        // Update history for next frame
        lastPos = currentPos;
        lastRot = currentRot;
    }

    void UpdateFillPhysics()
    {
        float liquidFill = currentLiquidVolume / maxVolume;
        float pptFill = currentPptVolume / maxVolume;

        // Tilt Correction
        float tilt = Mathf.Abs(Vector3.Dot(transform.up, Vector3.up));
        float correction = Mathf.Lerp(HorizonalFloatAdj, 1.0f, tilt);

        // Apply to Shader
        if (mainRenderer) mainRenderer.material.SetFloat(FillID, liquidFill * correction);
        if (precipitateRenderer) precipitateRenderer.material.SetFloat(FillID, pptFill * correction);

        // Cutoff Logic (Hide if empty)
        if (mainRenderer)
        {
            bool hasLiquid = currentLiquidVolume > 1f;
            if (mainRenderer.enabled != hasLiquid) mainRenderer.enabled = hasLiquid;
        }

        if (precipitateRenderer)
        {
            bool hasPpt = currentPptVolume > 1f;
            if (precipitateRenderer.enabled != hasPpt) precipitateRenderer.enabled = hasPpt;
        }

        Vector3 localUp = transform.InverseTransformDirection(Vector3.up);
        if (mainRenderer) mainRenderer.material.SetVector(UpVectorID, localUp);
        if (precipitateRenderer) precipitateRenderer.material.SetVector(UpVectorID, localUp);
    }

    void UpdateWobble(Vector3 velocity, Vector3 angularVelocity)
    {
        if (mainRenderer == null || !mainRenderer.enabled) return;

        time += Time.deltaTime;

        // Decay
        wobbleAmountToAddX = Mathf.Lerp(wobbleAmountToAddX, 0, Time.deltaTime * Recovery);
        wobbleAmountToAddZ = Mathf.Lerp(wobbleAmountToAddZ, 0, Time.deltaTime * Recovery);

        // Oscillate
        pulse = 2 * Mathf.PI * WobbleSpeed;
        wobbleAmountX = wobbleAmountToAddX * Mathf.Sin(pulse * time);
        wobbleAmountZ = wobbleAmountToAddZ * Mathf.Sin(pulse * time);

        // Add Velocity Impact
        wobbleAmountToAddX += Mathf.Clamp((velocity.x + (angularVelocity.z * 0.2f)) * MaxWobble, -MaxWobble, MaxWobble);
        wobbleAmountToAddZ += Mathf.Clamp((velocity.z + (angularVelocity.x * 0.2f)) * MaxWobble, -MaxWobble, MaxWobble);

        // Send to Shader
        mainRenderer.material.SetFloat(WobbleXID, wobbleAmountX);
        mainRenderer.material.SetFloat(WobbleZID, wobbleAmountZ);

        if (precipitateRenderer != null)
        {
            precipitateRenderer.material.SetFloat(WobbleXID, 0);
            precipitateRenderer.material.SetFloat(WobbleZID, 0);
        }
    }

    public void AddLiquid(ChemicalData incomingChemical, float amountToAdd)
    {
        if (incomingChemical == null)
            return;

        LiquidAdded?.Invoke(incomingChemical, amountToAdd);

        if (currentLiquidVolume + currentPptVolume + amountToAdd > maxVolume) return;

        // If waking up from empty, ensure visuals update
        if (currentLiquidVolume <= 0.1f && currentPptVolume <= 0.1f)
        {
            currentChemical = incomingChemical;
            currentLiquidVolume += amountToAdd;
            UpdateAllVisuals();
            return;
        }

        if (currentChemical == incomingChemical)
        {
            currentLiquidVolume += amountToAdd;
            return;
        }

        if (registry != null)
        {
            ReactionRule rule = registry.FindReaction(currentChemical, incomingChemical);

            if (rule != null)
            {
                if (rule.resultLiquid != null) currentChemical = rule.resultLiquid;
                if (rule.hasPrecipitate && rule.resultPrecipitate != null)
                {
                    currentPptChemical = rule.resultPrecipitate;
                    currentPptVolume += amountToAdd;
                }
                else
                {
                    currentLiquidVolume += amountToAdd;
                }
                UpdateAllVisuals(); // Update Color only on reaction
                ReactionOccurred?.Invoke(rule);
            }
            else
            {
                currentLiquidVolume += amountToAdd;
                // No registered reaction: report the mix so a context-aware binding can
                // decide whether it is actually "wrong" for the current step.
                if (currentChemical != null && incomingChemical != null && currentChemical != incomingChemical)
                    WrongReagentMixed?.Invoke(currentChemical, incomingChemical);
            }
        }
    }

    public void UpdateAllVisuals()
    {
        // 1. Handle Main Liquid Transition
        if (currentChemical != null && mainRenderer != null)
        {
            // Stop any old transition so they don't fight
            if (liquidChangeRoutine != null) StopCoroutine(liquidChangeRoutine);

            // Start the new smooth transition
            liquidChangeRoutine = StartCoroutine(LerpColor(
                mainRenderer,
                currentChemical.liquidColor,
                currentChemical.sceneColourAmount
            ));
        }

        // 2. Handle Precipitate Transition
        if (currentPptChemical != null && precipitateRenderer != null)
        {
            if (pptChangeRoutine != null) StopCoroutine(pptChangeRoutine);

            pptChangeRoutine = StartCoroutine(LerpColor(
                precipitateRenderer,
                currentPptChemical.liquidColor,
                currentPptChemical.sceneColourAmount
            ));
        }
    }

    // The Worker Function: smoothly changes color over time
    System.Collections.IEnumerator LerpColor(Renderer targetRenderer, Color targetColor, float targetSceneAmt)
    {
        // Get starting values from the material currently
        Color startColor = targetRenderer.material.GetColor(LiquidColorID);
        float startSceneAmt = targetRenderer.material.GetFloat(SceneColorAmtID);
        float t = 0;

        // Loop until t reaches 1 (100% complete)
        while (t < 1f)
        {
            t += Time.deltaTime * colorChangeSpeed;

            // Calculate intermediate values
            Color newColor = Color.Lerp(startColor, targetColor, t);
            float newAmt = Mathf.Lerp(startSceneAmt, targetSceneAmt, t);

            // Apply to shader
            targetRenderer.material.SetColor(LiquidColorID, newColor);
            targetRenderer.material.SetFloat(SceneColorAmtID, newAmt);

            yield return null; // Wait for next frame
        }
    }

    public ChemicalData PourOut(float amountToRemove)
    {
        if (currentLiquidVolume <= 0) return null;

        currentLiquidVolume -= amountToRemove;
        if (currentLiquidVolume < 0) currentLiquidVolume = 0;

        return currentChemical;
    }
}

