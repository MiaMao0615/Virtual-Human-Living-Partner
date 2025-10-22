using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vuforia;

[RequireComponent(typeof(ObserverBehaviour))]
public class PictureManager : MonoBehaviour
{
    [Serializable]
    public class SpotPairEntry
    {
        public string spotId;
        public GameObject startPrefab;
        [HideInInspector] public Transform startInst;
        public bool startUseCapturedLocalTRS = false;
        public Vector3 startCapturedLocalPos;
        public Vector3 startCapturedLocalEuler;
        public Vector3 startCapturedLocalScale = Vector3.one;

        public GameObject targetPrefab;
        [HideInInspector] public Transform targetInst;
        public bool targetUseCapturedLocalTRS = false;
        public Vector3 targetCapturedLocalPos;
        public Vector3 targetCapturedLocalEuler;
        public Vector3 targetCapturedLocalScale = Vector3.one;
    }

    public List<SpotPairEntry> pairs = new List<SpotPairEntry>();
    public string defaultSpotId;
    public TongTongManager tongTongManager;
    [SerializeField] private float lostClearDelaySeconds = 6f;
    [SerializeField] private bool treatLimitedAsTracked = false;
    [SerializeField] private bool treatExtendedAsTracked = false;

    private Coroutine _lossCo;
    private bool IsConsideredTracked(TargetStatus status)
    {
        switch (status.Status)
        {
            case Status.TRACKED:
                return true;
            case Status.EXTENDED_TRACKED:
                return treatExtendedAsTracked;
            case Status.LIMITED:
                return treatLimitedAsTracked;
            default:
                return false;
        }
    }

    private IEnumerator LossTimeoutRoutine()
    {
        float t = lostClearDelaySeconds > 0f ? lostClearDelaySeconds : 0f;
        if (t > 0f) yield return new WaitForSeconds(t);

        if (_isTracked) yield break;
        _currentSpotId = "none";

        ActiveManagers.Remove(this);
        FindObjectOfType<TongTongManager>()?.MatchId();
        DespawnAllPairs();

        _lossCo = null;
    }

    private string _currentSpotId = "none";
    public string GetPictureId() => _currentSpotId;

    public bool TryGetPairById(string id, out Transform start, out Transform target)
    {
        start = null; target = null;
        if (!_isTracked) return false;
        if (string.IsNullOrEmpty(id) || !string.Equals(id, _currentSpotId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (_runtimePairs.TryGetValue(_currentSpotId, out var rt))
        {
            start = rt.start;
            target = rt.target;
            return (start && target);
        }
        return false;
    }

    private readonly Dictionary<string, (Transform start, Transform target)> _runtimePairs =
        new Dictionary<string, (Transform start, Transform target)>(StringComparer.OrdinalIgnoreCase);

    private ObserverBehaviour _observer;
    private bool _isTracked;
    private readonly List<string> _orderedSpotIds = new List<string>();

    public static readonly HashSet<PictureManager> ActiveManagers = new HashSet<PictureManager>();

    public bool IsTracked => _isTracked;
    public string CurrentSpotId => _currentSpotId;
    public bool TryGetLivePair(out Transform start, out Transform target)
    {
        return TryGetPairById(_currentSpotId, out start, out target);
    }

    private void Awake()
    {
        _observer = GetComponent<ObserverBehaviour>();
        _observer.OnTargetStatusChanged += OnTargetStatusChanged;

        _orderedSpotIds.Clear();
        foreach (var e in pairs)
            if (e != null && !string.IsNullOrEmpty(e.spotId) && !_orderedSpotIds.Contains(e.spotId))
                _orderedSpotIds.Add(e.spotId);
    }

    private void OnDestroy()
    {
        if (_observer != null) _observer.OnTargetStatusChanged -= OnTargetStatusChanged;
    }

    private void Start()
    {
        OnTargetStatusChanged(_observer, _observer.TargetStatus);
    }

    private void OnTargetStatusChanged(ObserverBehaviour ob, TargetStatus status)
    {
        bool nowTracked = IsConsideredTracked(status);
        if (nowTracked == _isTracked) return;

        _isTracked = nowTracked;

        if (_isTracked)
        {
            if (_lossCo != null) { StopCoroutine(_lossCo); _lossCo = null; }
            SpawnAllPairs();
            ReapplyAllLocalTRS();
            ActivateDefaultSpotId();

            ActiveManagers.Add(this);

            BindThisToManager();
            FindObjectOfType<TongTongManager>()?.MatchId();
        }
        else
        {
            if (_lossCo != null) StopCoroutine(_lossCo);
            _lossCo = StartCoroutine(LossTimeoutRoutine());
        }
    }

    public void SetActiveSpotId(string spotId)
    {
        if (!_isTracked) return;
        if (string.IsNullOrEmpty(spotId)) return;
        if (!_runtimePairs.ContainsKey(spotId)) return;

        _currentSpotId = spotId;
        BindThisToManager();
        ReapplyAllLocalTRS();
        TriggerMatch();
    }

    public void CycleNextSpot()
    {
        if (!_isTracked || _orderedSpotIds.Count == 0) return;
        int idx = Mathf.Max(0, _orderedSpotIds.IndexOf(_currentSpotId));
        idx = (idx + 1) % _orderedSpotIds.Count;
        _currentSpotId = _orderedSpotIds[idx];
        BindThisToManager();
        TriggerMatch();
    }

    private void SpawnAllPairs()
    {
        _runtimePairs.Clear();

        foreach (var e in pairs)
        {
            if (e == null || string.IsNullOrEmpty(e.spotId)) continue;

            if (e.startInst == null)
            {
                string startName = GetNodeName(e.spotId, "start");
                e.startInst = FindExistingChild(transform, startName);
                if (e.startInst == null)
                    e.startInst = SpawnChild(e.startPrefab, transform, startName);

                if (e.startUseCapturedLocalTRS && e.startInst)
                    ApplyLocalTRS(e.startInst, e.startCapturedLocalPos, e.startCapturedLocalEuler, e.startCapturedLocalScale);
            }

            if (e.targetInst == null)
            {
                string targetName = GetNodeName(e.spotId, "target");
                e.targetInst = FindExistingChild(transform, targetName);
                if (e.targetInst == null)
                    e.targetInst = SpawnChild(e.targetPrefab, transform, targetName);

                if (e.targetUseCapturedLocalTRS && e.targetInst)
                    ApplyLocalTRS(e.targetInst, e.targetCapturedLocalPos, e.targetCapturedLocalEuler, e.targetCapturedLocalScale);
            }

            _runtimePairs[e.spotId] = (e.startInst, e.targetInst);
        }
    }

    private Transform FindExistingChild(Transform parent, string fullPathName)
    {
        var t = parent.Find(fullPathName);
        if (t != null) return t;

        int slash = fullPathName.LastIndexOf('/');
        string tail = slash >= 0 ? fullPathName.Substring(slash + 1) : fullPathName;
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == tail) return child;
        }
        return null;
    }

