using System;
using System.Collections;
using TMPro;
using UnityEngine;
using static Unity.Burst.Intrinsics.X86.Avx;

public class TongTongManager : MonoBehaviour
{
    [Header("基础引用")]
    public GameObject tongTongPrefab;
    public Transform fallbackPosition;
    public Vector3 tongTongScale = new Vector3(0.5f, 0.5f, 0.5f);

    [Header("外部管理器")]
    public PictureManager pictureManager;      
    public TimeStateController timeController;  

    [Header("提示")]
    public TMP_Text messageText;
    public AudioClip collisionClip;

    [Header("AnimationRig 控制器")]
    public TongTongAnimationRig rigController;

    [Header("Debug")]
    public bool debugLogs = true;

    
    private GameObject currentTongTong;
    private Animator currentAnimator;
    private MotionRunner motion;

    
    private bool   lastWasMatch        = false;
    private string lastMatch_PictureId = null;
    private string lastMatch_TimeId    = null;
    private Transform lastMatch_Start  = null;
    private Transform lastMatch_Target = null;

    
    private string lastPlayedStateName = "";

    void Start()
    {
        SpawnAtFallback();
        SetupCollisionForTong(currentTongTong);

        
        if (rigController != null && currentTongTong != null)
        {
            rigController.OnTongTongSpawned(currentTongTong.transform);
            rigController.StopRig();
        }
    }

    

    private void Log(string msg)
    {
        if (debugLogs) Debug.Log($"[TongTongManager] {msg}");
    }

    private void DumpIds(string pictureId, string timeId)
    {
        if (!debugLogs) return;
        Debug.Log($"[TongTongManager] pictureId={(string.IsNullOrEmpty(pictureId) ? "null/empty" : pictureId)}, timeId={(string.IsNullOrEmpty(timeId) ? "null/empty" : timeId)}");
    }

    public void SetPictureManager(PictureManager pm)
    {
        pictureManager = pm;
        Log($"Bound pictureManager = {(pm ? pm.name : "null")}");
    }

    
    private bool PickActivePictureForTimeId(string timeId, out PictureManager pickedPM, out Transform start, out Transform target)
    {
        pickedPM = null; start = target = null;

        // spotId == timeId
        foreach (var pm in PictureManager.ActiveManagers)
        {
            if (pm && pm.IsTracked && string.Equals(pm.CurrentSpotId, timeId, StringComparison.OrdinalIgnoreCase))
            {
                if (pm.TryGetLivePair(out start, out target))
                {
                    pickedPM = pm;
                    return true;
                }
            }
        }

        // timeId unmatch
        foreach (var pm in PictureManager.ActiveManagers)
        {
            if (pm && pm.IsTracked && pm.TryGetLivePair(out start, out target))
            {
                pickedPM = pm;
                return true;
            }
        }

        
        return false;
    }

    private void ClearLastMatchSnapshot()
    {
        lastMatch_PictureId = null;
        lastMatch_TimeId    = null;
        lastMatch_Start     = null;
        lastMatch_Target    = null;
    }

    private void EnsureTongSpawned()
    {
        if (currentTongTong == null) SpawnAtFallback();
    }


    public void MatchId()
    {
        if (timeController == null) { Log("timeController == null → 退出"); return; }

        string timeId = timeController.GetTimeId();

        PictureManager chosenPM;
        Transform liveStart, liveTarget;
        bool havePair = PickActivePictureForTimeId(timeId, out chosenPM, out liveStart, out liveTarget);

        bool nowMatch = havePair && chosenPM &&
                        string.Equals(chosenPM.CurrentSpotId, timeId, StringComparison.OrdinalIgnoreCase);

        string pictureId = (chosenPM ? chosenPM.CurrentSpotId : null);
        DumpIds(pictureId ?? "none", timeId);
        LogPair($"Resolved pair (nowMatch={nowMatch}, lastWasMatch={lastWasMatch})", liveStart, liveTarget);

        // 1) Matched → Matched
        if (lastWasMatch && nowMatch)
        {
            lastMatch_PictureId = pictureId;
            lastMatch_TimeId    = timeId;
            lastMatch_Start     = liveStart;
            lastMatch_Target    = liveTarget;
            lastWasMatch        = true;
            return;
        }

        // 2) Matched → Not matched
        if (lastWasMatch && !nowMatch)
        {
            EnsureTongSpawned();

            bool pictureChanged = (lastMatch_PictureId != null) &&
                                  !string.Equals(lastMatch_PictureId, pictureId, StringComparison.OrdinalIgnoreCase);

            
            if (pictureChanged || lastMatch_Start == null || lastMatch_Target == null)
            {
                Log("匹配→不匹配：图片改变 或 点位缺失 → 立即回 fallback");
                motion?.HardCancelAndReturnToFallback();
                StopRigIfAny("picture changed OR invalid pair");
                ClearLastMatchSnapshot();
                lastWasMatch = false;
                return;
            }

            
            Log("匹配→不匹配：时间改变（图片未变）→ Target→Start，再回 fallback");

            StopRigIfAny("start reverse");

            motion?.ApplyAndRunForId(lastMatch_TimeId, lastMatch_Start, lastMatch_Target, false, debugLogs);

            
            ClearLastMatchSnapshot();
            lastWasMatch = false;
            return;
        }

        // 3) Not matched → Matched
        if (!lastWasMatch && nowMatch)
        {
            EnsureTongSpawned();

            if (liveStart == null || liveTarget == null)
            {
                Log("不匹配→匹配：点位缺失 → 立即回 fallback");
                motion?.HardCancelAndReturnToFallback();
                ClearLastMatchSnapshot();
                lastWasMatch = false;
                return;
            }

            Log("不匹配→匹配：对齐 Start 并播放 Start→Target");
            motion?.AlignFromFallbackToStart(liveStart);
            motion?.ApplyAndRunForId(timeId, liveStart, liveTarget, /*forward=*/true, debugLogs);

            lastMatch_PictureId = pictureId;
            lastMatch_TimeId    = timeId;
            lastMatch_Start     = liveStart;
            lastMatch_Target    = liveTarget;
            lastWasMatch        = true;
            return;
        }

        // 4) Not matched → Not matched
        if (!lastWasMatch && !nowMatch)
        {
            EnsureTongSpawned();
            Log("不匹配→不匹配：确保在 fallback（幂等复位）");
            StopRigIfAny("not matched -> not matched");
            GetMotion()?.HardCancelAndReturnToFallback();
            ClearLastMatchSnapshot();
            lastWasMatch = false;
            return;
        }
    }

