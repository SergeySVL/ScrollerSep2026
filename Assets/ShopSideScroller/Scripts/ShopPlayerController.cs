using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Small, physics-driven side-scroller. The root pivot stays at the feet.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public sealed class ShopPlayerController : MonoBehaviour
{
    [Header("Foot-aligned sprite frames")]
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite[] runFrames;
    [SerializeField] private Sprite[] jumpFrames;
    [SerializeField] private Sprite[] crouchFrames;
    [SerializeField] private Sprite[] actionFrames;
    public bool spriteFacesRight = true;

    [Header("Movement")]
    [Min(0f)] public float walkSpeed = 4.5f;
    [Min(0f)] public float sprintSpeed = 7f;
    [Min(0f)] public float crouchSpeed = 2f;
    [Min(1f)] public float acceleration = 45f;
    [Min(1f)] public float braking = 65f;
    [Min(0f)] public float jumpSpeed = 8.5f;
    [Min(0f)] public float gravityScale = 2.5f;
    [Min(1f)] public float fallGravityMultiplier = 1.6f;
    [Range(0.05f, 1f)] public float jumpReleaseMultiplier = 0.45f;
    [Min(0f)] public float coyoteSeconds = 0.12f;
    [Min(0f)] public float jumpBufferSeconds = 0.14f;
    [Min(1f)] public float maximumFallSpeed = 18f;

    [Header("Collider and respawn")]
    [Min(0.1f)] public float standingHeight = 1.35f;
    [Min(0.1f)] public float bodyWidth = 0.70f;
    [Min(0.1f)] public float crouchingHeight = 0.82f;
    public LayerMask groundAndCeilingMask = ~0;
    public float killY = -25f;
    [SerializeField] private Vector3 respawnPoint;
    [SerializeField] private bool spawnPointConfigured;

    [Header("Animation")]
    [Min(0.1f)] public float idleFramesPerSecond = 5f;
    [Min(0.1f)] public float runFramesPerSecond = 10f;
    [Min(0.1f)] public float jumpFramesPerSecond = 8f;
    [Min(0.1f)] public float crouchFramesPerSecond = 6f;
    [Min(0.1f)] public float actionFramesPerSecond = 10f;

    public bool Grounded { get; private set; }
    public bool Crouching { get; private set; }
    public string CurrentAnimation { get; private set; } = "Idle";
    public Vector3 RespawnPoint => respawnPoint;

    private Rigidbody2D body;
    private CapsuleCollider2D capsule;
    private readonly ContactPoint2D[] contacts = new ContactPoint2D[32];
    private readonly Collider2D[] standingObstacles = new Collider2D[32];
    private float moveInput;
    private bool jumpHeld;
    private bool previousJumpHeld;
    private bool crouchInput;
    private bool sprintInput;
    private bool jumpReleasePending;
    private bool respawnPending;
    private bool hasFocus = true;
    private bool jumpConsumed;
    private float jumpBufferedUntil = float.NegativeInfinity;
    private float lastGroundedAt = float.NegativeInfinity;
    private float suppressGroundUntil = float.NegativeInfinity;
    private float actionUntil = float.NegativeInfinity;
    private float animationStartedAt;

    private struct InputFrame
    {
        public float move;
        public bool jumpPressed, jumpHeld, crouch, sprint, action, respawn;
    }

#if UNITY_EDITOR
    private bool testInputEnabled;
    private InputFrame testInput;

    /// <summary>Held values persist; pressed/action/respawn flags are consumed once by Update.</summary>
    public void SetTestInput(float move, bool jumpPressed, bool jumpHeld, bool crouch,
        bool sprint, bool action, bool respawn)
    {
        testInputEnabled = true;
        testInput.move = Mathf.Clamp(move, -1f, 1f);
        testInput.jumpPressed |= jumpPressed;
        testInput.jumpHeld = jumpHeld;
        testInput.crouch = crouch;
        testInput.sprint = sprint;
        testInput.action |= action;
        testInput.respawn |= respawn;
    }

    public void ClearTestInput()
    {
        ResetInputState(true);
        if (body != null) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }
#endif

    public void Configure(SpriteRenderer renderer, Sprite[] idle, Sprite[] run, Sprite[] jump,
        Sprite[] crouch, Sprite[] action, Vector3 spawnPoint)
    {
        visual = renderer;
        idleFrames = idle;
        runFrames = run;
        jumpFrames = jump;
        crouchFrames = crouch;
        actionFrames = action;
        respawnPoint = spawnPoint;
        spawnPointConfigured = true;
        killY = spawnPoint.y - 20f;
        CacheAndConfigureBody();
        SetCrouching(false);
        if (visual != null && HasFrames(idleFrames)) visual.sprite = idleFrames[0];
    }

    private void Awake()
    {
        CacheAndConfigureBody();
        if (!spawnPointConfigured) respawnPoint = transform.position;
        SetCrouching(false);
        animationStartedAt = Time.time;
    }

    private void CacheAndConfigureBody()
    {
        body = GetComponent<Rigidbody2D>();
        capsule = GetComponent<CapsuleCollider2D>();
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = gravityScale;
        body.linearDamping = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        capsule.direction = CapsuleDirection2D.Vertical;
        capsule.isTrigger = false;
    }

    private void Update()
    {
        InputFrame input = ReadInput();
        moveInput = Mathf.Clamp(input.move, -1f, 1f);
        jumpHeld = input.jumpHeld;
        crouchInput = input.crouch;
        sprintInput = input.sprint;
        respawnPending |= input.respawn;
        if (input.jumpPressed) jumpBufferedUntil = Time.time + jumpBufferSeconds;
        if (previousJumpHeld && !jumpHeld) jumpReleasePending = true;
        previousJumpHeld = jumpHeld;
        if (input.action && Grounded && HasFrames(actionFrames))
        {
            actionUntil = Time.time + actionFrames.Length / Mathf.Max(0.1f, actionFramesPerSecond);
            if (CurrentAnimation == "Action") animationStartedAt = Time.time;
        }
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        if (respawnPending || body.position.y < killY)
        {
            Respawn();
            return;
        }

        float now = Time.time;
        RefreshGrounded(now);
        if (Grounded)
        {
            lastGroundedAt = now;
            jumpConsumed = false;
        }

        bool wantsCrouch = crouchInput && Grounded;
        if (wantsCrouch) SetCrouching(true);
        else if (Crouching && CanStand()) SetCrouching(false);

        Vector2 velocity = body.linearVelocity;
        float speed = Crouching ? crouchSpeed : sprintInput ? sprintSpeed : walkSpeed;
        float targetSpeed = moveInput * speed;
        float changeRate = Mathf.Abs(moveInput) < 0.01f ? braking : acceleration;
        velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, changeRate * Time.fixedDeltaTime);

        bool mayJump = !jumpConsumed && (Grounded || now - lastGroundedAt <= coyoteSeconds);
        if (jumpBufferedUntil >= now && mayJump && (!Crouching || CanStand()))
        {
            SetCrouching(false);
            velocity.y = jumpSpeed;
            Grounded = false;
            jumpConsumed = true;
            lastGroundedAt = float.NegativeInfinity;
            suppressGroundUntil = now + 0.10f;
            jumpBufferedUntil = float.NegativeInfinity;
            actionUntil = float.NegativeInfinity;
            // A press released before the physics step is still a valid short jump.
            if (!jumpHeld) velocity.y *= jumpReleaseMultiplier;
            jumpReleasePending = false;
        }

        if (jumpReleasePending && velocity.y > 0f) velocity.y *= jumpReleaseMultiplier;
        jumpReleasePending = false;
        if (velocity.y < 0f)
            velocity.y += Physics2D.gravity.y * body.gravityScale * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime;
        velocity.y = Mathf.Max(velocity.y, -maximumFallSpeed);
        body.linearVelocity = velocity;
    }

    private ContactFilter2D SolidFilter()
    {
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(groundAndCeilingMask);
        return filter;
    }

    private bool OwnedCollider(Collider2D other)
    {
        return other != null && (other.attachedRigidbody == body || other.transform.IsChildOf(transform));
    }

    private void RefreshGrounded(float now)
    {
        Grounded = false;
        if (now < suppressGroundUntil) return;
        int count = capsule.GetContacts(SolidFilter(), contacts);
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = contacts[i];
            Collider2D other = OwnedCollider(contact.collider) ? contact.otherCollider : contact.collider;
            if (other == null || OwnedCollider(other) || other.isTrigger) continue;
            // A wall or ceiling never refills the jump. Ignore lingering take-off contacts.
            if (contact.normal.y >= 0.65f && body.linearVelocity.y <= 0.5f)
            {
                Grounded = true;
                return;
            }
        }
    }

    private bool CanStand()
    {
        const float skin = 0.025f;
        Vector2 center = transform.TransformPoint(new Vector3(0f, standingHeight * 0.5f, 0f));
        Vector3 scale = transform.lossyScale;
        Vector2 size = new Vector2(Mathf.Max(0.05f, bodyWidth - skin * 2f) * Mathf.Abs(scale.x),
            Mathf.Max(0.05f, standingHeight - skin * 2f) * Mathf.Abs(scale.y));
        int count = Physics2D.OverlapCapsule(center, size, CapsuleDirection2D.Vertical,
            body.rotation, SolidFilter(), standingObstacles);
        for (int i = 0; i < count; i++)
        {
            Collider2D other = standingObstacles[i];
            if (other != null && other.enabled && !other.isTrigger && !OwnedCollider(other)) return false;
        }
        // Conservatively refuse expansion if an unusually crowded query filled the buffer.
        return count < standingObstacles.Length;
    }

    private void SetCrouching(bool value)
    {
        Crouching = value;
        float width = Mathf.Max(0.1f, bodyWidth);
        float height = Mathf.Max(width, value ? crouchingHeight : standingHeight);
        capsule.size = new Vector2(width, height);
        capsule.offset = new Vector2(0f, height * 0.5f);
    }

    public void Respawn()
    {
        ResetInputState(false);
        SetCrouching(false);
        Grounded = false;
        jumpConsumed = false;
        lastGroundedAt = float.NegativeInfinity;
        suppressGroundUntil = Time.time + 0.05f;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.position = respawnPoint;
        transform.position = respawnPoint;
        body.WakeUp();
    }

    private InputFrame ReadInput()
    {
#if UNITY_EDITOR
        if (testInputEnabled)
        {
            InputFrame result = testInput;
            testInput.jumpPressed = testInput.action = testInput.respawn = false;
            return result;
        }
#endif
        var input = new InputFrame();
        if (!hasFocus) return input;
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return input;
        input.move = ((keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) ? 1f : 0f)
            - ((keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) ? 1f : 0f);
        input.jumpHeld = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed || keyboard.spaceKey.isPressed;
        input.jumpPressed = keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame;
        input.crouch = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
        input.sprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        input.action = keyboard.jKey.wasPressedThisFrame;
        input.respawn = keyboard.rKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        input.move = ((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) ? 1f : 0f)
            - ((Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) ? 1f : 0f);
        input.jumpHeld = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.Space);
        input.jumpPressed = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Space);
        input.crouch = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        input.sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        input.action = Input.GetKeyDown(KeyCode.J);
        input.respawn = Input.GetKeyDown(KeyCode.R);
