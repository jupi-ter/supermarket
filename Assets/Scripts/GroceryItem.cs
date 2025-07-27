using System;
using Unity.Netcode;
using UnityEngine;

public enum GroceryNames
{
    None,
    Can,
    Apple
}

public interface IPickupable
{
    void OnPickup(Transform holdTransform);
    void OnDrop();
    bool CanPickup();
    NetworkObject GetNetworkObject();
}

public class GroceryData : INetworkSerializable, IEquatable<GroceryData>
{
    public GroceryNames Name = GroceryNames.None;

    public bool Equals(GroceryData other)
    {
        return Name == other.Name;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Name);
    }
}

//attached to the prefab
public class GroceryItem : NetworkBehaviour, IPickupable
{
    [SerializeField] private GroceryNames Name;
    [SerializeField] private Rigidbody rb;
    private NetworkObject networkObject;

    private Transform holdTransform;
    public NetworkVariable<GroceryData> Data = new(new GroceryData { });

    public override void OnNetworkSpawn()
    {
        networkObject = transform.GetComponent<NetworkObject>();
    }

    void FixedUpdate()
    {
        if (!IsOwner || holdTransform == null) return;

        rb.MovePosition(holdTransform.position);
    }

    public void OnPickup(Transform holdTransform)
    {
        if (!CanPickup()) return;

        rb.useGravity = false;
        this.holdTransform = holdTransform;
    }

    public void OnDrop()
    {
        holdTransform = null;
        rb.useGravity = true;
    }

    public bool CanPickup()
    {
        return true;
    }

    public NetworkObject GetNetworkObject()
    {
        return networkObject;
    }
}