using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AudioCommunicationUI : MonoBehaviour
{
    [Header("语音识别组件")]
    private AudioRecorder audioRecorder;
    private AudioSend audioSend;

    [Header("UI 元素")]
    public Button audioOpenButton;
    public TextMeshProUGUI receiveText;

    [Header("设置")]
    public int maxRecordTime = 10;

    [Header("考试组件")]
    public PoemExamNPC examNPC;

    private PoemExamNPC currentExamNPC;

    private bool isRecording = false;
    private bool isRecognizing = false;
    private bool isCanvasActive = false;

    private void Awake()
    {
        audioRecorder = GetComponent<AudioRecorder>();
        audioSend = GetComponent<AudioSend>();
    }

    private void Start()
    {
        if (audioOpenButton != null)
        {
            audioOpenButton.onClick.AddListener(OnButtonClicked);
            UpdateButtonText();
        }
    }

    private void Update()
    {
        if (!isCanvasActive || MobileInputManager.Instance == null)
            return;

        if (MobileInputManager.Instance.GetAudioDown())
        {
            OnInteractPressed();
        }
    }

    public void SetCurrentExamNPC(PoemExamNPC npc)
    {
        currentExamNPC = npc;

        if (receiveText != null && npc != null)
            npc.ShowExamContent();
    }

    public void ClearCurrentExamNPC(PoemExamNPC npc)
    {
        if (currentExamNPC == npc)
            currentExamNPC = null;
    }

    private void OnInteractPressed()
    {
        if (isRecognizing)
            return;

        if (audioRecorder == null || audioSend == null || receiveText == null)
        {
            Debug.LogError("[AudioCommunicationUI] 缺少 AudioRecorder / AudioSend / receiveText 引用。");
            return;
        }

        if (!isRecording)
        {
            StartRecording();
        }
        else
        {
            StopRecordingAndRecognize();
        }
    }

    private void OnButtonClicked()
    {
        OnInteractPressed();
    }

    private void StartRecording()
    {
        audioRecorder.StartRecording(maxRecordTime);

        if (!audioRecorder.IsRecording)
        {
            receiveText.text = "无法开始录音，请检查麦克风权限";
            return;
        }

        isRecording = true;
        receiveText.text = "正在聆听...";
        UpdateButtonText();
    }

    private void StopRecordingAndRecognize()
    {
        byte[] audioData = audioRecorder.StopRecordingAndGetPCM();
        isRecording = false;
        receiveText.text = "识别中...";
        UpdateButtonText();

        if (audioData == null || audioData.Length == 0)
        {
            receiveText.text = "录音失败，请重试";
            return;
        }

        isRecognizing = true;
        StartCoroutine(RecognizeRoutine(audioData));
    }

    private IEnumerator RecognizeRoutine(byte[] audioData)
    {
        string recognized = null;
        yield return StartCoroutine(audioSend.RecognizeSpeech(audioData, result => recognized = result));

        isRecognizing = false;
        OnRecognitionComplete(recognized);
    }

    private void UpdateButtonText()
    {
        if (audioOpenButton == null)
            return;

        var buttonText = audioOpenButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (buttonText != null)
        {
            buttonText.text = isRecording ? "停止录音" : "开始录音";
        }
    }

    private void OnRecognitionComplete(string result)
    {
        if (receiveText == null)
            return;

        if (string.IsNullOrEmpty(result))
        {
            receiveText.text = "识别失败，请重试";
            return;
        }

        if (currentExamNPC != null)
        {
            // 交给考官 NPC 决定显示什么（它会覆盖 receiveText）
            currentExamNPC.OnVoiceRecognized(result);
        }
        else
        {
            receiveText.text = result;
        }
    }


    private void OnEnable()
    {
        isCanvasActive = true;
    }

    private void OnDisable()
    {
        isCanvasActive = false;

        if (isRecording && audioRecorder != null)
        {
            audioRecorder.StopRecordingAndGetPCM();
            isRecording = false;
        }

        UpdateButtonText();
    }

    private void OnDestroy()
    {
        if (audioOpenButton != null)
            audioOpenButton.onClick.RemoveAllListeners();
    }
}