#endif
        return input;
    }

    private static bool HasFrames(Sprite[] frames) => frames != null && frames.Length > 0;

    private void UpdateAnimation()
    {
        if (visual == null) return;
        if (Mathf.Abs(moveInput) > 0.01f) visual.flipX = (moveInput < 0f) == spriteFacesRight;
        string state;
        Sprite[] frames;
        float fps;
        bool loop = true;
        if (!Grounded)
        {
            state = body.linearVelocity.y > 0.1f ? "Jump" : "Fall";
            frames = jumpFrames;
            fps = jumpFramesPerSecond;
            loop = false;
        }
        else if (Time.time < actionUntil && HasFrames(actionFrames))
        {
            state = "Action";
            frames = actionFrames;
            fps = actionFramesPerSecond;
            loop = false;
        }
        else if (Crouching)
        {
            state = "Crouch";
            frames = crouchFrames;
            fps = crouchFramesPerSecond;
        }
        else if (Mathf.Abs(body.linearVelocity.x) > 0.1f && Mathf.Abs(moveInput) > 0.01f)
        {
            state = "Run";
            frames = runFrames;
            fps = runFramesPerSecond * (sprintInput ? 1.25f : 1f);
        }
        else
        {
            state = "Idle";
            frames = idleFrames;
            fps = idleFramesPerSecond;
        }
        if (CurrentAnimation != state)
        {
            CurrentAnimation = state;
            animationStartedAt = Time.time;
        }
        if (!HasFrames(frames)) frames = idleFrames;
        if (!HasFrames(frames)) return;
        int elapsedFrames = Mathf.Max(0, Mathf.FloorToInt((Time.time - animationStartedAt) * Mathf.Max(0.1f, fps)));
        int frame = loop ? elapsedFrames % frames.Length : Mathf.Min(elapsedFrames, frames.Length - 1);
        if (frames[frame] != null) visual.sprite = frames[frame];
    }

    private void ResetInputState(bool clearTest)
    {
        moveInput = 0f;
        jumpHeld = previousJumpHeld = crouchInput = sprintInput = false;
        jumpReleasePending = respawnPending = false;
        jumpBufferedUntil = actionUntil = float.NegativeInfinity;
#if UNITY_EDITOR
        testInput.jumpPressed = testInput.action = testInput.respawn = false;
        if (clearTest)
        {
            testInputEnabled = false;
            testInput = default;
        }
#endif
    }

    private void OnApplicationFocus(bool focused)
    {
        hasFocus = focused;
        if (focused) return;
        ResetInputState(true);
        if (body != null) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    private void OnDisable()
    {
        ResetInputState(true);
        if (body != null) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }
}
