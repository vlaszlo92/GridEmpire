using UnityEngine;
using NaughtyAttributes;

public class UnitTestBed : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private ParticleSystem particleFx;

    [Header("Audio Variations")]
    [SerializeField] private AudioClip[] attackSounds;
    [SerializeField] private AudioClip[] hitSounds;

    // --- INSPECTOR GOMBOK ---

    [Button("Play Attack Sequence", EButtonEnableMode.Always)]
    public void TestAttack()
    {
        PlayAnimation("Attack");
        PlayRandomSound(attackSounds);
        PlayParticle();
    }

    [Button("Play Hit Sequence", EButtonEnableMode.Always)]
    public void TestHit()
    {
        PlayAnimation("Hit");
        PlayRandomSound(hitSounds);
        PlayParticle();
    }

    [Button("Play Idle", EButtonEnableMode.Always)]
    public void TestIdle() => PlayAnimation("Idle");

    [Button("Stop Particle", EButtonEnableMode.Always)]
    public void StopParticle()
    {
        if (particleFx != null) particleFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // --- HELPEREK ---

    private void PlayAnimation(string stateName)
    {
        if (animator != null) animator.Play(stateName, 0, 0f);
    }

    private void PlayRandomSound(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        audioSource.PlayOneShot(clip);
    }

    private void PlayParticle()
    {
        if (particleFx == null) return;
        particleFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleFx.Play(true);
    }
}