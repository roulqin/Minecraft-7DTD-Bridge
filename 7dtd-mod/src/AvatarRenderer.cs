using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MC7DTD
{
    // Local presentation only. No entity component or network state is written here.
    public sealed class AvatarRenderer : IDisposable
    {
        public sealed class Handle
        {
            public string Name, Status, Reason, Skin;
            public double X, Y, Z, Yaw, Pitch;
            public GameObject Root;
            public Transform Head, RightHand;
            public Animator Animator;
            public AvatarEquipmentAnimation EquipmentAnimation;
            public AvatarHeadPose Pose;
            public readonly AvatarAnimationState Animation = new AvatarAnimationState();
            public readonly AvatarActionState Action = new AvatarActionState();
            public AvatarMotionController Motion;
            public AvatarGroundAlignment Ground;
            public AvatarMotionSettings MotionSettings;
            public Texture2D Texture;
            public readonly List<Material> Materials = new List<Material>();
            public object Fallback;
            public bool Removed;
            public readonly LocalAvatarEquipmentAdapter Equipment = new LocalAvatarEquipmentAdapter();
        }
        public static AvatarRenderer Current { get; internal set; }
        readonly string resourceRoot;
        readonly Action<string> log;
        readonly AvatarMotionSettings motionSettings;
        readonly EquipmentStyleResolver equipmentStyles;
        readonly UnityPlayerProxyScene fallback = new UnityPlayerProxyScene();
        readonly List<Handle> active = new List<Handle>();
        AssetBundle bundle;
        GameObject prefab;
        int resourceUsers;
        readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        void MainThread()
        { if(System.Threading.Thread.CurrentThread.ManagedThreadId!=ownerThread) throw new InvalidOperationException("Avatar renderer must stay on the game thread"); }
        public Handle[] Active => active.ToArray();
        public AvatarRenderer(string projectRoot, Action<string> logger, AvatarMotionSettings options = null)
        { resourceRoot = Path.Combine(projectRoot, "assets", "avatar"); log = logger; motionSettings = options ?? AvatarMotionSettings.Load(projectRoot,logger); equipmentStyles=File.Exists(Path.Combine(projectRoot,"config","equipment_attachment.json"))?new EquipmentStyleResolver(projectRoot):null; }

        public Handle CreateAvatar(string name, double x, double y, double z)
        {
            MainThread();
            var h = new Handle { Name = name, Status = "active", Skin = "player_default.png" };
            h.MotionSettings = motionSettings; h.Motion = new AvatarMotionController(motionSettings); h.Ground = new AvatarGroundAlignment(motionSettings);
            bool acquired = false;
            try
            {
                var config = ReadObject(Path.Combine(resourceRoot, "avatar_config.json"));
                if ((string)config["model"] != "minecraft_humanoid" || (string)config["variant"] != "alex_slim")
                    throw new InvalidDataException("unsupported_avatar");
                if (config["skeleton"] != null && (string)config["skeleton"] != "minecraft_avatar_v1")
                    throw new InvalidDataException("unsupported_skeleton");
                if (bundle == null)
                {
                    bundle = AssetBundle.LoadFromFile(Path.Combine(resourceRoot, "bundles", "windows", "minecraft_avatar_v1"));
                    if (bundle == null) throw new InvalidDataException("resource_missing");
                    prefab = bundle.LoadAsset<GameObject>("Assets/Avatar/Generated/MinecraftAvatarPrefab.prefab");
                    if (prefab == null) throw new InvalidDataException("prefab_missing");
                }
                resourceUsers++; acquired = true;
                h.Root = UnityEngine.Object.Instantiate(prefab); h.Root.name = name;
                h.Head = Find(h.Root.transform, "rig_head"); h.RightHand = Find(h.Root.transform, "rig_hand_right");
                h.Animator = h.Root.GetComponent<Animator>();
                if (h.Head == null || h.RightHand == null || h.Animator == null) throw new InvalidDataException("skeleton_missing");
                if(equipmentStyles!=null)h.Equipment.Configure(h.Root.transform,equipmentStyles);
                h.Animator.applyRootMotion = false; h.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                h.Animator.Rebind(); h.Animator.SetFloat("speed", 0); h.Animator.Update(0);
                var heldWalk=bundle.LoadAsset<AnimationClip>("Assets/Avatar/Generated/HeldWalk.anim");var heldRun=bundle.LoadAsset<AnimationClip>("Assets/Avatar/Generated/HeldRun.anim");
                if(heldWalk!=null&&heldRun!=null){h.EquipmentAnimation=new AvatarEquipmentAnimation(h.Animator,heldWalk,heldRun);h.Equipment.Changed=()=>h.EquipmentAnimation?.SetHolding(h.Equipment.Holding);}
                h.Pose = h.Root.AddComponent<AvatarHeadPose>(); h.Pose.Bind(h.Head, h.Animator, h.Animation);
                h.Pose.Handle = h;
                var skinPath = ResolveSkin((string)config["skin"]);
                try { h.Texture = LoadPng(skinPath); h.Skin = Path.GetFileName(skinPath); }
                catch (Exception)
                {
                    h.Status = "fallback"; h.Reason = "skin_missing_or_invalid";
                    // The prefab contains the verified default skin; its shared texture remains read-only.
                }
                var owned = new Dictionary<Material, Material>();
                foreach (var r in h.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!owned.TryGetValue(r.sharedMaterial, out var material))
                    {
                        material = new Material(r.sharedMaterial); owned.Add(r.sharedMaterial, material); h.Materials.Add(material);
                        if (material.shader == null || !material.shader.isSupported || material.shader.name != "MC7DTD/AvatarSkin" || material.FindPass("AVATAR_DEPTH") < 0)
                            throw new InvalidDataException("material_unavailable");
                        material.SetFloat("_Cutoff", 0.5f);
                        material.renderQueue = r.name == "Layer2" ? 2451 : 2450;
                        if (h.Texture != null) material.mainTexture = h.Texture;
                    }
                    r.sharedMaterial = material;
                }
            }
            catch (Exception ex)
            {
                DestroyOwned(h);
                if (acquired) resourceUsers--;
                ReleaseUnusedBundle();
                h.Status = "fallback"; h.Reason = ex is InvalidDataException ? ex.Message : "resource_missing";
                h.Fallback = fallback.Create(name, x, y, z);
                log?.Invoke("Avatar fallback: " + h.Reason);
            }
            active.Add(h); UpdateAvatar(h, x, y, z, 0, 0); if(h.Root!=null&&motionSettings.Enabled)h.Pose.RenderFrame(); return h;
        }
        public void UpdateAvatar(Handle h, double x, double y, double z, double yaw, double pitch)
        {
            MainThread();
            if (h.Removed) return;
            h.X=x;h.Y=y;h.Z=z;h.Yaw=yaw;h.Pitch=pitch;
            if (h.Fallback != null) { fallback.Move(h.Fallback, x, y, z); fallback.Rotate(h.Fallback, yaw, 0, 0); return; }
            if (motionSettings.Enabled) { h.Motion.Receive(new Vector3((float)x,(float)y,(float)z),(float)yaw,(float)pitch,Time.realtimeSinceStartup);return; }
            h.Root.transform.position = new Vector3((float)x, (float)y, (float)z) - Origin.position;
            h.Root.transform.rotation = Quaternion.Euler(0, -(float)yaw, 0);
            h.Animation.Observe(x, z, Time.realtimeSinceStartup);
            h.Pose.UpdateAnimation();
            h.Pose.Pitch = Mathf.Clamp((float)pitch, -90, 90); h.Pose.Apply();
        }
        public void RemoveAvatar(Handle h)
        {
            MainThread();
            if (h == null || h.Removed) return;
            h.Removed = true; h.Action.Reset(); h.Pose?.Driver.Reset(); h.Animation.Reset(); h.Motion.Clear(); h.Ground.Clear(); h.Equipment.Remove(); active.Remove(h);
            if (h.Fallback != null) fallback.Delete(h.Fallback);
            else { DestroyOwned(h); resourceUsers--; }
            ReleaseUnusedBundle();
        }
        static void DestroyOwned(Handle h)
        {
            h.Equipment.Remove();
            h.Equipment.Changed=null;h.EquipmentAnimation?.Dispose();h.EquipmentAnimation=null;
            if (h.Root != null) { h.Root.SetActive(false); UnityEngine.Object.Destroy(h.Root); } h.Root = null;
            h.Head=null;h.RightHand=null;h.Animator=null;h.Pose=null;
            foreach (var m in h.Materials) UnityEngine.Object.Destroy(m); h.Materials.Clear();
            if (h.Texture != null) UnityEngine.Object.Destroy(h.Texture); h.Texture = null;
        }
        void ReleaseUnusedBundle()
        { if (resourceUsers == 0 && bundle != null) { bundle.Unload(true); bundle = null; prefab = null; } }
        public void Dispose() { foreach (var h in Active) RemoveAvatar(h); if (Current == this) Current = null; }
        public JObject Inspect(Handle h) => new JObject {
            ["entity_id"] = h.Name.StartsWith("MC7DTD-player-proxy-", StringComparison.Ordinal) ? h.Name.Substring(20) : h.Name,
            ["position"] = new JObject { ["x"]=h.X,["y"]=h.Y,["z"]=h.Z },
            ["avatar"] = new JObject { ["model"] = h.Fallback == null ? "minecraft_alex" : "geometric_proxy", ["variant"] = "alex_slim", ["animation"] = h.Animation.State,
                ["velocity"] = h.Animation.Speed, ["interpolation"] = h.MotionSettings.Enabled && h.Fallback == null ? h.Motion.Status : "disabled", ["ground_offset"] = h.Ground.Offset },
            ["avatar_action"] = h.Action.Inspect(),
            ["equipment_attachment"] = h.Equipment.Attachment?.Inspect(),
            ["avatar_equipment"] = h.Equipment.InspectSlots(),
            ["avatar_animation_debug"] = new JObject { ["animation_state"] = h.Action.State, ["animator_state"] = AvatarAnimatorDriver.State(h.Animator), ["speed"] = h.Action.Speed,
                ["blend"] = AvatarAnimatorDriver.Blend(h.Animator), ["pose"] = PoseStatus(h), ["interpolation_profile"] = h.MotionSettings.Profile, ["buffer_ms"] = h.MotionSettings.Delay*1000 },
            ["avatar_renderer"] = new JObject { ["status"] = h.Removed ? "removed" : h.Fallback == null && !RenderObjectsActive(h) ? "fallback" : h.Status,
                ["reason"] = h.Removed ? "entity_removed" : h.Fallback == null && !RenderObjectsActive(h) ? "renderer_inactive" : h.Reason,
                ["model"] = h.Fallback == null ? "minecraft_alex" : "geometric_proxy", ["variant"] = "alex_slim",
                ["skin"] = h.Skin, ["material"] = h.Fallback == null ? "cutout" : "fallback",
                ["animation"] = h.Fallback == null ? h.Animation.State : "none",
                ["animator"] = ActualAnimation(h), ["speed"] = h.Animation.Speed,
                ["velocity"] = h.Animation.Speed, ["vertical_velocity"] = h.Animation.VerticalSpeed,
                ["playback_speed"] = h.Animator == null ? 0 : h.Animator.speed,
                ["interpolation"] = h.MotionSettings.Enabled && h.Fallback == null ? h.Motion.Status : "disabled",
                ["ground_offset"] = h.Ground.Offset, ["ground_status"] = h.Ground.Status,
                ["body_yaw"] = h.Motion.BodyYaw, ["head_relative_yaw"] = h.Motion.HeadYaw,
                ["display_position"] = h.Root == null ? null : new JObject { ["x"] = h.Root.transform.position.x + Origin.position.x, ["y"] = h.Root.transform.position.y + Origin.position.y, ["z"] = h.Root.transform.position.z + Origin.position.z },
                ["render_objects_active"] = RenderObjectsActive(h),
                ["held_item_local_test"] = h.Equipment.Item, ["anchor"] = "RightHand" }
        };
        static string PoseStatus(Handle h)
        {
            if(h.Root==null||h.Animator==null)return "unavailable";
            if(h.Animator.IsInTransition(0))return "transition";
            if(!h.Action.Grounded)return "airborne";
            foreach(var side in new[]{"left","right"}){var arm=Find(h.Root.transform,"rig_arm_"+side+"_upper");if(arm==null)return "unavailable";var direction=arm.TransformDirection(side=="left"?Vector3.left:Vector3.right);if(Vector3.Dot(direction,h.Root.transform.up)>-.65f)return "abnormal";}
            return "normal";
        }
        static bool RenderObjectsActive(Handle h)
        {
            if (h.Root == null || !h.Root.activeInHierarchy || h.Animator == null || !h.Animator.isActiveAndEnabled) return false;
            var renderers = h.Root.GetComponentsInChildren<SkinnedMeshRenderer>();
            return renderers.Length == 12 && renderers.All(r => r.enabled && r.sharedMaterial != null && r.sharedMaterial.mainTexture != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.isSupported);
        }
        static string ActualAnimation(Handle h)
        {
            if (h.Animator == null || !h.Animator.isActiveAndEnabled) return "none";
            var actual=AvatarAnimatorDriver.State(h.Animator);
            return actual=="walk"?"walking":actual=="run"?"running":actual=="jump_start"||actual=="jump_loop"?"jumping":actual=="fall"?"falling":actual;

        }
        static JObject ReadObject(string path)
        {
            var bytes = File.ReadAllBytes(path); if (bytes.Length > 4096) throw new InvalidDataException("config_invalid");
            using (var reader = new JsonTextReader(new StringReader(System.Text.Encoding.UTF8.GetString(bytes))))
                return JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        }
        string ResolveSkin(string path)
        {
            if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path) || path.Contains(":")) throw new InvalidDataException("skin_path_invalid");
            var full = Path.GetFullPath(Path.Combine(resourceRoot, path));
            if (!full.StartsWith(Path.GetFullPath(resourceRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("skin_path_invalid");
            return full;
        }
        static Texture2D LoadPng(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 24 || bytes.Length > 262144 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71 ||
                bytes[16] != 0 || bytes[17] != 0 || bytes[18] != 0 || bytes[19] != 64 || bytes[20] != 0 || bytes[21] != 0 || bytes[22] != 0 || bytes[23] != 64)
                throw new InvalidDataException("skin_invalid");
            var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            try { if (!ImageConversion.LoadImage(t, bytes, false) || t.width != 64 || t.height != 64) throw new InvalidDataException("skin_invalid");
                t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp; return t; }
            catch { UnityEngine.Object.Destroy(t); throw; }
        }
        static Transform Find(Transform t, string name)
        { foreach (var child in t.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child; return null; }
    }
    // Animator runs before LateUpdate; apply head look after its bind-pose writes.
    public sealed class AvatarHeadPose : MonoBehaviour
    {
        Transform head; Quaternion bind;
        Animator animator; AvatarAnimationState animation;
        public readonly AvatarAnimatorDriver Driver = new AvatarAnimatorDriver();
        public AvatarRenderer.Handle Handle;
        public float Pitch;
        public void Bind(Transform target, Animator controller, AvatarAnimationState state) { head = target; bind = target.localRotation; animator = controller; animation = state; }
        public void UpdateAnimation() { if(animator!=null && animation!=null){animation.Tick(Time.realtimeSinceStartup);Driver.Update(animator,Handle?.Action,animation,Handle!=null&&Handle.MotionSettings.Enabled);} }
        public void RenderFrame()
        {
            var h=Handle;if(h==null||h.Removed||!h.MotionSettings.Enabled)return;
            double now=Time.realtimeSinceStartup;h.Motion.Evaluate(now);
            animation.SetVelocity(h.Motion.HorizontalSpeed,h.Motion.VerticalSpeed,now);
            transform.position=h.Ground.Apply(h.Motion.Position-Origin.position,h.Motion.Position.y,animation.Airborne || !h.Action.Grounded,now,h.Motion.Status=="snap_teleport");
            h.Action.ObserveTransform(h.Motion.HorizontalSpeed,h.Motion.VerticalSpeed,h.Motion.Position.y,h.Ground.Status=="ground_hit",now,h.Motion.Status=="snap_teleport" || h.Motion.Status=="resync");
            transform.rotation=Quaternion.Euler(0,-h.Motion.BodyYaw,0);UpdateAnimation();
        }
        public void Apply() { if (head != null) head.localRotation = bind * (Handle!=null&&Handle.MotionSettings.Enabled ? Handle.Motion.HeadRotation : Quaternion.Euler(Pitch, 0, 0)); }
        void Update() { if(Handle!=null&&Handle.MotionSettings.Enabled)RenderFrame();else UpdateAnimation(); }
        void LateUpdate() { Apply(); }
    }
}
