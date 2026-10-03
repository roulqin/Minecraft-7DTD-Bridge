using System;
using System.Globalization;
using UnityEngine;

namespace MC7DTD
{
    // Passive local geometric avatar: no EntityAlive, AI, animator, physics or equipment.
    public sealed class UnityPlayerProxyScene : IRotatingProxyScene
    {
        private sealed class Handle { public GameObject Object; public Material Material; }
        public object Create(string name, double x, double y, double z)
        {
            var handle = new Handle();
            try
            {
                handle.Object = new GameObject(name);
                var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("Player proxy shader unavailable");
                handle.Material = new Material(shader) { color = new Color(1f, 0.55f, 0.1f) };
                Part(handle, "body", new Vector3(0, 0.95f, 0), new Vector3(0.6f, 0.9f, 0.3f));
                Part(handle, "head", new Vector3(0, 1.65f, 0), new Vector3(0.4f, 0.4f, 0.4f));
                Part(handle, "nose", new Vector3(0, 1.65f, 0.28f), new Vector3(0.14f, 0.14f, 0.18f));
                Part(handle, "left-leg", new Vector3(-0.17f, 0.25f, 0), new Vector3(0.22f, 0.5f, 0.25f));
                Part(handle, "right-leg", new Vector3(0.17f, 0.25f, 0), new Vector3(0.22f, 0.5f, 0.25f));
                Move(handle, x, y, z);
                Log.Out("[MC7DTD] Player proxy Unity created: name=" + name + " instance=" + handle.Object.GetInstanceID() + " parts=5 colliderEnabled=false");
                return handle;
            }
            catch { Delete(handle); throw; }
        }
        private static void Part(Handle handle, string name, Vector3 position, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.transform.SetParent(handle.Object.transform, false);
            part.name = name; part.transform.localPosition = position; part.transform.localScale = scale;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false; UnityEngine.Object.Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial = handle.Material;
        }
        public void Move(object value, double x, double y, double z)
        { ((Handle)value).Object.transform.position = new Vector3((float)x, (float)y, (float)z) - Origin.position; }
        public void Rotate(object value, double yaw, double pitch, double roll)
        {
            var y = yaw * Math.PI / 180; var p = pitch * Math.PI / 180;
            var forward = new Vector3((float)(-Math.Sin(y) * Math.Cos(p)), (float)-Math.Sin(p), (float)(Math.Cos(y) * Math.Cos(p)));
            var right = new Vector3((float)Math.Cos(y), 0, (float)Math.Sin(y));
            var up = Vector3.Cross(forward, right).normalized;
            ((Handle)value).Object.transform.rotation = Quaternion.LookRotation(forward, Quaternion.AngleAxis((float)roll, forward) * up);
        }
        public void Delete(object value)
        {
            var handle = (Handle)value;
            if (handle.Object != null)
            {
                var id = handle.Object.GetInstanceID(); handle.Object.SetActive(false);
                UnityEngine.Object.Destroy(handle.Object);
                Log.Out("[MC7DTD] Player proxy Unity deleted: instance=" + id + " inactive=true destroyScheduled=true");
            }
            if (handle.Material != null) UnityEngine.Object.Destroy(handle.Material);
        }
    }
}
