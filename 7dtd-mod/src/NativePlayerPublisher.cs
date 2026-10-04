using System;

namespace MC7DTD
{
    // Main-thread lifecycle tracker. Only the primary native player is supplied by BridgeMod.
    public sealed class NativePlayerPublisher
    {
        private readonly Func<long> epoch;
        private readonly Func<NativeEntityMessage, long, bool> send;
        private object player;
        private string world, id, stream;
        private long session, sequence;
        private DateTime nextUpdate;
        private readonly Func<HealthComponentMessage, long, bool> sendHealth;
        private long healthRevision;
        private HealthValue health;
        private readonly Func<ComponentMessage,long,bool> sendComponents;
        private System.Collections.Generic.Dictionary<string,string> componentValues = new System.Collections.Generic.Dictionary<string,string>();
        private System.Collections.Generic.Dictionary<string,string> equipmentSlots;
        public NativePlayerPublisher(Func<long> epoch, Func<NativeEntityMessage, long, bool> send,
            Func<HealthComponentMessage, long, bool> sendHealth = null, Func<ComponentMessage,long,bool> sendComponents = null)
        { this.epoch = epoch; this.send = send; this.sendHealth = sendHealth; this.sendComponents=sendComponents; }
        public void Tick(object nativePlayer, string worldId, double x, double y, double z,
            double yaw, double pitch, double roll, DateTime now, double? currentHealth = null, double? maxHealth = null, IdentitySample identity = null,
            Func<System.Collections.Generic.Dictionary<string,string>> sampleEquipment = null)
        {
            var current = epoch();
            if (current != session)
            { player = null; session = current; stream = Guid.NewGuid().ToString("D"); sequence = 0; health = null; healthRevision = 0; componentValues.Clear(); equipmentSlots=null; }
            if (current == 0) { player = null; return; }
            if (player != null && (!ReferenceEquals(player, nativePlayer) || world != worldId)) Leave("world_unloaded");
            if (nativePlayer == null) return;
            foreach (var value in new[] { x, y, z, yaw, pitch, roll })
                if (double.IsNaN(value) || double.IsInfinity(value)) return;
            if (pitch < -90 || pitch > 90) return;
            var spawn = player == null;
            if (!spawn && now < nextUpdate) return;
            if (spawn) { id = Guid.NewGuid().ToString("D"); world = worldId; health = null; healthRevision = 0; componentValues.Clear(); equipmentSlots=null; }
            var message = Message(spawn ? "spawn" : "update");
            message.Position = new EntityPosition { X = x, Y = y, Z = z, Space = "7dtd" };
            message.Rotation = new EntityRotation { Yaw = Wrap(yaw, 0), Pitch = pitch, Roll = Wrap(roll, -180) };
            if (!send(message, current)) { player = null; return; }
            player = nativePlayer; nextUpdate = now.AddMilliseconds(500);
            var valid = currentHealth.HasValue && maxHealth.HasValue && !double.IsNaN(currentHealth.Value) && !double.IsInfinity(currentHealth.Value)
                && !double.IsNaN(maxHealth.Value) && !double.IsInfinity(maxHealth.Value) && currentHealth.Value >= 0 && maxHealth.Value > 0 && currentHealth.Value <= maxHealth.Value;
            var sampled = valid ? new HealthValue { Current = currentHealth.Value, Max = maxHealth.Value } : null;
            if(sendComponents!=null) PublishCombined(sampled,identity,sampleEquipment?.Invoke());
            else if (sampled == null ? health != null : health == null || sampled.Current != health.Current || sampled.Max != health.Max)
                PublishHealth(sampled);
        }
        public void Leave(string reason)
        {
            if (player != null && session != 0 && session == epoch())
            { if(sendComponents!=null) PublishCombined(null,null); else if (health != null) PublishHealth(null); var message = Message("despawn"); message.Lifecycle.Reason = reason; send(message, session); }
            player = null;
        }
        private void PublishHealth(HealthValue value)
        {
            if (sendHealth == null) return;
            var revision = healthRevision + 1;
            var snapshot = health == null && value != null;
            var message = new HealthComponentMessage {
                StreamId = stream, EntityId = id, WorldId = world, EntitySequence = sequence,
                Origin = new EntityOrigin { Game = "7dtd", WorldId = world, Dimension = "7dtd:main", EntityId = id },
                Revision = revision, BaseRevision = snapshot ? 0 : healthRevision, Mode = snapshot ? "snapshot" : "patch",
                Components = new System.Collections.Generic.Dictionary<string, HealthValue> { ["health"] = value }
            };
            if (!sendHealth(message, session)) { player = null; return; }
            healthRevision = revision; health = value;
        }
        private void PublishCombined(HealthValue health,IdentitySample identity,System.Collections.Generic.Dictionary<string,string> equipment=null)
        {
            var desired=new System.Collections.Generic.Dictionary<string,string>();
            if(health!=null)desired["health"]=ComponentMessage.Json(health);
            if(identity!=null && identity.Enabled){desired["name"]=identity.NameJson;desired["custom_metadata"]=identity.TagsJson;}
            if(equipment!=null)desired["equipment"]=EquipmentJson(equipment);
            var snapshot=healthRevision==0;
            var delta=new System.Collections.Generic.Dictionary<string,string>();
            foreach(var item in desired)if(snapshot || !componentValues.TryGetValue(item.Key,out var old) || item.Value!=old)delta[item.Key]=item.Value;
            if(!snapshot && equipment!=null && equipmentSlots!=null && delta.ContainsKey("equipment")) {
                var changed=new System.Collections.Generic.Dictionary<string,string>();
                foreach(var slot in equipment)if(!equipmentSlots.TryGetValue(slot.Key,out var old) || old!=slot.Value)changed[slot.Key]=slot.Value;
                delta["equipment"]=EquipmentJson(changed);
            }
            if(!snapshot)foreach(var item in componentValues)if(!desired.ContainsKey(item.Key))delta[item.Key]=null;
            if(!snapshot && delta.Count==0)return;
            var message=new ComponentMessage {
                Envelope=new HealthComponentMessage {
                    StreamId=stream,EntityId=id,WorldId=world,EntitySequence=sequence,
                    Origin=new EntityOrigin{Game="7dtd",WorldId=world,Dimension="7dtd:main",EntityId=id},
                    Revision=healthRevision+1,BaseRevision=snapshot?0:healthRevision,Mode=snapshot?"snapshot":"patch"
                },Values=delta
            };
            if(!sendComponents(message,session)){player=null;return;}
            healthRevision++;componentValues=desired;equipmentSlots=equipment==null ? null : new System.Collections.Generic.Dictionary<string,string>(equipment);
        }
        private static string EquipmentJson(System.Collections.Generic.Dictionary<string,string> slots)
        {
            var members=new System.Collections.Generic.List<string>();
            foreach(var slot in slots)members.Add(ComponentMessage.Json(slot.Key)+":"+(slot.Value==null ? "null" : "{\"item_id\":"+ComponentMessage.Json(slot.Value)+"}"));
            return "{\"slots\":{"+string.Join(",",members)+"}}";
        }
        private NativeEntityMessage Message(string action)
        {
            return new NativeEntityMessage
            {
                StreamId = stream, EntityId = id, WorldId = world, Sequence = ++sequence,
                Origin = new EntityOrigin { Game = "7dtd", WorldId = world, Dimension = "7dtd:main", EntityId = id },
                Lifecycle = new EntityLifecycle { Event = action }
            };
        }
        private static double Wrap(double value, double start) => ((value - start) % 360 + 360) % 360 + start;
    }
}
