using System.Collections.Generic;
using UnityEngine;

public class Blackboard
{
    private Dictionary<string, object> data = new Dictionary<string, object>();

    public void Set<T>(string key, T value) { data[key] = value; }
    public T Get<T>(string key)
    {
        if (data.TryGetValue(key, out object value) && value is T typed) return typed;
        return default;
    }
    public bool Has(string key) => data.ContainsKey(key);
}