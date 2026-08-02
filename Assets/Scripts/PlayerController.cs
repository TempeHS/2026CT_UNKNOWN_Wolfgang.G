using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;


public class PlayerController : MonoBehaviour
{
    // Movement
    public float moveSpeed = 5f;
    public float acceleration = 60f;
    public float deceleration = 80f;
    public float airAcceleration = 30f;
    public float airDeceleration = 40f;

    // Jump
    public float jumpForce = 16f;
    public float delayedJumpTime = 0.15f;
    public float jumpBufferTime = 0.15f;
    private float delayedJumpTimeCounter;
    private float jumpBufferCounter;
    private bool jumpHeld;
    private bool jumpConsumed;  

    // Dash
    public float dashSpeed = 10;
    public float dashCooldown = 1f;
    public float dashDuration = 0.5f;
    private float dashDirection;

    // Attack
    public float attackCooldown = 0.25f;
    public float attackDuration = 1f;
    public GameObject whipParticlePrefab;      
    public GameObject whipParticlePrefabUp;     
    public GameObject whipParticlePrefabDown;   
    [Range(0f, 1f)] public float attackMoveMultiplier = 0.35f; 
    public float vfxRotationOffset = 0f; // add this
    private float attackCooldownTimer;
    public float attackInputThreshold = 0.5f;
    public bool allowDownAttackOnGround = false;
    private Vector2 currentAttackDirection = Vector2.right;
    public float whipRadius = 0.5f;
    public float attackDamage = 1f;
    public LayerMask attackLayer;

    // Checks
    public Transform groundCheck;
    public Transform groundCheck2;
    public float groundCheckRadius = 0.25f;
    public LayerMask groundLayer;
    public Transform attackPoint;      
    public Transform attackPointUp;     
    public Transform attackPointDown;  
    private bool isGrounded;
    private bool facingRight = true;
    private bool isAttacking;
    private bool isDashing;
    public bool canDash;

    // Input
    private float moveInput;
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        canDash = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        bool groundedA = groundCheck != null &&
                     Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        bool groundedB = groundCheck2 != null &&
                     Physics2D.OverlapCircle(groundCheck2.position, groundCheckRadius, groundLayer);

