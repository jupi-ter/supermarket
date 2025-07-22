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
    private NetworkVariable<ulong> currentOwnerId = new(ulong.MaxValue);
    private NetworkVariable<bool> isBeingHeld = new();
    private Transform originalParent;

    public NetworkVariable<GroceryData> Data = new(new GroceryData { });

    public override void OnNetworkSpawn()
    {
        originalParent = transform.parent;

        if (isBeingHeld.Value && rb != null)
        {
            rb.isKinematic = true;
        }
    }

    public void OnPickup(Transform holdTransform)
    {
        if (!CanPickup()) return;
        RequestPickupServerRpc(holdTransform.GetComponent<NetworkObject>());
    }

    public void OnDrop()
    {
        if (!isBeingHeld.Value) return;
        RequestDropServerRpc();
    }

    public bool CanPickup()
    {
        // Only allow pickup if not held by anyone
        return !isBeingHeld.Value && IsClient;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(NetworkObjectReference holderRef, ServerRpcParams rpcParams = default)
    {
        // Server-side validation
        if (isBeingHeld.Value) return; // Already held

        if (!holderRef.TryGet(out NetworkObject holderObject)) return;

        // Grant ownership
        currentOwnerId.Value = rpcParams.Receive.SenderClientId;
        isBeingHeld.Value = true;

        // Update transform
        transform.SetParent(holderObject.transform);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        // Update physics
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Sync to requesting client only (others will sync via NetworkVariable)
        UpdatePickupStateClientRpc(
            true,
            holderRef,
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { rpcParams.Receive.SenderClientId }
                }
            }
        );
    }

    [ServerRpc]
    private void RequestDropServerRpc(ServerRpcParams rpcParams = default)
    {
        // Validate requester is current owner
        if (!isBeingHeld.Value || currentOwnerId.Value != rpcParams.Receive.SenderClientId) return;

        // Release ownership
        currentOwnerId.Value = ulong.MaxValue;
        isBeingHeld.Value = false;

        // Reset parent
        transform.SetParent(originalParent);

        // Update physics
        if (rb != null) rb.isKinematic = false;

        // Broadcast drop to all clients
        UpdatePickupStateClientRpc(false, new NetworkObjectReference(originalParent.gameObject));
    }

    [ClientRpc]
    private void UpdatePickupStateClientRpc(bool pickedUp, NetworkObjectReference parentRef, ClientRpcParams _ = default)
    {
        parentRef.TryGet(out NetworkObject parentObject);
        transform.SetParent(pickedUp ? parentObject.transform : originalParent);

        if (rb != null)
        {
            rb.isKinematic = pickedUp;
            if (pickedUp)
            {
                transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
        }
    }
}
