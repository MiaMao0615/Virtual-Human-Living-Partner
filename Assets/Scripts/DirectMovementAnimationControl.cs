// Assets/Scripts/TestPoint2Point.cs
using UnityEngine;
using System.Collections;

public class TestPoint2Point : MonoBehaviour
{
    [Header("引用")]
    public Animator animator;
    public RuntimeAnimatorController controller;
    public Transform startPoint;
    public Transform endPoint;

    [Header("Animator ")]
    public string moveStateName = "drawfwalk";
    public string arriveStateName = "run";
    public string idleStateName = "";

    [Header("移动参数")]
    public float moveSpeed = 2.0f;
    public float arriveThreshold = 0.05f;
    public float rotateSpeed = 10f;
    public bool smoothStop = true;

    [Header("到达策略")]
    public bool arriveOnBackward = false;
    public bool parentToEndOnForward = false;

    [Header("方向")]
    public bool trigger = true;

    [Header("到达事件")]
    public System.Action<bool> onArrived;
    public bool hideOnBackwardArrive = false;
    public GameObject objectToHide;


    private bool _lastTrigger;
    private Coroutine _moveCo;

    void Reset()
    {
        animator = GetComponent<Animator>();
    }

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!animator)
        {
            Debug.LogError("[TestPoint2Point] 未找到 Animator。");
            enabled = false;
            return;
        }
        if (controller) animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        VerifyStates();

        if (objectToHide == null) objectToHide = gameObject;
    }

    void Start()
    {
        _lastTrigger = trigger;
        TryStartMoveByTrigger(force: true);
    }

    void Update()
    {
        if (trigger != _lastTrigger)
        {
            _lastTrigger = trigger;
            TryStartMoveByTrigger(force: false);
        }
    }


    private void TryStartMoveByTrigger(bool force)
    {
        EnsureAnimatorReady();

        if (!animator || !startPoint || !endPoint)
        {
            Debug.LogWarning("[TestPoint2Point] 缺少 animator/startPoint/endPoint");
            return;
        }

        if (_moveCo != null) { StopCoroutine(_moveCo); _moveCo = null; }

        if (trigger)
        {
            _moveCo = StartCoroutine(MoveRoutine(
                from: startPoint.position,
                to: endPoint.position,
                isForward: true,
                playArriveAnim: true
            ));
        }
        else
        {
            _moveCo = StartCoroutine(MoveRoutine(
                from: endPoint.position,
                to: startPoint.position,
                isForward: false,
                playArriveAnim: arriveOnBackward
            ));
        }
    }

    public void Kickoff(Transform start, Transform end, bool forward)
    {
        startPoint = start;
        endPoint = end;
        trigger = forward;

        Vector3 from = forward ? startPoint.position : endPoint.position;
        Vector3 to = forward ? endPoint.position : startPoint.position;

        EnsureAnimatorReady();

        
        transform.position = from;
        Vector3 dir = (to - from);
        if (dir.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        if (_moveCo != null) { StopCoroutine(_moveCo); _moveCo = null; }
        bool playArriveAnim = forward || arriveOnBackward;
        _moveCo = StartCoroutine(MoveRoutine(from, to, forward, playArriveAnim));
    }

    
    private void EnsureAnimatorReady()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!animator)
        {
            Debug.LogError("[TestPoint2Point] 未找到 Animator。");
            enabled = false;
            return;
        }

        if (!animator.runtimeAnimatorController && controller)
            animator.runtimeAnimatorController = controller;

        animator.enabled = true;            
        animator.applyRootMotion = false;
        VerifyStates();
    }

    private void VerifyStates()
    {
        if (!animator || animator.runtimeAnimatorController == null) return;

        if (!string.IsNullOrEmpty(moveStateName) &&
            !animator.HasState(0, Animator.StringToHash(moveStateName)))
        {
            Debug.LogWarning($"[TestPoint2Point] Layer0 未找到 moveState：{moveStateName}");
        }

        if (!string.IsNullOrEmpty(arriveStateName) &&
            !animator.HasState(0, Animator.StringToHash(arriveStateName)))
        {
            Debug.LogWarning($"[TestPoint2Point] Layer0 未找到 arriveState：{arriveStateName}");
        }
    }

    private void PlayStateImmediately(string stateName)
    {
        if (!string.IsNullOrEmpty(stateName) && animator.HasState(0, Animator.StringToHash(stateName)))
            animator.Play(stateName, 0, 0f);
    }

    private void CrossFadeState(string stateName, float fade = 0.1f)
    {
        if (!string.IsNullOrEmpty(stateName) && animator.HasState(0, Animator.StringToHash(stateName)))
            animator.CrossFade(stateName, fade, 0, 0f);
    }

    private void PlayMoveAnimImmediately() => PlayStateImmediately(moveStateName);
    private void PlayArriveAnim() => CrossFadeState(arriveStateName, 0.1f);

    private IEnumerator MoveRoutine(Vector3 from, Vector3 to, bool isForward, bool playArriveAnim)
    {
        transform.position = from;
        Vector3 dir = (to - from);
        if (dir.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        EnsureAnimatorReady();
        PlayMoveAnimImmediately();

        while (true)
        {
            Vector3 pos = transform.position;
            Vector3 toDir = (to - pos);
            float dist = toDir.magnitude;

            if (dist <= arriveThreshold) break;

            if (toDir.sqrMagnitude > 1e-6f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toDir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotateSpeed);
            }

            float speed = moveSpeed;
            if (smoothStop)
            {
                float factor = Mathf.Clamp01(dist / 1.0f);
                speed *= Mathf.Lerp(0.5f, 1f, factor);
            }

            transform.position = Vector3.MoveTowards(pos, to, speed * Time.deltaTime);
            yield return null;
        }

        if (isForward)
        {
            if (playArriveAnim) PlayArriveAnim();

            if (parentToEndOnForward && endPoint)
            {
                transform.SetParent(endPoint, worldPositionStays: false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale = Vector3.one;
            }

            onArrived?.Invoke(true);
        }
        else
        {
            animator.enabled = true;

            if (!string.IsNullOrEmpty(idleStateName) &&
                animator.HasState(0, Animator.StringToHash(idleStateName)))
            {
                PlayStateImmediately(idleStateName);
            }
            else if (!string.IsNullOrEmpty(arriveStateName) &&
                     animator.HasState(0, Animator.StringToHash(arriveStateName)))
            {
                PlayStateImmediately(arriveStateName);
            }
            else
            {
                PlayStateImmediately(moveStateName);
            }

            if (hideOnBackwardArrive && objectToHide)
                objectToHide.SetActive(false);

            onArrived?.Invoke(false);
        }

        _moveCo = null;
    }
}