        isGrounded = groundedA || groundedB;

        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer -= Time.deltaTime;
        }

        if (isGrounded && rb.linearVelocity.y <= 0.01f)
        {
            delayedJumpTimeCounter = delayedJumpTime;
            jumpConsumed = false;
        }
        else
        {
            delayedJumpTimeCounter -= Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        if (Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0) 
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
        }

        jumpHeld = Input.GetButton("Jump");

        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
        {
            StartCoroutine(Dash());
        }

        if (moveInput > 0 && !facingRight)
            Flip();
        else if (moveInput < 0 && facingRight)
            Flip();

        dashDirection = facingRight ? 1 : -1;

        if (Input.GetButtonDown("Fire1") && attackCooldownTimer <= 0f && !isDashing)
        {
            currentAttackDirection = GetAttackDirection();
            StartCoroutine(WhipAttack());
        }
    }
    
    IEnumerator Dash()
    {
        isDashing = true;
        canDash = false;
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, rb.linearVelocity.y);
        yield return new WaitForSeconds(dashDuration);
        isDashing = false;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    private Vector2 GetAttackDirection()
    {
        float vertical = Input.GetAxisRaw("Vertical");

        if (vertical > attackInputThreshold)
            return Vector2.up;

        if (vertical < -attackInputThreshold && (!isGrounded || allowDownAttackOnGround))
            return Vector2.down;

        return facingRight ? Vector2.right : Vector2.left;
    }

    private Transform GetAttackPoint(Vector2 direction)
    {
        if (direction == Vector2.up && attackPointUp != null)
            return attackPointUp;

        if (direction == Vector2.down && attackPointDown != null)
            return attackPointDown;

        return attackPoint;
    }

    private GameObject GetAttackVfxPrefab(Vector2 direction)
    {
        if (direction == Vector2.up && whipParticlePrefabUp != null)
            return whipParticlePrefabUp;

        if (direction == Vector2.down && whipParticlePrefabDown != null)
            return whipParticlePrefabDown;

        return whipParticlePrefab;
    }

    private Quaternion GetAttackRotation(Vector2 direction)
    {
        return GetAttackRotation(direction, GetAttackVfxPrefab(direction));
    }

    private Quaternion GetAttackRotation(Vector2 direction, GameObject selectedVfx)
    {
        // If using dedicated vertical prefabs, assume they are already oriented correctly.
        if (direction == Vector2.up && selectedVfx == whipParticlePrefabUp && whipParticlePrefabUp != null)
            return Quaternion.Euler(0f, 0f, vfxRotationOffset);

        if (direction == Vector2.down && selectedVfx == whipParticlePrefabDown && whipParticlePrefabDown != null)
            return Quaternion.Euler(0f, 0f, vfxRotationOffset);

        // Fallback rotation for shared prefab
        if (direction == Vector2.up) return Quaternion.Euler(0f, 0f, 90f + vfxRotationOffset);
        if (direction == Vector2.down) return Quaternion.Euler(0f, 0f, -90f + vfxRotationOffset);
        if (direction == Vector2.left) return Quaternion.Euler(0f, 0f, 180f + vfxRotationOffset);
        return Quaternion.Euler(0f, 0f, 0f + vfxRotationOffset); // right
    }

    private Vector2 GetAttackSpawnPosition(Vector2 direction, Transform spawnPoint)
    {
        if (spawnPoint == null) return transform.position;
        return spawnPoint.position; // use the actual attack point for all directions
    }

    IEnumerator WhipAttack()
    {
        isAttacking = true;
        attackCooldownTimer = attackCooldown;

        Vector2 direction = currentAttackDirection.normalized;
        Transform spawnPoint = GetAttackPoint(direction);
        GameObject selectedVfx = GetAttackVfxPrefab(direction);

        if (selectedVfx != null && spawnPoint != null)
        {
            Vector2 spawnPos = GetAttackSpawnPosition(direction, spawnPoint);
            Quaternion rot = GetAttackRotation(direction, selectedVfx);
            GameObject vfx = Instantiate(selectedVfx, spawnPos, rot);
            Destroy(vfx, 0.05f);
        }

        Vector2 playerPos = transform.position;
        Vector2 origin = spawnPoint.position;
        Vector2 toAttackPoint = origin - playerPos;
        float castDistance = toAttackPoint.magnitude;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(playerPos, whipRadius, toAttackPoint.normalized, castDistance, attackLayer);
        HashSet<Collider2D> hitOnce = new HashSet<Collider2D>();

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || hit.collider.attachedRigidbody == rb) continue;
            if (hitOnce.Contains(hit.collider)) continue;

            hitOnce.Add(hit.collider);
            hit.collider.gameObject.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
        }

        yield return new WaitForSeconds(attackDuration);
        isAttacking = false;
    }

    private void FixedUpdate()
    {
        if (isDashing) return;

        float moveScale = isAttacking ? attackMoveMultiplier : 1f;
        float targetSpeed = moveInput * moveSpeed * moveScale;

        float accelRate = isGrounded
            ? (Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration)
            : (Mathf.Abs(targetSpeed) > 0.01f ? airAcceleration : airDeceleration);

        float newX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, accelRate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);

        if (jumpBufferCounter > 0 && delayedJumpTimeCounter > 0 && !jumpConsumed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferCounter = 0;
            delayedJumpTimeCounter = 0;
            jumpConsumed = true;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        if (groundCheck != null)
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);

        if (groundCheck2 != null)
            Gizmos.DrawWireSphere(groundCheck2.position, groundCheckRadius);

        if (attackPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(attackPoint.position, 0.2f);
        }

        if (attackPointUp != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(attackPointUp.position, 0.2f);
        }

        if (attackPointDown != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(attackPointDown.position, 0.2f);
        }
    }

    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;
    }
}