using UnityEngine;

/// Gentle hover animation for a floating character/prop (Pharmee): sinusoidal
/// vertical bob plus a slight sway tilt, around the object's authored pose.
/// Purely local-space so it composes with any parent motion.
public class FloatBob : MonoBehaviour
{
    [SerializeField] private float bobAmplitude = 0.055f;
    [SerializeField] private float bobSpeed = 1.6f;
    [SerializeField, Range(0f, 8f)] private float swayDegrees = 2.5f;
    [Tooltip("Turn OFF when a FaceCamera controls this object's rotation (e.g. Pharmee looking at the player), so the two don't fight.")]
    [SerializeField] private bool applyRotation = true;

    public void SetApplyRotation(bool on) => applyRotation = on;

    private Vector3 _homePos;
    private Quaternion _homeRot;
    private float _phase;

    private void Awake()
    {
        _homePos = transform.localPosition;
        _homeRot = transform.localRotation;
        _phase = Random.value * 10f;   // desync multiple floaters
    }

    private void Update()
    {
        float t = Time.time + _phase;
        transform.localPosition = _homePos + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobAmplitude);
        if (applyRotation)
            transform.localRotation = _homeRot * Quaternion.Euler(
                Mathf.Sin(t * bobSpeed * 0.7f) * swayDegrees,
                0f,
                Mathf.Cos(t * bobSpeed * 0.5f) * swayDegrees * 0.6f);
    }
}
