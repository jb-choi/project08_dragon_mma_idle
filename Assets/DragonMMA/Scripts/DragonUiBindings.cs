using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonMMA
{
    // Serialized keys stay stable when designers rename or rearrange GameObjects.
    public sealed class DragonUiBindings : MonoBehaviour
    {
        [Serializable] public struct Entry { public string key; public GameObject target; }
        [SerializeField] private List<Entry> elements = new List<Entry>();
        private Dictionary<string, GameObject> lookup;
        public IReadOnlyList<Entry> Elements => elements;
        public T Get<T>(string key) where T : Component
        {
            if (lookup == null)
            {
                lookup = new Dictionary<string, GameObject>();
                foreach (var item in elements) if (item.target != null) lookup[item.key] = item.target;
            }
            if (!lookup.TryGetValue(key, out var target))
                throw new InvalidOperationException(name + ": missing authored UI binding " + key);
            var component = target.GetComponent<T>();
            if (component == null) throw new InvalidOperationException(name + "/" + key + ": missing " + typeof(T).Name);
            return component;
        }
        public void Show(string key, bool visible) => Get<Transform>(key).gameObject.SetActive(visible);
#if UNITY_EDITOR
        public void CaptureEditorBindings()
        {
            elements.Clear(); lookup = null;
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform) continue;
                string path = child.name;
                Transform parent = child.parent;
                while (parent != transform && parent != null) { path = parent.name + "/" + path; parent = parent.parent; }
                elements.Add(new Entry { key = path, target = child.gameObject });
            }
        }
#endif
    }
}

