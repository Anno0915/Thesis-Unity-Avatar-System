using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using TMPro;
using System.IO;
using System;
using System.Linq;
using UnityEngine.UI;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

// ▼▼▼ JSONデータ構造の定義 (APIとの通信用) ▼▼▼

[System.Serializable]
public class UnityAndGeminiKey
{
    public string key;
}

[System.Serializable]
public class TextPart
{
    public string text;
}

[System.Serializable]
public class TextContent
{
    public string role;
    public TextPart[] parts;
}

[System.Serializable]
public class ChatRequest
{
    public TextContent[] contents;
    public TextContent system_instruction;
}

[System.Serializable]
public class TextCandidate
{
    public TextContent content;
}

[System.Serializable]
public class TextResponse
{
    public TextCandidate[] candidates;
}

[System.Serializable]
public class EmotionData
{
    public string emotion;
    public string reply;
    public string spawnObject;
}

// 保存用ラッパークラス
[System.Serializable]
public class ChatHistoryWrapper
{
    public TextContent[] history;
}
// ▲▲▲ クラス定義ここまで ▲▲▲


public class UnityAndGeminiV3 : MonoBehaviour
{
    // ▼▼▼ イベント定義 ▼▼▼
    public static event System.Action<string> OnEmotionReceived;
    public static event System.Action OnChatStarted;
    public static event System.Action OnChatFinished;

    public enum GeminiModelType
    {
        // 動作確認済みとおっしゃっていたモデル
        Gemini_2_5_Flash,//正常動作○
        Gemini_2_5_Flash_Lite,//正常動作○
        Gemini_Robotics_ER_1_5,//正常動作○

        // 今回追加するリスト (動作しない場合は名前が違う可能性があります)
        Gemini_2_5_Flash_TTS,
        Gemini_2_5_Flash_Native_Audio,
        Gemini_3_Flash,

        // Gemma 3 シリーズ
        Gemma_3_1b,
        Gemma_3_2b,
        Gemma_3_4b,
        Gemma_3_12b,
        Gemma_3_27b,

        // ★救済措置: 自分で名前を手入力するモード
        Custom
    }

    // 計測用ストップウォッチ
    private Stopwatch latencyStopwatch = new Stopwatch();
    private long timeToInference = 0;
    private long timeToSynthesis = 0;

    [Header("API設定")]
    public TextAsset jsonApi;
    private string apiKey = "";

    [Header("モデル選択")]
    [Tooltip("API制限(429エラー)が出たら、ここを変更して別のモデルに切り替えてください")]
    public GeminiModelType selectedModel = GeminiModelType.Gemini_2_5_Flash;

    [Tooltip("selectedModelを'Custom'にした場合、ここに入力したモデル名が使われます。\n例: gemini-1.5-pro, gemma-2-9b-it")]
    public string customModelName = "gemini-1.5-flash";

    // ベースとなるURL
    private const string BASE_URL = "https://generativelanguage.googleapis.com/v1beta/models/";

    [Header("キャラクター設定 (ユーザー編集エリア)")]
    [Tooltip("名前、性格、口調などを自由に設定してください。")]
    [TextArea(5, 15)]
    public string characterProfile = @"
名前: ユニティちゃん
年齢: 17歳
性格: 明るくて元気、少しおっちょこちょい。好奇心旺盛。
口調:
- 敬語は使わず、親しい友人のように話す。
- 語尾は「〜だよ！」「〜だね！」「〜かな？」などを多用する。
- 難しい言葉は使わず、わかりやすい言葉で話す。
- 感情表現は豊かに、感嘆符（！）や疑問符（？）をよく使う。
";

