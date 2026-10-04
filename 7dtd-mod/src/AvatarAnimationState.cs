using System;

namespace MC7DTD
{
    // Local horizontal motion estimate. Never writes Entity Transform or network components.
    public sealed class AvatarAnimationState
    {
        public const double WalkThreshold = 0.1, RunThreshold = 5.0, StopTimeout = 0.85;
        bool initialized;
        double x, z, sampledAt;
        public string State { get; private set; } = "idle";
        public double Speed { get; private set; }
        public double VerticalSpeed { get; private set; }
        public float AnimatorSpeed => State == "running" || State == "sprinting" ? (float)Math.Min(4,1+Speed/5) : State == "walking" ? (float)Math.Max(.5,Math.Min(1,Speed/5)) : 0f;
        public float PlaybackSpeed => State == "walking" ? (float)Math.Max(.5,Math.Min(1.5,Speed/3.2)) : State == "running" || State == "sprinting" ? (float)Math.Max(.8,Math.Min(1.8,Speed/6)) : 1f;
        public bool Airborne => State == "jumping" || State == "falling";
        public void SetVelocity(double horizontal,double vertical,double now)
        {
            if (!Finite(horizontal) || !Finite(vertical) || !Finite(now)) return;
            initialized = true; sampledAt = now; Speed = Math.Max(0,horizontal); VerticalSpeed = vertical;
            State = vertical > 2.5 ? "jumping" : vertical < -2.5 ? "falling" : Speed >= 8 ? "sprinting" : Speed >= RunThreshold ? "running" : Speed > WalkThreshold ? "walking" : "idle";
        }

        public void Observe(double nextX, double nextZ, double now)
        {
            if (!Finite(nextX) || !Finite(nextZ) || !Finite(now)) return;
            if (!initialized) { initialized = true; x = nextX; z = nextZ; sampledAt = now; return; }
            double dx = nextX - x, dz = nextZ - z;
            if (dx * dx + dz * dz < 0.000001) { Tick(now); return; }
            double elapsed = now - sampledAt;
            double distance = Math.Sqrt(dx * dx + dz * dz);
            x = nextX; z = nextZ; sampledAt = now;
            // Ignore discontinuities and same-frame fixture/teleport updates, not normal 500 ms poses.
            if (elapsed < 0.05 || distance > 12 || distance / elapsed > 40) { Stop(); return; }
            SetVelocity(distance / Math.Min(elapsed, 1.0),0,now);
        }
        public void Tick(double now) { if (initialized && (now - sampledAt >= StopTimeout || now < sampledAt)) Stop(); }
        public void Reset() { initialized = false; Stop(); }
        void Stop() { Speed = VerticalSpeed = 0; State = "idle"; }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
