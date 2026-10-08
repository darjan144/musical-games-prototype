using UnityEngine;

/// <summary>
/// Little note pictures that stream out behind the bird while a note is held: one particle system per note, each with its own picture.
/// The bird never moves sideways, so the particles are given the world's speed to the left to look left behind.
/// </summary>
public class NoteTrail : MonoBehaviour
{
    [SerializeField] Transform bird;
    [Tooltip("Where on the bird the notes come out, from its centre, in world units.")]
    [SerializeField] Vector2 offset = new Vector2(-0.5f, 0f);
    [Tooltip("One per note, in order: C, E, G. They must simulate in World space.")]
    [SerializeField] ParticleSystem[] notes;

    float appliedSpeed = float.NaN;

    /// <summary>Call once a frame with the note being held (0 = C, 1 = E, 2 = G, -1 = none) and how fast the world moves left.</summary>
    public void Show(int level, float driftSpeed)
    {
        Vector3 position = bird.position + (Vector3)offset;
        position.z = transform.position.z;
        transform.position = position;

        for (int i = 0; i < notes.Length; i++)
        {
            var emission = notes[i].emission;
            emission.enabled = i == level;
        }

        if (driftSpeed == appliedSpeed) return;
        appliedSpeed = driftSpeed;
        foreach (var system in notes)
        {
            // A little spread in speed, so the notes don't travel in a rigid line.
            var velocity = system.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(-driftSpeed, -driftSpeed * 0.8f);
        }
    }
}
