using UnityEngine;
using NaughtyAttributes;
using GridEmpire.Gameplay;
using GridEmpire.Shared;

public class UnitVisualTester : MonoBehaviour
{
    [Header("Setup Options")]
    [SerializeField] private bool enableAllRenderersOnStart = true;

    private UnitAnimator _unitAnimator;
    private Animator[] _animators;
    private AudioSource[] _audioSources;
    private ParticleSystem[] _particleSystems;

    private void Awake()
    {
        // 1. Megkeressük az ÖSSZES gyerek Animátort, AudioSource-t és Particle-t (a Mount-ot is beleértve)
        _unitAnimator = GetComponent<UnitAnimator>();
        _animators = GetComponentsInChildren<Animator>(true);
        _audioSources = GetComponentsInChildren<AudioSource>(true);
        _particleSystems = GetComponentsInChildren<ParticleSystem>(true);

        // 2. Automatikusan bekapcsoljuk az összes kikapcsolt SkinnedMeshRenderer-t és MeshRenderer-t
        if (enableAllRenderersOnStart)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.enabled = true;
            }
        }
    }

    [Button("Play Attack", EButtonEnableMode.Always)]
    public void TriggerAttack()
    {
        if (_unitAnimator != null && Application.isPlaying)
        {
            _unitAnimator.Play(ActionType.Attack);
        }
        else
        {
            PlayAnimationOnAll("Attack");
        }

        PlayParticleSystems();
    }

    [Button("Play Move", EButtonEnableMode.Always)]
    public void TriggerMove()
    {
        if (_unitAnimator != null && Application.isPlaying)
        {
            _unitAnimator.Play(ActionType.Move);
        }
        else
        {
            PlayAnimationOnAll("Move");
        }
    }

    [Button("Play Capture", EButtonEnableMode.Always)]
    public void TriggerCapture()
    {
        if (_unitAnimator != null && Application.isPlaying)
        {
            _unitAnimator.Play(ActionType.Capture);
        }
        else
        {
            PlayAnimationOnAll("Capture");
        }

        PlayParticleSystems();
    }

    [Button("Play Idle", EButtonEnableMode.Always)]
    public void TriggerIdle()
    {
        if (_unitAnimator != null && Application.isPlaying)
        {
            _unitAnimator.Play(ActionType.Idle);
        }
        else
        {
            PlayAnimationOnAll("Idle");
        }
    }

    [Button("Play Death", EButtonEnableMode.Always)]
    public void TriggerDeath()
    {
        if (_unitAnimator != null && Application.isPlaying)
        {
            _unitAnimator.PlayDeath();
        }
        else
        {
            PlayAnimationOnAll("Death");
        }

        PlayParticleSystems();
    }

    // --- HELPEREK ---

    private void PlayAnimationOnAll(string stateName)
    {
        if (_animators == null || _animators.Length == 0)
            _animators = GetComponentsInChildren<Animator>(true);

        foreach (var anim in _animators)
        {
            if (anim != null && anim.enabled)
            {
                anim.Play(stateName, 0, 0f);
            }
        }
    }

    private void PlayParticleSystems()
    {
        if (_particleSystems == null) return;
        foreach (var ps in _particleSystems)
        {
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
        }
    }
}