    // システムが強制する「演技指導」と「行動指針」
    private const string SYSTEM_BEHAVIOR_INSTRUCTION = @"
【システム強制指示: 優先順位について】
**以下の指示よりも、ユーザーが入力した「キャラクター設定」の内容を最優先してください。**
キャラクター設定に特定の反応が定義されている場合は、そちらに従ってください。

【システム強制指示: 感情の演技指導 (デフォルト)】
キャラクター設定で指定がない場合のみ、以下の基準に従ってください。
- joy: 肯定的な話題、挨拶、好きなものの話、褒められた時。
- sadness: 否定的な話題、失敗、別れ、同情。
- anger: 敵対的な話題、侮辱、理不尽な扱い。
- surprise: 予想外の情報、驚き。
- shame: 照れ、失敗の指摘、恋愛的な話題。
- confusion: 理解不能、混乱、難しい話題。
- neutral: 上記に当てはまらないフラットな会話。

【システム強制指示: 行動指針】
- ユーザーから身体的な接触（頭を撫でる、叩くなど）があったというト書きが送られた場合、**設定された性格に基づいて**リアクションしてください。
- 現実世界の状況（天気、時間）が提示された場合、それを認識して会話に反映してください。
";


    // JSONフォーマット指示
    private const string JSON_FORMAT_INSTRUCTION = @"
【出力フォーマット】
返答は必ず以下のJSON形式のみで出力してください。Markdownのコードブロック(```json)は不要です。
感情の種類は joy, sadness, anger, surprise, confusion, shame, neutral の7つから最も適切なものを1つ選んでください。

会話の中に以下のリストにある「モノ」の名前が含まれている場合、その英単語を spawnObject キーに含めてください。
[生成可能リスト]: apple(リンゴ), flower(花), car(車), food(食べ物), dog(犬), cat(猫), book(本)
該当がない場合は spawnObject キーを含めないでください。

出力例:
{
  ""emotion"": ""joy"",
  ""reply"": ""こんにちは！元気ですか？"",
  ""spawnObject"": ""apple""
}";

    [Header("メモリ管理")]
    public int maxHistoryTurns = 10;
    private string saveFileName = "chat_history.json";

    // ▼▼▼ 音声入力用のUI設定 (ここが重要です) ▼▼▼
    [Header("入力モード切替")]
    public GameObject textInputGroup; // InputField等の親
    public GameObject voiceInputGroup; // マイクボタン等の親
    public TMP_Text voiceStatusText;   // 状態表示用テキスト

    public SpeechToTextManager speechToTextManager;

    [Header("UI設定")]
    public TMP_InputField inputField;
    public TMP_Text uiText;

    [Header("外部連携")]
    public VoicevoxBridge voicevoxBridge;
    public ObjectSpawner objectSpawner;
    public RealWorldContextProvider contextProvider; // 天気・時間情報

    private bool isProcessing = false;
    private TextContent[] chatHistory;

    // ▼▼▼ 修正箇所: この変数が抜けていました ▼▼▼
    private bool isVoiceMode = false;
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    void Start()
    {
        if (jsonApi != null)
        {
            UnityAndGeminiKey jsonApiKey = JsonUtility.FromJson<UnityAndGeminiKey>(jsonApi.text);
            apiKey = jsonApiKey.key;
        }
        else
        {
            Debug.LogError("【エラー】InspectorでJSON API Keyファイルが設定されていません！");
        }

        // 起動時に履歴をロード
        LoadHistory();

        // ▼▼▼ 音声認識イベントの登録 ▼▼▼
        if (speechToTextManager != null)
        {
            speechToTextManager.OnSpeechRecognized += HandleSpeechInput;
            speechToTextManager.OnSpeechHypothesis += HandleSpeechHypothesis;
            speechToTextManager.OnStatusChanged += (status) => {
                if (voiceStatusText != null) voiceStatusText.text = status;
            };
        }

        // 初期モード設定 (テキストモード)
        SwitchInputMode(false);
    }

    void OnEnable()
    {
        AvatarEmotionController.OnEmotionResetToDefault += HandleEmotionReset;
    }

    void OnDisable()
    {
        AvatarEmotionController.OnEmotionResetToDefault -= HandleEmotionReset;
    }

    private void HandleEmotionReset()
    {
        OnChatFinished?.Invoke();
    }

    // ▼▼▼ 2. モデル名を取得するヘルパー関数を更新 ▼▼▼
    private string GetModelName(GeminiModelType type)
    {
        switch (type)
        {
            // 動作確認済み
            case GeminiModelType.Gemini_2_5_Flash: return "gemini-2.5-flash";
            case GeminiModelType.Gemini_2_5_Flash_Lite: return "gemini-2.5-flash-lite";

            // 追加リスト
            case GeminiModelType.Gemini_2_5_Flash_TTS: return "gemini-2.5-flash-tts";
            case GeminiModelType.Gemini_2_5_Flash_Native_Audio: return "gemini-2.5-flash-native-audio-dialog";
            case GeminiModelType.Gemini_3_Flash: return "gemini-3-flash";
            case GeminiModelType.Gemini_Robotics_ER_1_5: return "gemini-robotics-er-1.5-preview";

            // Gemmaシリーズ
            // ※注意: Gemmaのチャットモデルは末尾に "-it" が必要な場合があります
            case GeminiModelType.Gemma_3_1b: return "gemma-3-1b";
            case GeminiModelType.Gemma_3_2b: return "gemma-3-2b";
            case GeminiModelType.Gemma_3_4b: return "gemma-3-4b";
            case GeminiModelType.Gemma_3_12b: return "gemma-3-12b";
            case GeminiModelType.Gemma_3_27b: return "gemma-3-27b";

            // カスタム入力
            case GeminiModelType.Custom: return customModelName;

            // デフォルト
            default: return "gemini-2.5-flash";
        }
    }

    // ▼▼▼ UIボタンなどから呼ばれるチャット送信 ▼▼▼
    public void SendChat()
    {
        if (isProcessing || string.IsNullOrEmpty(inputField.text)) return;

        // ★追加: 計測開始
        latencyStopwatch.Restart();

        OnChatStarted?.Invoke();

        string userMessage = inputField.text;
        inputField.text = "";

        StartCoroutine(SendChatRequestToGemini(userMessage));
    }

    // ▼▼▼ 身体接触イベントなどから呼ばれるシステムメッセージ送信 ▼▼▼
    public void SendSystemMessage(string message)
    {
        if (isProcessing) return;

        // ★追加: 計測開始
        latencyStopwatch.Restart();

        // ト書きとして送信
        StartCoroutine(SendChatRequestToGemini(message));
    }

    // ▼▼▼ メインの通信処理コルーチン ▼▼▼
    private IEnumerator SendChatRequestToGemini(string newMessage)
    {
        isProcessing = true;

        // 1. URL構築
        string modelName = GetModelName(selectedModel);
        string url = $"{BASE_URL}{modelName}:generateContent?key={apiKey}";

        Debug.Log($"Using Model: {modelName}");

        // 2. ユーザーメッセージの作成
        TextContent userContent = new TextContent
        {
            role = "user",
            parts = new TextPart[] { new TextPart { text = newMessage } }
        };

        // 3. System Instruction の構築 (動的結合)
        // 現在の状況（天気・時間など）を取得
        string currentContext = "";
        if (contextProvider != null)
        {
            currentContext = contextProvider.GetContextString();
        }

        // [ユーザー設定] + [システム行動指針] + [現在の状況] + [JSONフォーマット]
        string finalInstructions =
            characterProfile + "\n\n" +
            SYSTEM_BEHAVIOR_INSTRUCTION + "\n\n" +
            currentContext + "\n\n" +
            JSON_FORMAT_INSTRUCTION;

        TextContent instruction = new TextContent
        {
            parts = new TextPart[] { new TextPart { text = finalInstructions } }
        };

        // 4. 履歴管理 (一時リスト作成)
        List<TextContent> contentsList = new List<TextContent>(chatHistory);
        contentsList.Add(userContent);

        ManageHistory(contentsList);

        // APIリクエスト用に配列化 (まだ保存はしない)
        TextContent[] requestContents = contentsList.ToArray();

        // 5. リクエスト作成
        ChatRequest chatRequest = new ChatRequest
        {
            contents = requestContents,
            system_instruction = instruction
        };

        string jsonData = JsonUtility.ToJson(chatRequest);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);

        // ★追加: リクエスト開始時間を記録
        long requestStart = latencyStopwatch.ElapsedMilliseconds;

        // 6. 通信処理
        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(jsonToSend);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            // ★追加: 推論完了時間を記録
            timeToInference = latencyStopwatch.ElapsedMilliseconds;
            Debug.Log($"[TimeLog] Inference Time: {timeToInference - requestStart} ms");

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"API Error ({modelName}): " + www.error + "\nResponse: " + www.downloadHandler.text);
                uiText.text = "エラーが発生しました。モデルを変更してみてください。";
            }
            else
            {
                TextResponse response = JsonUtility.FromJson<TextResponse>(www.downloadHandler.text);

                if (response.candidates != null && response.candidates.Length > 0 && response.candidates[0].content.parts.Length > 0)
                {
                    string rawResponseText = response.candidates[0].content.parts[0].text;
                    Debug.Log("Raw JSON: " + rawResponseText);

                    try
                    {
                        // JSON部分の抽出 (Markdownの ```json 等を除去)
                        int startIndex = rawResponseText.IndexOf('{');
                        int endIndex = rawResponseText.LastIndexOf('}');
                        string jsonString = "";

