using UnityEngine;

/// <summary>
/// Switches the bird's Animator between its looping Idle (on the ground) and Fly (anywhere in the air) animations.
/// </summary>
[RequireComponent(typeof(Animator))]
public class BirdAnimation : MonoBehaviour
{
    static readonly int ToIdle = Animator.StringToHash("ToIdle");
    static readonly int ToFly = Animator.StringToHash("ToFly");

    Animator animator;
    bool flying;   // the Animator starts in Idle

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    /// <summary>Call every frame; the trigger is only sent when the state changes.</summary>
    public void SetFlying(bool value)
    {
        if (value == flying) return;
        flying = value;

        // Clear the other trigger in case it was set and not used yet, so it can't fire later.
        animator.ResetTrigger(value ? ToIdle : ToFly);
        animator.SetTrigger(value ? ToFly : ToIdle);
    }
}
