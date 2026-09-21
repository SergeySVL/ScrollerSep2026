using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public Rigidbody2D playerRb;
    public float speed;
    public float input;
    public SpriteRenderer spriteRenderer;
    public float jumpForce;
    public LayerMask groundLayer;
    public Transform feetPosition;
    public float groundCheckCircle;
    public InputAction playerControls;
    public InputAction playerJump;

    public float fallMultiplier = 2.5f;

    public float lowJumpMultiplier = 2f;

    private bool isGrounded;


    private void OnEnable()
    {
        playerControls.Enable();
        playerJump.Enable();
    }

    private void OnDisable()
    {
        playerControls.Disable();
        playerJump.Disable();
    }

    void Start()
    {

    }

    void Update()
    {
        input = playerControls.ReadValue<Vector2>().x;

        if (input < 0) {
            spriteRenderer.flipX = true;
        } else if (input > 0) {
            spriteRenderer.flipX = false;
        }
        

        isGrounded = Physics2D.OverlapCircle(feetPosition.position, groundCheckCircle, groundLayer);

        if (isGrounded && playerJump.triggered) {
            playerRb.linearVelocityY = Vector2.up.y * jumpForce;
        }

        if (playerRb.linearVelocity.y < 0)
        {
            playerRb.linearVelocityY += Vector2.up.y * Physics2D.gravity.y * (fallMultiplier - 1) * Time.deltaTime;
        }
        else if (playerRb.linearVelocity.y > 0 && !playerJump.IsPressed())
        {
            playerRb.linearVelocityY += Vector2.up.y * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime;
        }

    }

    void FixedUpdate()
    {
        playerRb.linearVelocity = new Vector2(input * speed, playerRb.linearVelocity.y);
    }



}