    private void DespawnAllPairs()
    {
        foreach (var e in pairs)
        {
            if (e == null) continue;
            if (e.startInst) Destroy(e.startInst.gameObject);
            if (e.targetInst) Destroy(e.targetInst.gameObject);
            e.startInst = e.targetInst = null;
        }
        _runtimePairs.Clear();
    }

    private Transform SpawnChild(GameObject prefab, Transform parent, string nameWhenEmpty)
    {
        if (prefab == null)
        {
            var go = new GameObject(nameWhenEmpty);
            return go.transform;
        }
        return Instantiate(prefab, parent, false).transform;
    }

    private static void ApplyLocalTRS(Transform t, Vector3 localPos, Vector3 localEuler, Vector3 localScale)
    {
        if (!t) return;
        t.localPosition = localPos;
        t.localRotation = Quaternion.Euler(localEuler);
        t.localScale = localScale;
    }

    public void ReapplyAllLocalTRS()
    {
        foreach (var e in pairs)
        {
            if (e == null) continue;

            if (e.startUseCapturedLocalTRS && e.startInst)
                ApplyLocalTRS(e.startInst.transform, e.startCapturedLocalPos, e.startCapturedLocalEuler, e.startCapturedLocalScale);

            if (e.targetUseCapturedLocalTRS && e.targetInst)
                ApplyLocalTRS(e.targetInst.transform, e.targetCapturedLocalPos, e.targetCapturedLocalEuler, e.targetCapturedLocalScale);
        }
    }

    private string GetNodeName(string spotId, string kind)
    {
        string imageName = !string.IsNullOrEmpty(gameObject.name) ? gameObject.name : "Image";
        return $"PM_{imageName}_{spotId}/{kind}";
    }

    private void ActivateDefaultSpotId()
    {
        if (!string.IsNullOrEmpty(defaultSpotId) && _runtimePairs.ContainsKey(defaultSpotId))
            _currentSpotId = defaultSpotId;
        else
            _currentSpotId = _orderedSpotIds.Count > 0 ? _orderedSpotIds[0] : "none";
    }

    private void BindThisToManager()
    {
        if (!tongTongManager) tongTongManager = FindObjectOfType<TongTongManager>();
        if (tongTongManager)
        {
            tongTongManager.SetPictureManager(this);
        }
    }

    private void TriggerMatch()
    {
        if (tongTongManager != null)
            tongTongManager.MatchId();
    }
}