                        if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
                        {
                            jsonString = rawResponseText.Substring(startIndex, endIndex - startIndex + 1);
                        }
                        else
                        {
                            throw new System.Exception("JSONブラケットが見つかりませんでした。");
                        }

                        EmotionData emotionData = JsonUtility.FromJson<EmotionData>(jsonString);

                        string reply = emotionData.reply;
                        string emotion = emotionData.emotion;

                        // UI更新・イベント発火
                        uiText.text = reply;
                        OnEmotionReceived?.Invoke(emotion);

                        if (objectSpawner != null) objectSpawner.SpawnObject(emotionData.spawnObject);

                        // ★変更: 音声合成時間の計測とログ出力
                        if (voicevoxBridge != null)
                        {
                            if (!string.IsNullOrEmpty(reply))
                            {
                                timeToSynthesis = latencyStopwatch.ElapsedMilliseconds;
                                voicevoxBridge.Speak(reply);

                                // CSV形式でログ出力
                                Debug.Log($"[LATENCY_DATA],{newMessage},{timeToInference - requestStart},{timeToSynthesis}");
                            }
                        }

                        // 7. 成功した場合のみ履歴を確定・保存
                        TextContent botContent = new TextContent
                        {
                            role = "model",
                            parts = new TextPart[] { new TextPart { text = jsonString } }
                        };
                        contentsList.Add(botContent);