    // ====================== component ======================
    private MotionRunner GetMotion()
    {
        if (currentTongTong == null) return null;
        if (!motion) motion = currentTongTong.GetComponent<MotionRunner>()
                          ?? currentTongTong.AddComponent<MotionRunner>();
        return motion;
    }
    private void SpawnAtFallback()
    {
        if (currentTongTong != null) return;

        if (!tongTongPrefab || !fallbackPosition)
        {
            Debug.LogWarning("[TongTongManager] Missing tongTongPrefab or fallbackPosition");
            return;
        }

        // Instantiate at fallback position
        currentTongTong = Instantiate(tongTongPrefab, fallbackPosition);
        if (!currentTongTong.activeSelf) currentTongTong.SetActive(true);

        // Align to fallback and set scale
        currentTongTong.transform.localPosition = Vector3.zero;
        currentTongTong.transform.localRotation = Quaternion.identity;
        currentTongTong.transform.localScale = tongTongScale;

        // Get/add necessary components
        currentAnimator = currentTongTong.GetComponent<Animator>() ?? currentTongTong.AddComponent<Animator>();
        motion = currentTongTong.GetComponent<MotionRunner>() ?? currentTongTong.AddComponent<MotionRunner>();
        motion.debugLogs = debugLogs;
        motion.followCopyRotation = true;
        motion.followCopyScale = true;
        motion.SetFallback(fallbackPosition);

        if (rigController != null)
        {
            rigController.OnTongTongSpawned(currentTongTong.transform);
            rigController.StopRig();
        }

        // Optional: Collision message
        if (messageText || collisionClip)
        {
            var cm = currentTongTong.GetComponent<CollisionMessage>() ?? currentTongTong.AddComponent<CollisionMessage>();
            cm.messageText = messageText;
            cm.collisionClip = collisionClip;
        }
    }



    private void StopRigIfAny(string reason = "")
    {
        if (rigController != null)
        {
            rigController.StopRig();  
            if (debugLogs) Debug.Log($"[TongTongManager] StopRigIfAny({reason})");
        }
    }

    [Header("Collision 提示")]
    public TMP_Text CollisionText; 
    public AudioClip studyClip;         
    public string collisionTag = "Collision";
    public float collisionDisplayTime = 5f;

    private void SetupCollisionForTong(GameObject tong)
    {
        if (!tong) return;

        // Collider
        var col = tong.GetComponent<Collider>();
        if (!col) col = tong.AddComponent<CapsuleCollider>();
        col.isTrigger = true;

        if (col is CapsuleCollider cap)
        {
            
            cap.center = new Vector3(0f, 0.4f, 0f);
            cap.radius = 0.20f;
            cap.height = 1.20f;
            cap.direction = 1; 
        }

        // Rigidbody
        var rb = tong.GetComponent<Rigidbody>();
        if (!rb) rb = tong.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.drag = 100f;

        // AudioSource
        var audio = tong.GetComponent<AudioSource>() ?? tong.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        // CollisionMessage
        var cm = tong.GetComponent<CollisionMessage>() ?? tong.AddComponent<CollisionMessage>();
        cm.messageText = messageText;
        cm.messageOnHit = "TongTong hit the table, need to move back a bit";
        cm.displayTime = (collisionDisplayTime > 0f) ? collisionDisplayTime : 5f;
        cm.triggerTag = string.IsNullOrEmpty(collisionTag) ? "Collision" : collisionTag;
        cm.collisionClip = studyClip;
        cm.tongTongManager = this;
    }


   

    // TimeStateController 
    public string GetCurrentStateName()
    {
        if (!string.IsNullOrEmpty(lastPlayedStateName)) return lastPlayedStateName;
        if (currentAnimator == null) return "";
        var info = currentAnimator.GetCurrentAnimatorStateInfo(0);
        return info.IsName("") ? "" : "State";
    }

    private static string GetPath(Transform t)
    {
        if (!t) return "null";
        string path = t.name;
        Transform p = t.parent;
        int guard = 0;
        while (p && guard++ < 32) { path = p.name + "/" + path; p = p.parent; }
        return path;
    }

    private void LogPair(string tag, Transform s, Transform e)
    {
        if (!debugLogs) return;
        Debug.Log($"[TongTongManager] {tag}: start=({s?.name}) {s?.position} [{GetPath(s)}]  |  target=({e?.name}) {e?.position} [{GetPath(e)}]");
    }
}
