using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MC7DTD
{
    [DataContract]
    public sealed class NetworkConfig
    {
        [DataMember(Name = "host", IsRequired = true)] public string Host;
        [DataMember(Name = "port", IsRequired = true)] public int Port;
    }
    [DataContract]
    public sealed class EntityPosition
    {
        [DataMember(Name = "x")] public double? X;
        [DataMember(Name = "y")] public double? Y;
        [DataMember(Name = "z")] public double? Z;
        [DataMember(Name = "space")] public string Space;
    }
    [DataContract]
    public sealed class EntityRotation
    {
        [DataMember(Name = "yaw")] public double? Yaw;
        [DataMember(Name = "pitch")] public double? Pitch;
        [DataMember(Name = "roll")] public double? Roll;
    }
    [DataContract]
    public sealed class EntityLifecycle
    {
        [DataMember(Name = "event")] public string Event;
        [DataMember(Name = "reason", EmitDefaultValue = false)] public string Reason;
    }
    [DataContract]
    public sealed class WireMessage
    {
        [DataMember(Name="components", EmitDefaultValue=false)] public PresentationComponents Components;
        [DataMember(Name = "type")] public string Type;
        [DataMember(Name = "client", EmitDefaultValue = false)] public string Client;
        [DataMember(Name = "from", EmitDefaultValue = false)] public string From;
        [DataMember(Name = "text", EmitDefaultValue = false)] public string Text;
        [DataMember(Name = "code", EmitDefaultValue = false)] public string Code;
        [DataMember(Name = "source", EmitDefaultValue = false)] public string Source;
        [DataMember(Name = "x", EmitDefaultValue = false)] public double? X;
        [DataMember(Name = "y", EmitDefaultValue = false)] public double? Y;
        [DataMember(Name = "z", EmitDefaultValue = false)] public double? Z;
        [DataMember(Name = "version", EmitDefaultValue = false)] public int Version;
        [DataMember(Name = "stream_id", EmitDefaultValue = false)] public string StreamId;
        [DataMember(Name = "entity_id", EmitDefaultValue = false)] public string EntityId;
        [DataMember(Name = "entity_type", EmitDefaultValue = false)] public string EntityType;
        [DataMember(Name = "world_id", EmitDefaultValue = false)] public string WorldId;
        [DataMember(Name = "dimension", EmitDefaultValue = false)] public string Dimension;
        [DataMember(Name = "sequence", EmitDefaultValue = false)] public long Sequence;
        [DataMember(Name = "lifecycle", EmitDefaultValue = false)] public EntityLifecycle Lifecycle;
        [DataMember(Name = "position", EmitDefaultValue = false)] public EntityPosition Position;
        [DataMember(Name = "rotation", EmitDefaultValue = false)] public EntityRotation Rotation;
    }
    [DataContract]
    public sealed class EntityOrigin
    {
        [DataMember(Name = "game")] public string Game;
        [DataMember(Name = "world_id")] public string WorldId;
        [DataMember(Name = "dimension")] public string Dimension;
        [DataMember(Name = "entity_id")] public string EntityId;
    }
    [DataContract]
    public sealed class NativeEntityMessage
    {
        [DataMember(Name="components", EmitDefaultValue=false)] public PresentationComponents Components;
        [DataMember(Name = "type")] public string Type = "entity_state";
        [DataMember(Name = "version")] public int Version = 2;
        [DataMember(Name = "source")] public string Source = "7dtd";
        [DataMember(Name = "authority")] public string Authority = "7dtd";
        [DataMember(Name = "origin")] public EntityOrigin Origin;
        [DataMember(Name = "stream_id")] public string StreamId;
        [DataMember(Name = "entity_id")] public string EntityId;
        [DataMember(Name = "entity_type")] public string EntityType = "7dtd:player";
        [DataMember(Name = "world_id")] public string WorldId;
        [DataMember(Name = "dimension")] public string Dimension = "7dtd:main";
        [DataMember(Name = "sequence")] public long Sequence;
        [DataMember(Name = "lifecycle")] public EntityLifecycle Lifecycle;
        [DataMember(Name = "position")] public EntityPosition Position;
        [DataMember(Name = "rotation")] public EntityRotation Rotation;
        [DataMember(Name = "metadata")] public Dictionary<string, object> Metadata = new Dictionary<string, object>();
    }
    // Also exercised by tests/client-harness: no game types or game state access here.
    [DataContract]
    public sealed class HealthValue
    {
        [DataMember(Name = "current")] public double Current;
        [DataMember(Name = "max")] public double Max;
    }
    [DataContract]
    public sealed class HealthComponentMessage
    {
        [DataMember(Name = "type")] public string Type = "entity_components";
        [DataMember(Name = "version")] public int Version = 1;
        [DataMember(Name = "entity_state_version")] public int EntityStateVersion = 2;
        [DataMember(Name = "source")] public string Source = "7dtd";
        [DataMember(Name = "authority")] public string Authority = "7dtd";
        [DataMember(Name = "origin")] public EntityOrigin Origin;
        [DataMember(Name = "stream_id")] public string StreamId;
        [DataMember(Name = "entity_id")] public string EntityId;
        [DataMember(Name = "entity_type")] public string EntityType = "7dtd:player";
        [DataMember(Name = "world_id")] public string WorldId;
        [DataMember(Name = "dimension")] public string Dimension = "7dtd:main";
        [DataMember(Name = "entity_sequence")] public long EntitySequence;
        [DataMember(Name = "revision")] public long Revision;
        [DataMember(Name = "base_revision")] public long BaseRevision;
        [DataMember(Name = "mode")] public string Mode;
        [DataMember(Name = "components")] public Dictionary<string, HealthValue> Components;
    }
    public sealed class BridgeClient : IDisposable
    {
        private readonly Uri uri;
        private readonly Action<string> log;
        private readonly Action<WireMessage> entityReceived;
        private readonly Action resetEntities;
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private Task worker;
        private int playerResyncRequested;
        private readonly object outgoingGate = new object();
        private readonly Queue<byte[]> outgoing = new Queue<byte[]>();
        private ClientWebSocket nativeSocket;
        private long nativeEpoch;
        public long NativeEpoch { get { lock (outgoingGate) return nativeSocket == null ? 0 : nativeEpoch; } }
        private void ResetNative(ClientWebSocket socket)
        { lock (outgoingGate) { outgoing.Clear(); nativeSocket = socket; if (socket != null) nativeEpoch++; } }
        public bool PublishNative(NativeEntityMessage message, long epoch)
            => PublishObservation(message, epoch);
        public bool PublishHealth(HealthComponentMessage message, long epoch)
            => PublishObservation(message, epoch);
        public bool PublishComponents(ComponentMessage message, long epoch)
            => QueueObservation(message.Serialize(), epoch);
        private bool PublishObservation(object message, long epoch)
        {
            byte[] payload;
            using (var data = new MemoryStream())
            {
                new DataContractJsonSerializer(message.GetType(), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true }).WriteObject(data, message);
                payload = data.ToArray();
            }
            return QueueObservation(payload, epoch);
        }
        private bool QueueObservation(byte[] payload,long epoch)
        {
            lock (outgoingGate)
            {
                if (nativeSocket == null || epoch != nativeEpoch || stop.IsCancellationRequested) return false;
                if (payload.Length > 8192 || outgoing.Count >= 64)
                { nativeSocket.Abort(); nativeSocket = null; outgoing.Clear(); log("Native entity queue overflow; reconnect required"); return false; }
                outgoing.Enqueue(payload); return true;
            }
        }
        private async Task FlushNative(ClientWebSocket socket)
        {
            // All socket sends execute on the connection worker, never on the game thread.
            while (true)
            {
                byte[] payload;
                lock (outgoingGate)
                { if (nativeSocket != socket || outgoing.Count == 0) return; payload = outgoing.Dequeue(); }
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(5));
                    await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
                }
                log("7DTD entity sent: " + Encoding.UTF8.GetString(payload));
            }
        }
        public void RequestPlayerResync() { Interlocked.Exchange(ref playerResyncRequested, 1); }
        public BridgeClient(string configPath, Action<string> logger, Action<WireMessage> entityReceived = null, Action resetEntities = null)
        {
            log = logger;
            this.entityReceived = entityReceived;
            this.resetEntities = resetEntities;
            NetworkConfig config;
            using (var file = File.OpenRead(configPath))
                config = (NetworkConfig)new DataContractJsonSerializer(typeof(NetworkConfig)).ReadObject(file);
            if (config.Host != "localhost" || config.Port < 1024 || config.Port > 65535)
                throw new InvalidDataException("network.json requires localhost and port 1024..65535");
            uri = new Uri("ws://localhost:" + config.Port + "/ws");
        }
        public void Start() { if (worker != null) return; worker = Task.Run(Run); }
        private async Task Run()
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    using (var socket = new ClientWebSocket())
                    {
                        socket.Options.Proxy = null;
                        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
                        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
                        {
                            timeout.CancelAfter(TimeSpan.FromSeconds(5));
                            await socket.ConnectAsync(uri, timeout.Token).ConfigureAwait(false);
                        }
                        await Send(socket, new WireMessage { Type = "7dtd_connect", Client = "7dtd" }).ConfigureAwait(false);
                        bool welcomed = false;
                        Task<WireMessage> pendingReceive = null;
                        while (socket.State == WebSocketState.Open && !stop.IsCancellationRequested)
                        {
                            if (welcomed && Interlocked.Exchange(ref playerResyncRequested, 0) != 0)
                                await Send(socket, new WireMessage { Type = "test", Text = "MC7DTD player proxy resync" }).ConfigureAwait(false);
                            if (welcomed) await FlushNative(socket).ConfigureAwait(false);
                            if (pendingReceive == null) pendingReceive = ReceiveWithDeadline(socket, welcomed);
                            if (await Task.WhenAny(pendingReceive, Task.Delay(100, stop.Token)).ConfigureAwait(false) != pendingReceive) continue;
                            var message = await pendingReceive.ConfigureAwait(false);
                            pendingReceive = null;
                            if (message == null) break;
                            if (message.Type == "welcome" && message.Client == "7dtd")
                            { welcomed = true; ResetNative(socket); log("Bridge connected"); log("7DTD connected"); }
                            else if (welcomed && message.Type == "peer_connected")
                            {
                                if (message.Client == "minecraft") { ResetNative(socket); resetEntities?.Invoke(); }
                                await Send(socket, new WireMessage { Type = "test", Text = "Hello from 7DTD" }).ConfigureAwait(false);
                            }
                            else if (welcomed && message.Type == "test") log("Test received from " + message.From + ": " + message.Text);
                            else if (welcomed && message.Type == "player_position")
                            {
                                if (message.Source == "minecraft" && ValidCoordinate(message.X) && ValidCoordinate(message.Y) && ValidCoordinate(message.Z))
                                    log(string.Format(CultureInfo.InvariantCulture, "Minecraft player: x={0:R} y={1:R} z={2:R}", message.X.Value, message.Y.Value, message.Z.Value));
                                else log("Invalid player position ignored");
                            }
                            else if (welcomed && message.Type == "entity_state") LogEntity(message);
                            else if (message.Type == "error") log("Bridge error: " + message.Code);
                        }
                    }
                    if (!stop.IsCancellationRequested) log("Bridge disconnected; retry in 3 seconds");
                }
                catch (Exception ex) { if (!stop.IsCancellationRequested) log("Bridge unavailable: " + ex.Message + "; retry in 3 seconds"); }
                finally { ResetNative(null); resetEntities?.Invoke(); }
                try { await Task.Delay(3000, stop.Token).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            }
        }
        private async Task<WireMessage> ReceiveWithDeadline(ClientWebSocket socket, bool welcomed)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
            {
                if (!welcomed) deadline.CancelAfter(TimeSpan.FromSeconds(10));
                return await Receive(socket, deadline.Token).ConfigureAwait(false);
            }
        }
        private async Task Send(ClientWebSocket socket, WireMessage message)
        {
            using (var data = new MemoryStream())
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
            {
                new DataContractJsonSerializer(typeof(WireMessage)).WriteObject(data, message);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                await socket.SendAsync(new ArraySegment<byte>(data.ToArray()), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
            }
        }
        private static bool ValidCoordinate(double? value)
        { return value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value); }
        private void LogEntity(WireMessage message)
        {
            if (message.Version != 1 || message.Source != "minecraft" || message.Lifecycle == null ||
                string.IsNullOrEmpty(message.EntityId) || message.Sequence < 1)
            { log("Invalid entity state ignored"); return; }
            var action = message.Lifecycle.Event;
            var identity = string.Format(CultureInfo.InvariantCulture,
                "Entity state: event={0} id={1} type={2} source={3} world={4} dimension={5} stream={6} sequence={7}",
                action, message.EntityId, message.EntityType, message.Source, message.WorldId, message.Dimension, message.StreamId, message.Sequence);
            if (action == "despawn" && message.Position == null && message.Rotation == null)
            { log(identity + " reason=" + message.Lifecycle.Reason); entityReceived?.Invoke(message); return; }
            var p = message.Position; var r = message.Rotation;
            if ((action != "spawn" && action != "update") || p == null || r == null || p.Space != "7dtd" ||
                !ValidCoordinate(p.X) || !ValidCoordinate(p.Y) || !ValidCoordinate(p.Z) ||
                !ValidCoordinate(r.Yaw) || !ValidCoordinate(r.Pitch) || !ValidCoordinate(r.Roll))
            { log("Invalid entity state ignored"); return; }
            log(identity + string.Format(CultureInfo.InvariantCulture,
                " x={0:R} y={1:R} z={2:R} yaw={3:R} pitch={4:R} roll={5:R}",
                p.X.Value, p.Y.Value, p.Z.Value, r.Yaw.Value, r.Pitch.Value, r.Roll.Value));
            entityReceived?.Invoke(message);
        }
        private static async Task<WireMessage> Receive(ClientWebSocket socket, CancellationToken ct)
        {
            using (var data = new MemoryStream())
            {
                var buffer = new byte[2048];
                WebSocketReceiveResult frame;
                do
                {
                    frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                    if (frame.MessageType == WebSocketMessageType.Close) return null;
                    if (frame.MessageType != WebSocketMessageType.Text || data.Length + frame.Count > 8192)
                        throw new InvalidDataException("Invalid bridge frame");
                    data.Write(buffer, 0, frame.Count);
                } while (!frame.EndOfMessage);
                data.Position = 0;
                return (WireMessage)new DataContractJsonSerializer(typeof(WireMessage)).ReadObject(data);
            }
        }
        public void Dispose() { stop.Cancel(); /* Never block the Unity game thread. */ }
    }
}
