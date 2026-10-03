using System;
using System.Globalization;
using UnityEngine;

namespace MC7DTD
{
    // Local visual object only: not an EntityAlive, no AI, physics or save data.
    public sealed class UnityMarkerScene : IMarkerScene
    {
        private sealed class Handle { public GameObject Object; public Material Material; }
        public object Create(string name, double x, double y, double z)
        {
            var handle = new Handle();
            try
            {
                handle.Object = GameObject.CreatePrimitive(PrimitiveType.Cube);
                handle.Object.name = name;
                var collider = handle.Object.GetComponent<Collider>();
                if (collider != null) { collider.enabled = false; UnityEngine.Object.Destroy(collider); }
                var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("Marker shader unavailable");
                handle.Material = new Material(shader) { color = Color.cyan };
                handle.Object.GetComponent<Renderer>().sharedMaterial = handle.Material;
                Move(handle, x, y, z);
                Log.Out(string.Format(CultureInfo.InvariantCulture,
                    "[MC7DTD] Marker Unity created: instance={0} name={1} active={2} renderer={3} colliderEnabled={4} world=({5:R},{6:R},{7:R}) origin={8}",
                    handle.Object.GetInstanceID(), name, handle.Object.activeInHierarchy,
                    handle.Object.GetComponent<Renderer>().enabled, collider != null && collider.enabled, x, y, z, Origin.position));
                return handle;
            }
            catch { Delete(handle); throw; }
        }
        public void Move(object value, double x, double y, double z)
        {
            var handle = (Handle)value;
            if (handle.Object == null) throw new InvalidOperationException("Marker object lost");
            // Wire position is the bottom center; the unit cube's transform is its center.
            handle.Object.transform.position = new Vector3((float)x, (float)y + 0.5f, (float)z) - Origin.position;
        }
        public void Delete(object value)
        {
            var handle = (Handle)value;
            if (handle.Object != null)
            {
                var id = handle.Object.GetInstanceID();
                handle.Object.SetActive(false);
                UnityEngine.Object.Destroy(handle.Object);
                Log.Out("[MC7DTD] Marker Unity deleted: instance=" + id + " inactive=true destroyScheduled=true");
            }
            if (handle.Material != null) UnityEngine.Object.Destroy(handle.Material);
        }
    }
}
