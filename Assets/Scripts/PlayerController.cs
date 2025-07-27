using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float runSpeed = 12f;
    [SerializeField] private float jumpPower = 7f;
    [SerializeField] private float gravity = 10f;
    [SerializeField] private float lookSpeed = 2f;
    [SerializeField] private float lookXLimit = 45f;

    Vector3 moveDirection = Vector3.zero;
    float rotationX = 0;
    private bool canMove = true;

    //pickup items
    [SerializeField] private Transform heldObjectTransform;
    [SerializeField] private float pickupDistance = 5f;
    [SerializeField] private LayerMask pickupLayerMask;
    private bool areHandsBusy = false;
    private bool canPickup = true;

    private IPickupable currentPickupable;
    private NetworkObject heldNetworkObject;

    public override void OnNetworkSpawn()
    {
        if (IsLocalPlayer)
        {
            playerCamera.enabled = true;
            Camera.main.enabled = false;
            playerCamera.tag = "MainCamera";
            //pesky error
            playerCamera.GetComponent<AudioListener>().enabled = false;
        }
        else
        {
            playerCamera.enabled = false;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!IsOwner || !canMove) return;

        HandlePickup();
        HandleMovement();
        HandleRotation();
    }

    #region Pickup

    void HandlePickup()
    {
        if (!canPickup) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (areHandsBusy)
            {
                DropItem();
            }
            else
            {
                PickupItem();
            }
        }
    }

    void DropItem()
    {
        if (currentPickupable != null)
        {
            DropItemServerRpc(heldNetworkObject.NetworkObjectId);
            areHandsBusy = false;
        }
    }

    [ServerRpc]
    void DropItemServerRpc(ulong objectId)
    {
        DropItemClientRpc(objectId);
    }

    [ClientRpc]
    void DropItemClientRpc(ulong objectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out var obj))
        {
            if (obj.TryGetComponent(out IPickupable pickupable))
            {
                pickupable.OnDrop();
                heldNetworkObject = null;
            }
        }
    }

    void PickupItem()
    {
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit raycastHit, pickupDistance, pickupLayerMask))
        {
            if (raycastHit.transform.TryGetComponent(out NetworkObject netObj))
            {
                PickupItemServerRpc(netObj.NetworkObjectId);
            }
        }
    }

    [ServerRpc]
    void PickupItemServerRpc(ulong objectId, ServerRpcParams rpcParams = default)
    {
        NetworkObject obj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[objectId];

        if (obj.TryGetComponent(out IPickupable pickupable))
        {
            // Transfer ownership
            obj.ChangeOwnership(OwnerClientId);

            // Call pickup on client that owns it now
            PickupItemClientRpc(objectId, OwnerClientId);
        }
    }

    [ClientRpc]
    void PickupItemClientRpc(ulong objectId, ulong newOwnerId)
    {
        if (NetworkManager.Singleton.LocalClientId != newOwnerId) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out var obj))
        {
            if (obj.TryGetComponent(out IPickupable pickupable))
            {
                currentPickupable = pickupable;
                heldNetworkObject = pickupable.GetNetworkObject();
                pickupable.OnPickup(heldObjectTransform);
                areHandsBusy = true;
            }
        }
    }

    #endregion

    #region Movement and Rotation
    private void HandleMovement()
    {
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        // Press Left Shift to run
        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        float curSpeedX = canMove ? (isRunning ? runSpeed : walkSpeed) * Input.GetAxis("Vertical") : 0;
        float curSpeedY = canMove ? (isRunning ? runSpeed : walkSpeed) * Input.GetAxis("Horizontal") : 0;
        float movementDirectionY = moveDirection.y;
        moveDirection = (forward * curSpeedX) + (right * curSpeedY);

        if (Input.GetButton("Jump") && canMove && characterController.isGrounded)
        {
            moveDirection.y = jumpPower;
        }
        else
        {
            moveDirection.y = movementDirectionY;
        }

        if (!characterController.isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        characterController.Move(moveDirection * Time.deltaTime);
    }

    private void HandleRotation()
    {
        rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
    }
    #endregion
}
