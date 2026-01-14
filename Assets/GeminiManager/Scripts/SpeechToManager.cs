using UnityEngine;
using UnityEngine.Windows.Speech; // Windows標準の音声認識名前空間
using System.Collections;

public class SpeechToTextManager : MonoBehaviour
{
    // 音声認識の結果を通知するイベント
    public System.Action<string> OnSpeechRecognized;
    public System.Action<string> OnSpeechHypothesis; // 認識中の推測テキスト
    public System.Action<string> OnStatusChanged;

    private DictationRecognizer dictationRecognizer;
    private bool isRecording = false;

    void Start()
    {
        // Windows環境以外では動作しないため、チェックを入れる
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        dictationRecognizer = new DictationRecognizer();

        // 確定した結果が返ってきた時
        dictationRecognizer.DictationResult += (text, confidence) =>
        {
            Debug.Log($"音声認識結果: {text}");
            OnSpeechRecognized?.Invoke(text);
        };

        // 話している最中の推測結果（リアルタイム表示用）
        dictationRecognizer.DictationHypothesis += (text) =>
        {
            OnSpeechHypothesis?.Invoke(text);
        };

        // エラーや完了時の処理
        dictationRecognizer.DictationComplete += (completionCause) =>
        {
            if (completionCause != DictationCompletionCause.Complete)
            {
                Debug.LogWarning($"音声認識終了: {completionCause}");
                OnStatusChanged?.Invoke($"停止: {completionCause}");
                isRecording = false;
            }
        };

        dictationRecognizer.DictationError += (error, hresult) =>
        {
            Debug.LogError($"音声認識エラー: {error}");
            OnStatusChanged?.Invoke("エラー発生");
            isRecording = false;
        };
#else
        Debug.LogError("この音声認識機能はWindows専用です。");
#endif
    }

    public void ToggleRecording()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (isRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
#endif
    }

    private void StartRecording()
    {
        if (dictationRecognizer.Status == SpeechSystemStatus.Running) return;

        dictationRecognizer.Start();
        isRecording = true;
        OnStatusChanged?.Invoke("聞いています...");
        Debug.Log("音声認識開始");
    }

    private void StopRecording()
    {
        if (dictationRecognizer.Status == SpeechSystemStatus.Running)
        {
            dictationRecognizer.Stop();
            isRecording = false;
            OnStatusChanged?.Invoke("待機中");
            Debug.Log("音声認識停止");
        }
    }

    void OnDestroy()
    {
        if (dictationRecognizer != null)
        {
            dictationRecognizer.Dispose();
        }
    }
}
