using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Protector))]
public class ProtectorVisuals : MonoBehaviour
{
    [Header("Visual Configuration")]
    [SerializeField] private string deathStateName = "OVER";
    [SerializeField, Min(0.05f)] private float destroyDelaySeconds = 2f;
    [SerializeField] private bool flipByVelocity = true;
    [SerializeField] private bool faceRightWhenVelocityPositive = false;

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Protector protector;
    private PlayableGraph deathPlayableGraph;
    private bool isDeathPlayableActive;

    private int deathStateHash;
    private int deathStateFullPathHash;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        protector = GetComponent<Protector>();

        deathStateHash = Animator.StringToHash(deathStateName);
        deathStateFullPathHash = Animator.StringToHash($"Base Layer.{deathStateName}");
    }

    private void OnEnable()
    {
        if (protector != null)
        {
            protector.OnDeath += HandleDeathAnimation;
        }
    }

    private void OnDisable()
    {
        if (protector != null)
        {
            protector.OnDeath -= HandleDeathAnimation;
        }

        StopDeathPlayable();
    }

    private void Update()
    {
        if (protector == null || protector.IsDead) return;
        if (!flipByVelocity || spriteRenderer == null || protector.Rigidbody == null) return;

        float velocityX = protector.Rigidbody.linearVelocity.x;
        if (velocityX > 0.01f) spriteRenderer.flipX = !faceRightWhenVelocityPositive;
        else if (velocityX < -0.01f) spriteRenderer.flipX = faceRightWhenVelocityPositive;
    }

    private void HandleDeathAnimation()
    {
        if (animator == null)
        {
            Destroy(gameObject, Mathf.Max(0.05f, destroyDelaySeconds));
            return;
        }

        if (protector != null && protector.deathAnimationClip != null)
        {
            float clipDelay = Mathf.Max(0.05f, protector.deathAnimationClip.length);
            PlayDeathClip(protector.deathAnimationClip);
            Destroy(gameObject, clipDelay);
            return;
        }

        // Force the state directly so death can play from any current animation.
        animator.enabled = true;
        animator.Rebind();
        animator.Update(0f);

        bool hasDeathState = animator.HasState(0, deathStateFullPathHash) || animator.HasState(0, deathStateHash);
        if (hasDeathState)
        {
            int stateHash = animator.HasState(0, deathStateFullPathHash) ? deathStateFullPathHash : deathStateHash;
            animator.Play(stateHash, 0, 0f);
            animator.Update(0f);
        }
        else
        {
            Debug.LogWarning($"ProtectorVisuals: El estado '{deathStateName}' no existe en el Animator.", this);
        }

        Destroy(gameObject, Mathf.Max(0.05f, destroyDelaySeconds));
    }

    private void PlayDeathClip(AnimationClip clip)
    {
        if (clip == null) return;

        StopDeathPlayable();

        animator.enabled = true;
        animator.Rebind();
        animator.Update(0f);

        deathPlayableGraph = PlayableGraph.Create($"{name}_DeathClipGraph");
        deathPlayableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(deathPlayableGraph, "DeathClipOutput", animator);
        AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(deathPlayableGraph, clip);
        output.SetSourcePlayable(clipPlayable);

        deathPlayableGraph.Play();
        isDeathPlayableActive = true;
    }

    private void StopDeathPlayable()
    {
        if (!isDeathPlayableActive) return;
        if (deathPlayableGraph.IsValid()) deathPlayableGraph.Destroy();
        isDeathPlayableActive = false;
    }

    private void OnDestroy()
    {
        StopDeathPlayable();
    }
}
