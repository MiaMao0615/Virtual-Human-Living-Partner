using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class TongTongAnimationRig : MonoBehaviour
{
    [Header("目标")]
    public Transform initialTarget;
    public Transform arCameraTarget;

    [Header("各骨节约束权重 (MultiAimConstraint.weight)")]
    [Range(0, 1)] public float headAimWeight = 0.4f;
    [Range(0, 1)] public float spine02AimWeight = 0.2f;
    [Range(0, 1)] public float spine03AimWeight = 0.35f;

    [Header("整体 Rig 渐变参数")]
    public float activateTweenDuration = 1.0f;

    [Header("调试")]
    public bool debugLogs = true;

    private Transform _tongTongRoot;
    private Rig _rig;
    private Transform _rigAimRoot;
    private bool _isTweening = false;
    private float _tweenFrom, _tweenTo, _tweenDur;

    private Coroutine _followCo;
    private Vector3 _savedPos;
    private Quaternion _savedRot;
    private bool _hasSavedPose = false;



    public void OnTongTongSpawned(Transform tongTongRoot)
    {
        _tongTongRoot = tongTongRoot;
        if (!BindRigAndConstraintsImmediate(_tongTongRoot, initialTarget))
        {
            return;
        }
        // 初始为关闭
        BindRigAndConstraintsImmediate(_tongTongRoot, initialTarget);
        print("绑定成功");
        SetRigImmediate(0f);
    }


    public void StopRig()
    {

        
        if (_followCo != null) { StopCoroutine(_followCo); _followCo = null; }
        if (_hasSavedPose && initialTarget)
        {
            initialTarget.position = _savedPos;
            initialTarget.rotation = _savedRot;
        }

  
        StopTween();
        SetRigImmediate(0f);
        
    }
    private bool BindRigAndConstraintsImmediate(Transform characterRoot, Transform aimTarget)
    {
        var animator = characterRoot.GetComponentInChildren<Animator>();
        Transform FindFirst(params Transform[] cands) { foreach (var c in cands) if (c) return c; return null; }
        Transform Fuzzy(string key) => FindChildByNameContains(characterRoot, key);

        
        Transform head = FindFirst(
            (animator && animator.isHuman) ? animator.GetBoneTransform(HumanBodyBones.Head) : null,
            characterRoot.Find("root/pelvis/spine_01/spine_02/spine_03/neck_01/head"),
            Fuzzy("head")
        );
        if (!head) { Debug.LogError("未找到 head 骨骼"); return false; }

        var host = animator ? animator.transform : characterRoot;
        var rb = host.GetComponent<RigBuilder>() ?? host.gameObject.AddComponent<RigBuilder>();
        if (rb.layers == null) rb.layers = new List<RigLayer>();

        
        var rigTf = host.Find("rig_aim");
        if (!rigTf)
        {
            var rigGO = new GameObject("rig_aim");
            rigGO.transform.SetParent(host, false);
            rigTf = rigGO.transform;
        }
        var rig = rigTf.GetComponent<Rig>() ?? rigTf.gameObject.AddComponent<Rig>();
        rig.weight = 1f; 

        MultiAimConstraint EnsureAimNode(string nodeName, Transform constrainedObj, float aimWeightEach)
        {
            var node = rigTf.Find(nodeName);
            if (!node)
            {
                var go = new GameObject(nodeName);
                node = go.transform;
                node.SetParent(rigTf, false);
            }
            var aim = node.GetComponent<MultiAimConstraint>() ?? node.gameObject.AddComponent<MultiAimConstraint>();

            var d = aim.data;
            d.constrainedObject = constrainedObj;
            d.worldUpType = MultiAimConstraintData.WorldUpType.None;
            d.aimAxis = MultiAimConstraintData.Axis.Y_NEG;
            d.upAxis = MultiAimConstraintData.Axis.X_NEG;
            d.constrainedXAxis = d.constrainedYAxis = d.constrainedZAxis = true;
            d.maintainOffset = true;
            d.limits = new Vector2(-100f, 100f);

            var src = new WeightedTransformArray();
            src.Add(new WeightedTransform(aimTarget, 1f)); 
            d.sourceObjects = src;

            aim.data = d;
            aim.weight = Mathf.Clamp01(aimWeightEach);     
            return aim;
        }

        
        EnsureAimNode("headaim", head, headAimWeight);

        Transform spine2 = FindFirst(
            characterRoot.Find("root/pelvis/spine_01/spine_02"),
            (animator && animator.isHuman) ? animator.GetBoneTransform(HumanBodyBones.Spine) : null,
            Fuzzy("spine_02")
        );
        if (spine2) EnsureAimNode("spine02aim", spine2, spine02AimWeight);
        else Debug.LogWarning("未找到 spine_02，跳过 Spine_02 约束");

        Transform spine3 = FindFirst(
            characterRoot.Find("root/pelvis/spine_01/spine_02/spine_03"),
            (animator && animator.isHuman) ? (animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine)) : null,
            Fuzzy("spine_03")
        );
        if (spine3) EnsureAimNode("spineaim", spine3, spine03AimWeight);
        else Debug.LogWarning("未找到 spine_03，跳过 Spine 约束");

        
        if (!rb.layers.Exists(l => l.rig == rig)) rb.layers.Add(new RigLayer(rig));
        rb.Build();

        
        _rig = rig;
        _rigAimRoot = rigTf;
        return true;
    }
    private void StopTween() => _isTweening = false;

    private void SetRigImmediate(float w)
    {
        if (_rig) _rig.weight = Mathf.Clamp01(w);
    }

    
    private Transform FindChildByNameContains(Transform root, string key)
    {
        string lowerKey = key.ToLower();
        return FindChildRecursive(root, lowerKey);
    }
    private Transform FindChildRecursive(Transform current, string lowerKey)
    {
        if (current.name.ToLower().Contains(lowerKey)) return current;
        foreach (Transform child in current)
        {
            var r = FindChildRecursive(child, lowerKey);
            if (r != null) return r;
        }
        return null;
    }

    
    
}
