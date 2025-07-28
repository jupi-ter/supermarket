using Unity.Netcode;
using UnityEngine;

public class ShoppingCart : NetworkBehaviour
{
    [SerializeField] private BoxCollider triggerCollider;

    public override void OnNetworkSpawn()
    {

    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.TryGetComponent(out GroceryItem groceryItem))
        {
            var groceryName = groceryItem.Data.Value.Name;
            Debug.Log($"groceryName {groceryName}");
        }
    }
}
