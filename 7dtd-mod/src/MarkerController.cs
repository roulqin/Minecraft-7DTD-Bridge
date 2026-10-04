using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace MC7DTD
{
    public interface IMarkerScene
    {
        object Create(string name, double x, double y, double z);
        void Move(object handle, double x, double y, double z);
        void Delete(object handle);
    }
    public interface IRotatingProxyScene : IMarkerScene
    {
        void Rotate(object handle, double yaw, double pitch, double roll);
    }

    // Receive on the socket worker; apply only from the game update callback.
    public sealed class MarkerController
    {
        public const string MarkerType = "minecraft:marker";
        public const int Capacity = 256;
        private readonly object gate = new object();
        private readonly Queue<Snapshot> pending = new Queue<Snapshot>();
        private readonly Dictionary<string, Marker> markers = new Dictionary<string, Marker>();
        private readonly IMarkerScene scene;
        private readonly Action<string> log;
        private readonly string entityType;
        private readonly string label;
        private readonly EntityDebugLog debug;
        private bool reset;
        private int ownerThread;
        public int Count => markers.Count; // Game thread only.
        public MarkerController(IMarkerScene scene, Action<string> log, string entityType = MarkerType, string label = "Marker", EntityDebugLog debug = null)
        { this.debug = debug; this.scene = scene; this.log = message => log(label == "Marker" ? message : message.Replace("Marker", label)); this.entityType = entityType; this.label = label; }

        public void Enqueue(WireMessage message)
        {
            if (message.EntityType != entityType) return;
            Snapshot snapshot;
            try { snapshot = new Snapshot(message); }
            catch (ArgumentException) { log("Marker rejected: invalid state"); return; }
            lock (gate)
            {
                if (pending.Count >= Capacity)
                {
                    pending.Clear(); reset = true;
                    log("Marker queue overflow; cleared, fresh spawn required"); return;
                }
                pending.Enqueue(snapshot);
            }
        }
        public void Reset() { lock (gate) { pending.Clear(); reset = true; } }

        public void Tick(bool worldReady)
        {
            var thread = Thread.CurrentThread.ManagedThreadId;
            if (ownerThread == 0) ownerThread = thread;
            if (ownerThread != thread) throw new InvalidOperationException("Marker Tick must stay on the game thread");
            Snapshot[] batch; bool clear;
            lock (gate)
            {
                clear = reset || !worldReady; reset = false;
                batch = worldReady ? pending.ToArray() : new Snapshot[0]; pending.Clear();
            }
            if (clear) Clear();
            if (!worldReady) return;
            foreach (var state in batch)
            {
                try { Apply(state); }
                catch (Exception ex) { log("Marker operation failed: " + ex.Message); }
            }
            // Refresh world -> Unity coordinates even without new wire messages (floating origin).
            foreach (var marker in markers.Values)
                try { scene.Move(marker.Handle, marker.State.X, marker.State.Y, marker.State.Z); Rotate(marker.Handle, marker.State); }
                catch (Exception ex) { log("Marker refresh failed: " + ex.Message); }
        }
        private void Apply(Snapshot state)
        {
            Marker marker;
            if (state.Event == "spawn")
            {
                if (markers.ContainsKey(state.Key)) { log("Marker duplicate spawn ignored: " + state.Id); return; }
                if (markers.Count >= Capacity) { log("Marker capacity reached; spawn ignored"); return; }
                var handle = scene.Create("MC7DTD-" + label.Replace(" ", "-").ToLowerInvariant() + "-" + state.Id, state.X, state.Y, state.Z);
                try { Rotate(handle, state); } catch { scene.Delete(handle); throw; }
                var appearance = PresentationComponent.Default().Merge(state.Presentation ?? PresentationComponent.Default());
                markers.Add(state.Key, new Marker { Handle = handle, State = state, Presentation = appearance });
                log("Spawn proxy: entity="+state.Id+" "+appearance);
                Report("spawned", state); debug?.Observe(state.Key,state.Id,entityType,"spawn",appearance); return;
            }
            if (!markers.TryGetValue(state.Key, out marker)) { log("Marker unknown " + state.Event + " ignored: " + state.Id); return; }
            if (marker.State.Stream != state.Stream) { log("Marker stream mismatch ignored: " + state.Id); return; }
            if (state.Event == "update")
            {
                scene.Move(marker.Handle, state.X, state.Y, state.Z); Rotate(marker.Handle, state); marker.State = state;
                if (state.HasPresentation)
                {
                    marker.Presentation = state.Presentation == null ? null : (marker.Presentation ?? PresentationComponent.Default()).Merge(state.Presentation);
                    log("[Presentation] entity="+state.Id+" "+(marker.Presentation ?? PresentationComponent.Default())+" removed="+(state.Presentation == null));
                }
                Report("updated", state); debug?.Observe(state.Key,state.Id,entityType,"update",marker.Presentation);
            }
            else
            {
                scene.Delete(marker.Handle); markers.Remove(state.Key); Report("despawned", state); debug?.Observe(state.Key,state.Id,entityType,"despawn",null);
            }
        }
        private void Rotate(object handle, Snapshot state)
        { if (scene is IRotatingProxyScene rotating) rotating.Rotate(handle, state.Yaw, state.Pitch, state.Roll); }
        private void Clear()
        {
            debug?.Clear();
            if (markers.Count == 0) return;
            foreach (var marker in markers.Values) scene.Delete(marker.Handle);
            markers.Clear(); log("Marker reset: count=0; fresh spawn required");
        }
        private void Report(string action, Snapshot state)
        {
            log(string.Format(CultureInfo.InvariantCulture,
                "Marker {0}: id={1} count={2} x={3:R} y={4:R} z={5:R} thread={6} yaw={7:R} pitch={8:R} roll={9:R}",
                action, state.Id, Count, state.X, state.Y, state.Z, Thread.CurrentThread.ManagedThreadId, state.Yaw, state.Pitch, state.Roll));
        }
        private sealed class Marker { public object Handle; public Snapshot State; public PresentationComponent Presentation; }
        private sealed class Snapshot
        {
            public readonly string Key, Id, Stream, Event;
            public readonly bool HasPresentation;
            public readonly PresentationComponent Presentation;
            public readonly double X, Y, Z;
            public readonly double Yaw, Pitch, Roll;
            public Snapshot(WireMessage message)
            {
                if (message.Type != "entity_state" || message.Version != 1 || message.Source != "minecraft" ||
                    string.IsNullOrEmpty(message.WorldId) || string.IsNullOrEmpty(message.Dimension) ||
                    string.IsNullOrEmpty(message.EntityId) || string.IsNullOrEmpty(message.StreamId)) throw new ArgumentException();
                Event = message.Lifecycle?.Event;
                if (Event != "spawn" && Event != "update" && Event != "despawn") throw new ArgumentException();
                Id = message.EntityId; Stream = message.StreamId;
                HasPresentation = message.Components != null; Presentation = message.Components?.Presentation?.Copy();
                Key = message.Source + "\n" + message.WorldId + "\n" + message.Dimension + "\n" + Id;
                if (Event == "despawn")
                { if (message.Position != null || message.Rotation != null) throw new ArgumentException(); return; }
                if (message.Position?.Space != "7dtd" || message.Rotation == null) throw new ArgumentException();
                X = Coordinate(message.Position.X); Y = Coordinate(message.Position.Y); Z = Coordinate(message.Position.Z);
                Yaw = Coordinate(message.Rotation.Yaw); Pitch = Coordinate(message.Rotation.Pitch); Roll = Coordinate(message.Rotation.Roll);
                if (Yaw < 0 || Yaw >= 360 || Pitch < -90 || Pitch > 90 || Roll < -180 || Roll >= 180) throw new ArgumentException();
            }
            private static double Coordinate(double? value)
            {
                if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value) || Math.Abs(value.Value) > 1000000)
                    throw new ArgumentException();
                return value.Value;
            }
        }
    }
}