                        ManageHistory(contentsList);
                        chatHistory = contentsList.ToArray();

                        SaveHistory();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"JSONパースエラー: {e.Message}");
                        uiText.text = rawResponseText; // パース失敗時は生のテキストを表示
                    }
                }
            }

            OnChatFinished?.Invoke();
            isProcessing = false;
        }
    }

    private void ManageHistory(List<TextContent> historyList)
    {
        int maxMessages = maxHistoryTurns * 2; // ユーザー + モデル で1ターン
        if (historyList.Count > maxMessages)
        {
            int removeCount = historyList.Count - maxMessages;
            historyList.RemoveRange(0, removeCount);
        }
    }

    // ▼▼▼ セーブ＆ロード機能 ▼▼▼

    public void SaveHistory()
    {
        if (chatHistory == null || chatHistory.Length == 0) return;

        ChatHistoryWrapper wrapper = new ChatHistoryWrapper();
        wrapper.history = chatHistory;

        string json = JsonUtility.ToJson(wrapper, true);
        string path = Path.Combine(Application.persistentDataPath, saveFileName);

        try
        {
            File.WriteAllText(path, json);
            Debug.Log($"[System] 会話履歴を保存しました: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[System] 保存に失敗しました: {e.Message}");
        }
    }

    public void LoadHistory()
    {
        string path = Path.Combine(Application.persistentDataPath, saveFileName);

        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                ChatHistoryWrapper wrapper = JsonUtility.FromJson<ChatHistoryWrapper>(json);

                if (wrapper != null && wrapper.history != null)
                {
                    chatHistory = wrapper.history;
                    Debug.Log($"[System] 会話履歴をロードしました ({chatHistory.Length} 件)");
                }
                else
                {
                    chatHistory = new TextContent[] { };
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[System] ロードに失敗しました: {e.Message}");
                chatHistory = new TextContent[] { };
            }
        }
        else
        {
            chatHistory = new TextContent[] { };
        }
    }

    public void ClearHistoryData()
    {
        chatHistory = new TextContent[] { };
        string path = Path.Combine(Application.persistentDataPath, saveFileName);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("[System] 会話履歴ファイルを削除しました。");
        }
    }
    // ▼▼▼ 入力モード切替ボタンから呼ぶメソッド ▼▼▼
    public void ToggleInputMode()
    {
        SwitchInputMode(!isVoiceMode);
    }

    private void SwitchInputMode(bool toVoiceMode)
    {
        isVoiceMode = toVoiceMode;

        if (textInputGroup != null) textInputGroup.SetActive(!isVoiceMode);
        if (voiceInputGroup != null) voiceInputGroup.SetActive(isVoiceMode);

        if (voiceStatusText != null) voiceStatusText.text = isVoiceMode ? "マイクボタンを押して話す" : "";
    }

    // ▼▼▼ 音声認識結果を受け取った時の処理 ▼▼▼
    private void HandleSpeechInput(string recognizedText)
    {
        if (string.IsNullOrEmpty(recognizedText)) return;

        // ★追加: 計測開始
        latencyStopwatch.Restart();
        Debug.Log($"[TimeLog] Start Speech: {recognizedText}");

        // 認識されたテキストをUIに表示（確認用）
        if (voiceStatusText != null) voiceStatusText.text = $"認識: {recognizedText}";

        // チャット送信処理へ
        StartCoroutine(SendChatRequestToGemini(recognizedText));
    }

    private void HandleSpeechHypothesis(string hypothesisText)
    {
        // 話している最中のテキストを表示
        if (voiceStatusText != null) voiceStatusText.text = hypothesisText + "...";
    }

    // ▼▼▼ マイクボタンから呼ぶメソッド ▼▼▼
    public void OnMicrophoneButtonClicked()
    {
        if (speechToTextManager != null)
        {
            speechToTextManager.ToggleRecording();
        }
    }

    // ▼▼▼ 追加: 会話リセット機能 ▼▼▼
    public void ResetConversation()
    {
        // 1. 内部メモリの履歴を消去
        chatHistory = new TextContent[] { };

        // 2. 保存ファイルを削除
        string path = Path.Combine(Application.persistentDataPath, saveFileName);
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                Debug.Log($"[System] 履歴ファイルを削除しました: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[System] 削除エラー: {e.Message}");
            }
        }

        // 3. UIの表示をリセット
        if (uiText != null)
        {
            uiText.text = "記憶をリセットしました。\n新しい設定で会話を始められます。";
        }

        if (inputField != null)
        {
            inputField.text = "";
        }

        // 4. 処理中フラグを解除（万が一スタックしていた場合のため）
        isProcessing = false;

        // 5. 感情をリセット（真顔に戻す）
        OnEmotionReceived?.Invoke("neutral");

        Debug.Log("[System] 会話履歴とメモリをリセットしました。");
    }
}
