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

    // Pour assist (W5.12, user: "I still can't pour into vials/tubes/beakers —
    // the beaker won't catch the spills"): VR aim is imprecise, so when the
    // precise ray misses, a forgiving sphere sweep catches a receiver whose
    // mouth is within this radius of the stream line.
    [Header("Pour Assist (W5.12)")]
    public float assistRadius = 0.045f;

    /// Dev toggle (DevExperimentDriver 'P'): shows what each PourTick actually
    /// hits so in-headset misses can be diagnosed live.
    public static bool DebugOverlay;
    private float _lastDebugText;

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

    /// Edit-mode/test seam (Start doesn't run on AddComponent in edit mode).
    public void Bind(LiquidPhysics source) => sourceContainer = source;

    void Update()
    {
        float tiltAngle = Vector3.Angle(Vector3.up, transform.up);

        if (tiltAngle > pourThreshold && sourceContainer != null && sourceContainer.currentLiquidVolume > 0)
        {
            EnsureStreamLine();
            PourTick(Time.deltaTime, tiltAngle);
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
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
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
        emis.rateOverTime = pouring ? Mathf.Lerp(35f, 110f, Mathf.Clamp01(flow01)) : 0f;
        if (pouring)
        {
            var main = _pourStream.main;
            Color c = sourceContainer != null && sourceContainer.currentChemical != null
                ? sourceContainer.currentChemical.liquidColor : new Color(0.6f, 0.75f, 0.9f);
            c.a = 1f;
            main.startColor = c;
        }
    }

    /// The connected pour ARC (DrawCurvedStream) previously rendered only on
    /// bottles whose optional LineRenderer someone had authored — which was none
    /// of them (user 2026-07-10: pours to the ground still hard to see). Build it
    /// on first pour so every pourer shows a continuous mouth-to-surface stream
    /// on top of the droplet particles.
    void EnsureStreamLine()
    {
        if (streamLine != null || !Application.isPlaying) return;
        var go = new GameObject("StreamLine");
        go.transform.SetParent(Mouth, false);
        streamLine = go.AddComponent<LineRenderer>();
        streamLine.useWorldSpace = true;
        streamLine.positionCount = Mathf.Max(2, streamSegments);
        streamLine.startWidth = minStreamWidth;
        streamLine.endWidth = minStreamWidth * 0.65f;
        streamLine.numCapVertices = 4;
        streamLine.material = EffectVfx.ParticleMaterial();
        streamLine.sortingOrder = 10;
        streamLine.enabled = false;
    }

    /// Pick the receiving vessel from a downward raycast set (W5.8 pour fix).
    /// Skips the source's own colliders (a tilted bottle's ray often exits
    /// through its own body — the old single-raycast could hit itself, fire
    /// LiquidAdded on itself, and even complete pour tasks by self-tilting).
    /// The nearest remaining hit decides: a LiquidPhysics there = the transfer
    /// target; anything else = the waste/puddle surface. Static + real-collider
    /// friendly so the self-test suite can exercise it in edit mode.
    public static LiquidPhysics ResolveTarget(RaycastHit[] hits, LiquidPhysics source, out RaycastHit firstHit)
    {
        firstHit = default;
        float bestDist = float.MaxValue;
        Collider bestCol = null;
        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i].collider;
            if (col == null) continue;
            // Own hierarchy (IsChildOf includes self). NOT transform.root — stage
            // props and vessels share the DynamicStage root, and shelf bottles
            // share the ReagentShelf root, so a root check would skip real targets.
            if (source != null && col.transform.IsChildOf(source.transform)) continue;
            if (hits[i].distance < bestDist)
            {
                bestDist = hits[i].distance;
                bestCol = col;
                firstHit = hits[i];
            }
        }
        if (bestCol == null) return null;
        var lp = bestCol.GetComponentInParent<LiquidPhysics>();
        return (lp != null && lp != source) ? lp : null;
    }

    /// One pour step (extracted from Update so edit-mode tests can drive a real
    /// transfer deterministically). Triggers are ignored — station sensor columns,
    /// socket spheres and hot-surface zones must never swallow the stream.
    public void PourTick(float dt, float currentTilt)
    {
        if (sourceContainer == null) sourceContainer = GetComponent<LiquidPhysics>();
        if (sourceContainer == null) return;

        // Calculate Amount
        float tiltDelta = Mathf.InverseLerp(pourThreshold, 180f, currentTilt);
        smoothedFlow01 = Mathf.Lerp(smoothedFlow01, tiltDelta, dt * flowSmoothingSpeed);
        float currentFlowRate = maxFlowRate * tiltDelta;
        float amountToPour = currentFlowRate * dt;

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

        // Raycast Physics: precise ray first, then the forgiving sphere sweep —
        // the sweep also catches a receiver already overlapping the mouth
        // (distance-0 hits), which the thin ray can slip past entirely.
        var hits = Physics.RaycastAll(Mouth.position, Vector3.down, 2.0f, ~0, QueryTriggerInteraction.Ignore);
        RaycastHit hit;
        LiquidPhysics target = ResolveTarget(hits, sourceContainer, out hit);
        if (target == null)
        {
            var sweep = Physics.SphereCastAll(Mouth.position, assistRadius, Vector3.down, 2.0f, ~0,
                                              QueryTriggerInteraction.Ignore);
            RaycastHit sweepHit;
            var sweepTarget = ResolveTarget(sweep, sourceContainer, out sweepHit);
            if (sweepTarget != null)
            {
                target = sweepTarget;
                // Distance-0 overlap hits report a degenerate point — land the
                // visual arc on the vessel instead so it matches the transfer.
                hit = sweepHit;
                if (hit.distance <= 0f)
                {
                    hit.point = target.transform.position;
                    hit.normal = Vector3.up;
                }
            }
        }
        if (DebugOverlay && Application.isPlaying && Time.time - _lastDebugText > 0.5f)
        {
            _lastDebugText = Time.time;
            FloatingText.Show(
                "pour hit: " + (hit.collider != null ? hit.collider.name : "—")
                + "\ntarget: " + (target != null ? target.name : "none"),
                Mouth.position + Vector3.up * 0.06f,
                target != null ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.6f, 0.5f), 0.8f);
        }
        if (hit.collider != null)
        {
            if (streamLine) DrawCurvedStream(Mouth.position, hit.point, smoothedFlow01);

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