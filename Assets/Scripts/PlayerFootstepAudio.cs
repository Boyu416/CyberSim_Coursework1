using StarterAssets;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(CharacterController))]
public class PlayerFootstepAudio : MonoBehaviour
{
    public AudioClip footstepClip;
    public float volume = 0.35f;
    public float minMoveSpeed = 0.15f;
    public float loopFirstSeconds = 1f;
    public float walkPitch = 1f;
    public float sprintPitch = 1.12f;

    private AudioSource audioSource;
    private CharacterController characterController;
    private FirstPersonController firstPersonController;
    private StarterAssetsInputs inputs;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        firstPersonController = GetComponent<FirstPersonController>();
        inputs = GetComponent<StarterAssetsInputs>();
        EnsureAudioSource();
        AutoAssignFootstepClip();
    }

    void Update()
    {
        if (audioSource == null || footstepClip == null || Time.timeScale <= 0.0001f)
        {
            StopFootsteps();
            return;
        }

        audioSource.volume = volume;
        bool grounded = characterController.isGrounded || (firstPersonController != null && firstPersonController.Grounded);
        Vector3 horizontalVelocity = characterController.velocity;
        horizontalVelocity.y = 0f;

        bool hasInput = inputs == null || inputs.move.sqrMagnitude > 0.01f;
        bool isMoving = grounded && hasInput && horizontalVelocity.magnitude > minMoveSpeed;

        if (isMoving)
        {
            audioSource.pitch = inputs != null && inputs.sprint ? sprintPitch : walkPitch;

            if (!audioSource.isPlaying)
            {
                audioSource.time = 0f;
                audioSource.Play();
            }
            else if (loopFirstSeconds > 0f && audioSource.time >= loopFirstSeconds)
            {
                audioSource.time = 0f;
            }
        }
        else
        {
            StopFootsteps();
        }
    }

    void EnsureAudioSource()
    {
        Transform audioTransform = transform.Find("FootstepAudioSource");

        if (audioTransform == null)
        {
            GameObject audioObject = new GameObject("FootstepAudioSource");
            audioObject.transform.SetParent(transform, false);
            audioTransform = audioObject.transform;
        }

        audioSource = audioTransform.GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = audioTransform.gameObject.AddComponent<AudioSource>();
        }

        audioSource.clip = footstepClip;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = volume;
        audioSource.spatialBlend = 0f;
    }

    void AutoAssignFootstepClip()
    {
#if UNITY_EDITOR
        if (footstepClip == null)
        {
            footstepClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/freesound_community-footsteps-boots-101657.mp3");
        }
#endif

        if (audioSource != null)
        {
            audioSource.clip = footstepClip;
        }
    }

    void StopFootsteps()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}
