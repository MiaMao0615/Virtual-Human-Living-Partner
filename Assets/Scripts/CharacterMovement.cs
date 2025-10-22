using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class MotionRunner : MonoBehaviour
{
    public float arriveThresholdFallback = 0.06f;
    public bool followCopyRotation = true;
    public bool followCopyScale = true;
    public bool debugLogs = false;

    private TestPoint2Point _pointMover;
    private FollowTarget _follower;
    private Transform _fallback;
    private Coroutine _pendingCo;
    private Animator _anim;

    private int _opToken = 0;
    private int BumpToken() { _opToken++; return _opToken; }

    static Vector3 GetWorldScale(Transform t) => t ? t.lossyScale : Vector3.one;
    static void SetWorldScale(Transform t, Vector3 world)
    {
        if (!t) return;
        var p = t.parent;
        if (!p) { t.localScale = world; return; }
        var ps = p.lossyScale;
        t.localScale = new Vector3(
            ps.x != 0 ? world.x / ps.x : 0f,
            ps.y != 0 ? world.y / ps.y : 0f,
            ps.z != 0 ? world.z / ps.z : 0f
        );
    }

    public void SetFollowOptions(bool copyRot, bool copyScale)
    {
        followCopyRotation = copyRot;
        followCopyScale = copyScale;
        if (_follower)
        {
            _follower.copyRotation = copyRot;
            _follower.copyScale = copyScale;
        }
    }

    void Awake()
    {
        _anim = GetComponent<Animator>();
        _pointMover = GetComponent<TestPoint2Point>() ?? gameObject.AddComponent<TestPoint2Point>();
        _pointMover.enabled = false;
        _follower = GetComponent<FollowTarget>() ?? gameObject.AddComponent<FollowTarget>();
        _follower.copyRotation = followCopyRotation;
        _follower.copyScale = followCopyScale;
        _follower.EnableFollow(false);
    }

    public void SetFallback(Transform fallback) => _fallback = fallback;

    public void AlignFromFallbackToStart(Transform start)
    {
        if (!start) return;
        StopAllInternal();
        transform.position = start.position;
        transform.rotation = start.rotation;
        SetWorldScale(transform, GetWorldScale(start));
    }

    public void MoveStartToTarget(Transform start, Transform target, bool targetFollow = true)
    {
        if (!start || !target) return;
        StopAllInternal();
        int token = BumpToken();
        _pendingCo = StartCoroutine(MoveRoutine(start, target, forward: true, afterArrive: () =>
        {
            if (token != _opToken) return;
            if (targetFollow) StartFollowing(target);
        }, token));
    }

    public void StartFollowing(Transform target)
    {
        if (!_follower) _follower = GetComponent<FollowTarget>() ?? gameObject.AddComponent<FollowTarget>();
        _follower.copyRotation = followCopyRotation;
        _follower.copyScale = followCopyScale;
        _follower.SetTarget(target, true);
        _follower.EnableFollow(true);
    }

    public void StopFollowing()
    {
        if (_follower) _follower.EnableFollow(false);
    }

    public void ReverseToStartThenFallback(Transform start, Transform target)
    {
        if (!start) return;
        StopAllInternal();
        int token = BumpToken();
        _pendingCo = StartCoroutine(ReverseThenFallbackRoutine(start, target, token));
    }

    public void HardCancelAndReturnToFallback()
    {
        StopAllInternal();
        BumpToken();
        if (_fallback)
        {
            if (transform.parent == _fallback)
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            else
            {
                transform.position = _fallback.position;
                transform.rotation = _fallback.rotation;
            }
            SetWorldScale(transform, GetWorldScale(_fallback));
        }

        if (_pointMover) _pointMover.enabled = false;
    }

    public void ApplyAndRunForId(string timeId, Transform start, Transform end, bool forward, bool debugLogsFromManager)
    {
        var table = TimeIdTable.Instance;
        if (table) table.ApplyAndRunForId(timeId, gameObject, start, end, forward, debugLogsFromManager);
        _pointMover = GetComponent<TestPoint2Point>();

        if (forward) MoveStartToTarget(start, end, true);
        else ReverseToStartThenFallback(start, end);
    }

    private void StopAllInternal()
    {
        StopFollowing();
        if (_pointMover) _pointMover.StopAllCoroutines();
        if (_pendingCo != null) { StopCoroutine(_pendingCo); _pendingCo = null; }

        if (_pointMover)
        {
            _pointMover.StopAllCoroutines();
            _pointMover.onArrived = null;
            _pointMover.enabled = false;
        }
    }

    private IEnumerator MoveRoutine(Transform start, Transform end, bool forward, Action afterArrive, int token)
    {
        StopFollowing();

        if (!_pointMover) _pointMover = GetComponent<TestPoint2Point>() ?? gameObject.AddComponent<TestPoint2Point>();

        var begin = forward ? start : end;
        if (begin)
        {
            transform.position = begin.position;
            transform.rotation = begin.rotation;
            SetWorldScale(transform, GetWorldScale(begin));
        }

        if (_pointMover)
        {
            _pointMover.enabled = true;
            _pointMover.parentToEndOnForward = false;
            _pointMover.hideOnBackwardArrive = false;
            bool arrived = false;

            _pointMover.onArrived = (isForward) =>
            {
                if (token != _opToken) return;
                arrived = true;
                if (!isForward)
                {
                    _pointMover.enabled = false;
                    HardCancelAndReturnToFallback();
                }
            };

            _pointMover.startPoint = start;
            _pointMover.endPoint = end;
            _pointMover.Kickoff(start, end, forward);

            float t0 = Time.time, timeout = 30f;
            while (!arrived && token == _opToken && (Time.time - t0) < timeout)
                yield return null;

            _pointMover.onArrived = null;

            if (arrived && token == _opToken && forward)
                afterArrive?.Invoke();
        }
        else
        {
            var dest = forward ? end : start;
            if (dest)
            {
                transform.position = dest.position;
                transform.rotation = dest.rotation;
                SetWorldScale(transform, GetWorldScale(dest));
            }
            yield return null;

            if (forward) afterArrive?.Invoke(); else HardCancelAndReturnToFallback();
        }

        _pendingCo = null;
    }

    void OnDestroy()
    {
    }

    private IEnumerator ReverseThenFallbackRoutine(Transform start, Transform target, int token)
    {
        yield return MoveRoutine(start, target, forward: false, afterArrive: null, token: token);
    }
}
