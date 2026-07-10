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

    // Procedural falling-liquid stream + landing puddles (user 2026-07-10: pouring/
    // spilling showed no visible liquid). Independent of the optional LineRenderer.
    private ParticleSystem _pourStream;
    private float _lastPuddle;
    private Transform Mouth => spout != null ? spout : transform;

    // Continuous positional pour sound (realism 2026-07-10: pouring was silent).
    private AudioSource pourAudio;
    private float pourBaseVol = 0.5f;

    /// Pure, testable pour-volume curve: silent when not pouring, otherwise the
    /// clip's base × the Sfx category volume, floored so a trickle is still heard.
    public static float PourVolume(bool pouring, float baseVol, float catVol, float flow01)
        => pouring ? Mathf.Clamp01(baseVol) * Mathf.Clamp01(catVol) * Mathf.Clamp01(0.3f + Mathf.Clamp01(flow01) * 0.7f) : 0f;

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
            UpdatePourAudio(true, smoothedFlow01);
            UpdatePourStream(true, smoothedFlow01);
        }
        else
        {
            smoothedFlow01 = Mathf.Lerp(smoothedFlow01, 0f, Time.deltaTime * flowSmoothingSpeed);
            if (streamLine) streamLine.enabled = false;
            UpdatePourAudio(false, 0f);
            UpdatePourStream(false, 0f);
        }
    }

    /// Drive the looping 3D pour source: swell with flow while pouring, fade to
    /// silence when the vessel is righted. Runtime-only; no-op without an AudioService.
    void UpdatePourAudio(bool pouring, float flow01)
    {
        if (!Application.isPlaying) return;
        if (pourAudio == null)
        {
            if (!pouring) return;                          // don't allocate a source until actually pouring
            if (AudioService.Instance == null) return;
            var e = AudioService.Instance.EntryOf("pour");
            if (e == null || e.clip == null) return;
            pourBaseVol = Mathf.Clamp01(e.volume);
            var host = spout != null ? spout : transform;
            var go = new GameObject("PourAudio");
            go.transform.SetParent(host, false);
            pourAudio = go.AddComponent<AudioSource>();
            pourAudio.clip = e.clip;
            pourAudio.loop = true;
            pourAudio.playOnAwake = false;
            pourAudio.spatialBlend = 1f;                 // 3D positional
            pourAudio.rolloffMode = AudioRolloffMode.Linear;
            pourAudio.minDistance = 0.5f;
            pourAudio.maxDistance = 6f;
            pourAudio.volume = 0f;
        }

        float catVol = AudioService.Instance != null ? AudioService.Instance.VolumeOf(AudioCategory.Sfx) : 1f;
        float target = PourVolume(pouring, pourBaseVol, catVol, flow01);
        pourAudio.volume = Mathf.Lerp(pourAudio.volume, target, Time.deltaTime * 10f);
        if (pouring && !pourAudio.isPlaying) pourAudio.Play();
        else if (!pouring && pourAudio.isPlaying && pourAudio.volume < 0.01f) pourAudio.Stop();
    }

    /// Procedural tinted droplet stream falling from the vessel mouth while pouring —
    /// visible even when the optional LineRenderer isn't wired. Gravity does the work,
    /// so orientation is forgiving; emission scales with flow, colour tracks the liquid.
    void UpdatePourStream(bool pouring, float flow01)
    {
        if (!Application.isPlaying) return;
        if (_pourStream == null)
        {
            if (!pouring) return;
            var go = new GameObject("PourStream");
            go.transform.SetParent(Mouth, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.rotation = Quaternion.LookRotation(Vector3.down);   // emit downward
            _pourStream = go.AddComponent<ParticleSystem>();
            _pourStream.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _pourStream.main;
            main.loop = true; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.55f;
            main.startSpeed = 0.45f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.016f);
            main.gravityModifier = 2.6f;
            main.maxParticles = 160;
            var em = _pourStream.emission; em.rateOverTime = 0f;
            var sh = _pourStream.shape;
            sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 5f; sh.radius = 0.004f;
            var r = _pourStream.GetComponent<ParticleSystemRenderer>();
            r.material = EffectVfx.ParticleMaterial();
            r.sortingOrder = 10;
            _pourStream.Play();
        }
        var emis = _pourStream.emission;
        emis.rateOverTime = pouring ? Mathf.Lerp(22f, 75f, Mathf.Clamp01(flow01)) : 0f;
        if (pouring)
        {
            var main = _pourStream.main;
            Color c = sourceContainer != null && sourceContainer.currentChemical != null
                ? sourceContainer.currentChemical.liquidColor : new Color(0.6f, 0.75f, 0.9f);
            c.a = 1f;
            main.startColor = c;
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
        if (Physics.Raycast(Mouth.position, Vector3.down, out hit, 2.0f))
        {
            if (streamLine) DrawCurvedStream(Mouth.position, hit.point, smoothedFlow01);

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

                // 2. Waste the liquid — and leave a growing wet puddle where it lands.
                sourceContainer.PourOut(amountToPour);
                if (Application.isPlaying && Time.time - _lastPuddle > 0.6f)
                {
                    _lastPuddle = Time.time;
                    Color pc = sourceContainer.currentChemical != null
                        ? sourceContainer.currentChemical.liquidColor : new Color(0.55f, 0.7f, 0.85f);
                    SpillPuddle.Spawn(hit.point + Vector3.up * 0.02f, pc, 0.10f);
                }
            }
        }
        else
        {
            // Poured into void
            if (streamLine) DrawCurvedStream(Mouth.position, Mouth.position + Vector3.down * 0.5f, smoothedFlow01);
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