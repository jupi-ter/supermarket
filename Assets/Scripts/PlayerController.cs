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
    [SerializeField] private Transform holdObjectTransform; // "hands"
    [SerializeField] private float pickupDistance = 10f;
    [SerializeField] private LayerMask pickupLayerMask;
    private bool areHandsBusy = false;
    private bool canPickup = true;
    private GameObject heldObject;
    private IPickupable currentPickupable;

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
    void PickupObject()
    {
        if (heldObject == null || !heldObject.TryGetComponent<IPickupable>(out var pickupable) || !canPickup)
            return;

        // Only the owner initiates the pickup
        if (IsOwner)
        {
            pickupable.OnPickup(holdObjectTransform);
        }

        areHandsBusy = true;
        currentPickupable = pickupable;
    }

    void CheckForPickupableObject()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        if (Physics.Raycast(ray, out RaycastHit hit, pickupDistance, pickupLayerMask))
        {
            // Check for IPickupable instead of a specific component
            if (hit.collider.TryGetComponent<IPickupable>(out var pickupable))
            {
                if (pickupable.CanPickup()) // Optional check
                {
                    heldObject = hit.collider.gameObject;
                    currentPickupable = pickupable; // Store the interface reference
                }
            }
        }
        else
        {
            heldObject = null;
            currentPickupable = null;
        }
    }

    void CheckForPickupableObjectDebugRay()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        // Visualize the ray (visible in Scene view and Game view when Gizmos are enabled)
        Debug.DrawRay(ray.origin, ray.direction * pickupDistance, Color.green);

        if (Physics.Raycast(ray, out RaycastHit hit, pickupDistance, pickupLayerMask))
        {
            // Check for IPickupable instead of a specific component
            if (hit.collider.TryGetComponent<IPickupable>(out var pickupable))
            {
                if (pickupable.CanPickup()) // Optional check
                {
                    heldObject = hit.collider.gameObject;
                    currentPickupable = pickupable; // Store the interface reference

                    // Optional: Highlight the object that can be picked up
                    Debug.DrawLine(ray.origin, hit.point, Color.red);
                }
            }
        }
        else
        {
            heldObject = null;
            currentPickupable = null;
        }
    }

    void DropObject()
    {
        if (currentPickupable == null) return;

        currentPickupable.OnDrop();
        heldObject = null;
        currentPickupable = null;
        areHandsBusy = false;
    }

    void HandlePickup()
    {
        // Check if we're looking at a pickupable object
        CheckForPickupableObject();
        //CheckForPickupableObjectDebugRay();

        if (Input.GetButtonDown("Interact"))
        {
            if (areHandsBusy)
            {
                DropObject();
            }
            else if (heldObject != null)
            {
                PickupObject();
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
