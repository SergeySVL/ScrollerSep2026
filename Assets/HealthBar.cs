using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class HealthBar : MonoBehaviour
{
    [SerializeField]
    private Image healthFill;

    [SerializeField]
    private float maxHealth = 100f;

    private float currentHealth;

    private void Start()
    {
        currentHealth = maxHealth;

        UpdateHealthBar();
    }

    private void Update()
    {
        // Temporary testing key.
        // Press H while the game is running.
        if (
    Keyboard.current != null &&
    Keyboard.current.hKey.wasPressedThisFrame
)
        {
            TakeDamage(10f);
        }
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;

        currentHealth = Mathf.Clamp(
            currentHealth,
            0f,
            maxHealth
        );

        Debug.Log(
            "Damage taken! Current HP: " +
            currentHealth
        );

        UpdateHealthBar();
    }

    public void Heal(float amount)
    {
        currentHealth += amount;

        currentHealth = Mathf.Clamp(
            currentHealth,
            0f,
            maxHealth
        );

        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        healthFill.fillAmount =
            currentHealth / maxHealth;
    }
}