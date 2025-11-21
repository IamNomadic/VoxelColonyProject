using UnityEngine;

public static class PrimitiveMeshLibrary
{
    static Mesh _cube;
    public static Mesh CubeMesh
    {
        get
        {
            if (_cube != null) return _cube;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            MeshFilter mf = go.GetComponent<MeshFilter>();
            _cube = Object.Instantiate(mf.sharedMesh);
            Object.DestroyImmediate(go);
            return _cube;
        }
    }
}
