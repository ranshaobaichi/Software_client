using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 10f;
    public float lifetime = 3f;

    private Vector2 m_direction;

    public void Initialize(Vector2 direction)
    {
        m_direction = direction.normalized;
    }

    private void Update()
    {
        transform.position +=
                (Vector3)m_direction *
                speed *
                Time.deltaTime;

        lifetime -= Time.deltaTime;

        if (lifetime <= 0)
        {
            Destroy(gameObject);
        }
    }
}