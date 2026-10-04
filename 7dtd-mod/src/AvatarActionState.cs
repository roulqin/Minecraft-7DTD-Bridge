using System;
using Newtonsoft.Json.Linq;

namespace MC7DTD
{
    // A local visual estimate, never a replicated component or source-game action assertion.
    public sealed class AvatarActionState
    {
        public string State { get; private set; } = "idle";
        public double Speed { get; private set; }
        public double VerticalVelocity { get; private set; }
        public bool Grounded { get; private set; } = true;
        public double Timestamp { get; private set; }
        public bool SneakingAvailable { get; private set; }
        public int Code => State == "walking" ? 1 : State == "running" ? 2 : State == "jumping" ? 3 : State == "falling" ? 4 : State == "sneaking" ? 5 : 0;
        public string Animation => Code == 1 || Code == 5 ? (Speed > .1 ? "walk" : "idle") : Code == 2 ? "run" : Code == 3 ? "jump" : Code == 4 ? "fall" : "idle";
        bool initialized, airborne, descended;
        double baselineY, stillSince = double.NaN;

        public void Observe(double speed, double vertical, bool grounded, double now, bool? sneaking = null)
        {
            if (!Finite(speed) || !Finite(vertical) || !Finite(now) || (initialized && now < Timestamp)) return;
            initialized = true; Speed = Math.Max(0, speed); VerticalVelocity = vertical; Grounded = grounded; Timestamp = now;
            SneakingAvailable = sneaking.HasValue;
            State = !grounded ? vertical > .15 ? "jumping" : "falling" : sneaking == true ? "sneaking" : Speed >= 5 ? "running" : Speed > .1 ? "walking" : "idle";
        }

        // Raycast hit supplies support, not proof of source-game contact: the two terrains have different Y.
        public void ObserveTransform(double speed, double vertical, double sourceY, bool support, double now, bool discontinuity)
        {
            if (!Finite(sourceY) || !Finite(now) || !Finite(speed) || !Finite(vertical) || (initialized && now < Timestamp)) return;
            if (!initialized || discontinuity) { baselineY = sourceY; airborne = descended = false; stillSince = double.NaN; }
            if (!discontinuity && (vertical > .15 || vertical < -.15))
            {
                airborne = true; descended |= vertical < -.15; stillSince = double.NaN;
            }
            if (airborne && Math.Abs(vertical) <= .15)
            {
                if (double.IsNaN(stillSince)) stillSince = now;
                // The fall ending at a different elevation is also a landing, if local support exists.
                if (support && ((descended && (sourceY <= baselineY + .12 || now - stillSince >= .25)) || now - stillSince >= .85)) airborne = descended = false;
            }
            if (!airborne) baselineY = sourceY;
            Observe(speed, discontinuity ? 0 : vertical, support && !airborne, now);
        }
        public JObject Inspect() => new JObject { ["state"] = State, ["speed"] = Speed, ["grounded"] = Grounded,
            ["verticalVelocity"] = VerticalVelocity, ["timestamp"] = Timestamp, ["animation"] = Animation, ["sneaking_available"] = SneakingAvailable };
        public void Reset() { initialized = airborne = descended = false; baselineY = 0; stillSince = double.NaN; State = "idle"; Speed = VerticalVelocity = Timestamp = 0; Grounded = true; SneakingAvailable = false; }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
