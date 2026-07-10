using System.Collections.Generic;
using UnityEngine;

/// Thin driver for ProctorRoamModel on Dr. Jimenez: walks him between his home
/// post and the observation points (shelf/benches), faces the walk direction, plays
/// the Walk animator state while moving, and glances at the point (or the player)
/// while observing. Roaming pauses (he returns home) from the moment an experiment
/// FINISHES until the next one starts — home is where he proctors the quiz.
public class ProctorRoamer : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Animator animator;                 // Jimenez.controller
    [SerializeField] private ExperimentRunner runner;           // finish → walk home
    [SerializeField] private List<Transform> observationPoints = new List<Transform>();
    [SerializeField] private string walkBool = "Walking";       // animator param (bool)

    [Header("Tuning")]
    [SerializeField] private float walkSpeed = 0.7f;
    [SerializeField] private float turnSpeed = 5f;
    [SerializeField] private float arriveDistance = 0.15f;
    [SerializeField] private float idleMin = 12f;
    [SerializeField] private float idleMax = 28f;
    [SerializeField] private float observeSeconds = 5f;

    private ProctorRoamModel _model;
    private Vector3 _home;
    private Quaternion _homeRot;
    private bool _allowRoam = true;
    private bool _subscribed;
    private bool _hasWalkParam;
    private CharacterController _cc;    // walls stop him; players can't pass through him

    public ProctorRoamModel Model => _model;

    /// Edit-mode/test seam.
    public void Bind(Animator a, ExperimentRunner r, List<Transform> points)
    {
        Unsubscribe();
        animator = a; runner = r;
        observationPoints = points ?? new List<Transform>();
        Subscribe();
        EnsureModel();
    }

    private void EnsureModel()
    {
        if (_model == null)
            _model = new ProctorRoamModel(observationPoints.Count, idleMin, idleMax, observeSeconds,
                (uint)(name.GetHashCode() | 1));
    }

    private void Start()
    {
        _home = transform.position;
        _homeRot = transform.rotation;
        EnsureModel();
        _hasWalkParam = HasBool(animator, walkBool);
        _cc = GetComponent<CharacterController>();
        Subscribe();
    }

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (_subscribed || runner == null) return;
        runner.ExperimentStarted += OnStarted;
        runner.ExperimentFinished += OnFinished;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || runner == null) return;
        runner.ExperimentStarted -= OnStarted;
        runner.ExperimentFinished -= OnFinished;
        _subscribed = false;
    }

    private void OnStarted(ExperimentModuleDefinition _) => _allowRoam = true;

    /// Quiz/grade time — come home and stay put until the next run.
    private void OnFinished(ExperimentResult _) => _allowRoam = false;

    private static bool HasBool(Animator a, string param)
    {
        if (a == null || a.runtimeAnimatorController == null) return false;
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Bool && p.name == param) return true;
        return false;
    }

    private void Update()
    {
        if (_model == null) return;
        Vector3 target = CurrentTarget();
        bool arrived = Flat(transform.position - target).magnitude <= arriveDistance;
        _model.Tick(Time.deltaTime, _allowRoam, arrived);

        bool walking = _model.IsWalking && !arrived;
        if (walking) MoveToward(target);
        else if (_model.Current == ProctorRoamModel.Phase.Observing) FaceToward(LookTarget());
        else if (_model.Current == ProctorRoamModel.Phase.AtHome)
            transform.rotation = Quaternion.Slerp(transform.rotation, _homeRot, turnSpeed * Time.deltaTime);

        if (_hasWalkParam) animator.SetBool(walkBool, walking);
    }

    private Vector3 CurrentTarget()
    {
        if (_model.Current == ProctorRoamModel.Phase.WalkingOut || _model.Current == ProctorRoamModel.Phase.Observing)
        {
            int i = _model.TargetIndex;
            if (i >= 0 && i < observationPoints.Count && observationPoints[i] != null)
                return observationPoints[i].position;
        }
        return _home;
    }

    private Vector3 LookTarget()
    {
        // Observing: half the time watch the PLAYER (proctor checking on you).
        var cam = Camera.main;
        if (cam != null && (_model.TargetIndex % 2 == 0)) return cam.transform.position;
        return CurrentTarget();
    }

    private void MoveToward(Vector3 target)
    {
        Vector3 to = Flat(target - transform.position);
        if (to.sqrMagnitude < 1e-6f) return;
        FaceToward(target);
        Vector3 step = to.normalized * walkSpeed * Time.deltaTime;
        if (_cc != null && _cc.enabled) _cc.Move(step);   // collides with walls/furniture
        else transform.position += step;
    }

    private void FaceToward(Vector3 worldPoint)
    {
        Vector3 dir = Flat(worldPoint - transform.position);
        if (dir.sqrMagnitude < 1e-6f) return;
        var want = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, turnSpeed * Time.deltaTime);
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
