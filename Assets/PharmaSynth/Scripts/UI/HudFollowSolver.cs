using UnityEngine;

/// Pure math for a lazy-follow VR HUD: the canvas hovers a fixed distance in front
/// of the head and only re-centres when the head strays outside a yaw/position
/// deadzone — the accepted comfort pattern for "screen-anchored" HUDs (a hard
/// head-lock is nauseating in VR). Static + stateless so it is edit-mode testable.
public static class HudFollowSolver
{
    [System.Serializable]
    public struct Params
    {
        public float distance;        // metres in front of the head
        public float heightOffset;    // metres relative to head height
        public float yawDeadzoneDeg;  // ignore head yaw wander inside this cone
        public float posDeadzone;     // ignore head drift inside this radius
        public float smoothTime;      // SmoothDamp time constant

        public static Params Default => new Params
        {
            distance = 1.45f,
            heightOffset = -0.08f,
            yawDeadzoneDeg = 22f,
            posDeadzone = 0.25f,
            smoothTime = 0.35f,
        };
    }

    public struct State
    {
        public Vector3 pos;    // rig world position
        public float yawDeg;   // rig world yaw
        public Vector3 vel;    // SmoothDamp scratch
        public float yawVel;
        public bool chasing;   // currently converging toward the anchor
    }

    /// Wrapped yaw delta in [-180, 180].
    public static float DeltaYawDeg(float fromDeg, float toDeg)
        => Mathf.DeltaAngle(fromDeg, toDeg);

    /// Where the rig wants to sit for a given head pose.
    public static Vector3 AnchorPoint(Vector3 headPos, float headYawDeg, in Params p)
    {
        var fwd = Quaternion.Euler(0f, headYawDeg, 0f) * Vector3.forward;
        return headPos + fwd * p.distance + Vector3.up * p.heightOffset;
    }

    /// True when the head has wandered far enough that the rig should re-centre.
    public static bool OutsideDeadzone(in State s, Vector3 headPos, float headYawDeg, in Params p)
    {
        if (Mathf.Abs(DeltaYawDeg(s.yawDeg, headYawDeg)) > p.yawDeadzoneDeg) return true;
        // Positional drift is measured against the rig's CURRENT yaw so that yaw
        // wander inside its own deadzone doesn't leak into the position check.
        Vector3 anchor = AnchorPoint(headPos, s.yawDeg, in p);
        Vector3 flat = anchor - s.pos; flat.y = 0f;
        return flat.magnitude > p.posDeadzone
               || Mathf.Abs(anchor.y - s.pos.y) > p.posDeadzone;
    }

    /// Advance one frame. Inside the deadzone (and not mid-chase) the rig holds
    /// still; once outside it SmoothDamps toward the anchor until it arrives.
    public static void Step(ref State s, Vector3 headPos, float headYawDeg, in Params p, float dt)
    {
        if (!s.chasing && !OutsideDeadzone(in s, headPos, headYawDeg, in p)) return;

        s.chasing = true;
        Vector3 anchor = AnchorPoint(headPos, headYawDeg, in p);
        s.pos = Vector3.SmoothDamp(s.pos, anchor, ref s.vel, p.smoothTime, Mathf.Infinity, dt);
        s.yawDeg = Mathf.SmoothDampAngle(s.yawDeg, headYawDeg, ref s.yawVel, p.smoothTime, Mathf.Infinity, dt);

        // Arrived → stop chasing so small head wander doesn't jiggle the HUD.
        if ((anchor - s.pos).magnitude < 0.01f && Mathf.Abs(DeltaYawDeg(s.yawDeg, headYawDeg)) < 0.5f)
        {
            s.pos = anchor;
            s.yawDeg = headYawDeg;
            s.vel = Vector3.zero; s.yawVel = 0f;
            s.chasing = false;
        }
    }

    /// Snap directly onto the anchor (scene setup, post-teleport).
    public static State Snapped(Vector3 headPos, float headYawDeg, in Params p) => new State
    {
        pos = AnchorPoint(headPos, headYawDeg, in p),
        yawDeg = headYawDeg,
        vel = Vector3.zero,
        yawVel = 0f,
        chasing = false,
    };
}
