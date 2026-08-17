using JetBrains.Annotations;
using System.Collections;
using UnityEngine;

[ExecuteInEditMode]
public class SetupTightGrab : MonoBehaviour {
    public Transform ModelObject;

    public Mesh Mesh;

    public bool Execute;

    public void OnValidate() {
        if (Execute) {
            if (Mesh is null) {
                Debug.LogError("[ATT Workshop] Please assign a custom mesh.");

                Execute = false;

                return;
            }

            GameObject obj = new GameObject("Tight Grab");

            if(ModelObject != null)
                obj.transform.localScale = ModelObject.localScale;

            obj.layer = 14;

            obj.transform.parent = transform;
            obj.transform.localPosition = Vector3.zero;

            Rigidbody rb = obj.AddComponent<Rigidbody>();

            rb.isKinematic = true;

            MeshCollider collider = obj.AddComponent<MeshCollider>();

            collider.convex = true;

            collider.sharedMesh = Mesh;

            Execute = false;

            Debug.Log($"[ATT Workshop] Successfully setup Tight Grab for {gameObject.name}!\nYou may remove this script now.");
        }
    }
}