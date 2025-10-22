using System.Collections;
using TMPro;
using UnityEngine;

public class CollisionMessage : MonoBehaviour
{
    [Header("UI & �İ�")]
    public TMP_Text messageText;
    [TextArea] public string messageOnHit = "TongTong hit the table, need to move back a bit.";
    public float displayTime = 5f;

    [Header("�������ˣ���ײ���� Tag��")]
    public string triggerTag = "Collision";

    [Header("��ѡ��Ч")]
    public AudioClip collisionClip;

    [Header("��ѡ�ص�")]
    public TongTongManager tongTongManager;

    private AudioSource _audio;
    private Coroutine _co;

    [Header("�ش�������")]
    public float retriggerInterval = 0.5f;
    private float _lastTriggerTime = -999f;
    private bool _isInside = false;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        if (_audio) _audio.playOnAwake = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other) return;
        if (!string.IsNullOrEmpty(triggerTag) && !other.CompareTag(triggerTag)) return;

        if (messageText)
        {
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(ShowMessageRoutine(messageOnHit, displayTime));
        }

        if (_audio && collisionClip) _audio.PlayOneShot(collisionClip);

        _lastTriggerTime = Time.time;
        _isInside = true;
    }

    private IEnumerator ShowMessageRoutine(string text, float seconds)
    {
        messageText.text = text;
        if (seconds > 0f)
        {
            yield return new WaitForSeconds(seconds);
            messageText.text = "";
        }
        _co = null;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other) return;
        if (!string.IsNullOrEmpty(triggerTag) && !other.CompareTag(triggerTag)) return;
        _isInside = false;
    }

    void OnTriggerStay(Collider other)
    {
        if (!other) return;
        if (!string.IsNullOrEmpty(triggerTag) && !other.CompareTag(triggerTag)) return;
        if (!_isInside) return;

        if (Time.time - _lastTriggerTime >= retriggerInterval)
        {
            if (messageText)
            {
                if (_co != null) StopCoroutine(_co);
                _co = StartCoroutine(ShowMessageRoutine(messageOnHit, displayTime));
            }
            if (_audio && collisionClip) _audio.PlayOneShot(collisionClip);

            _lastTriggerTime = Time.time;
        }
    }
}
