using System.Collections.Generic;
using UnityEngine;

public class VoxelPool : MonoBehaviour
{
    [Tooltip("Prefab used as voxel. If null, a cube primitive will be created once at runtime.")]
    public GameObject voxelPrefab;
    public int initialCapacity = 1024;
    public bool allowExpand = true;

    Queue<GameObject> pool = new Queue<GameObject>();

    void Awake()
    {
        // Pre-warm pool
        for (int i = 0; i < initialCapacity; i++)
            pool.Enqueue(CreateNew());
    }

    GameObject CreateNew()
    {
        GameObject go;
        if (voxelPrefab != null)
        {
            go = Instantiate(voxelPrefab);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            // remove collider by default to avoid raycast self-blocking; keep if you need colliders
            var col = go.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
        }
        go.SetActive(false);
        go.transform.SetParent(this.transform, false);
        return go;
    }

    public GameObject Get()
    {
        if (pool.Count > 0)
        {
            var g = pool.Dequeue();
            g.SetActive(true);
            return g;
        }
        if (allowExpand) return CreateNew();
        return null;
    }

    public void Return(GameObject g)
    {
        if (g == null) return;
        g.SetActive(false);
        g.transform.SetParent(this.transform, false);
        pool.Enqueue(g);
    }

    public void ReturnAll(IEnumerable<GameObject> items)
    {
        foreach (var g in items) Return(g);
    }

    public void ClearPool()
    {
        while (pool.Count > 0) Destroy(pool.Dequeue());
    }
}
