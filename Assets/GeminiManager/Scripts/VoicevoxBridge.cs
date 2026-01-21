using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

// このスクリプトにはAudioSourceが必須であることをUnityに伝えます
[RequireComponent(typeof(AudioSource))]
public class VoicevoxBridge : MonoBehaviour
{
    [Header("VOICEVOX設定")]
    [Tooltip("VOICEVOXアプリのアドレス (末尾のスラッシュは不要)")]
    public string voicevoxUrl = "http://127.0.0.1:50021";

    [Tooltip("話者ID (例: 2=四国めたんノーマル, 3=ずんだもんノーマル)")]
    public int speakerId = 2;

    private AudioSource audioSource;

    void Start()
    {
        // RequireComponentにより、必ず取得できることが保証されます
        audioSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// 指定されたテキストをVOICEVOXで音声化して再生します
    /// </summary>
    public void Speak(string text)
    {
        // 空文字なら何もしない
        if (string.IsNullOrEmpty(text)) return;

        StartCoroutine(GenerateAndPlayVoice(text));
    }

    private IEnumerator GenerateAndPlayVoice(string text)
    {
        // 1. 音声合成用クエリの作成 (Audio Query)
        // テキストを渡して、イントネーションやアクセントのパラメータ(JSON)を取得します

        // URLエンコードを行って安全にパラメータ化
        string queryUrl = $"{voicevoxUrl}/audio_query?speaker={speakerId}&text={UnityWebRequest.EscapeURL(text)}";
        string queryJson = "";

        // POSTリクエストを作成 (bodyは空でOK)
        using (UnityWebRequest www = UnityWebRequest.PostWwwForm(queryUrl, ""))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"VOICEVOX Query Error: {www.error}\nURL: {queryUrl}");
                yield break; // エラーならここで中断
            }
            queryJson = www.downloadHandler.text;
        }

        // 2. 音声データの合成 (Synthesis)
        // 取得したクエリJSONをそのまま送り返して、WAV音声データを取得します

        string synthesisUrl = $"{voicevoxUrl}/synthesis?speaker={speakerId}";

        using (UnityWebRequest www = new UnityWebRequest(synthesisUrl, "POST"))
        {
            // JSONデータをバイト配列に変換してアップロード設定
            byte[] bodyRaw = Encoding.UTF8.GetBytes(queryJson);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);

            // WAV形式としてダウンロードするハンドラーを設定
            www.downloadHandler = new DownloadHandlerAudioClip(synthesisUrl, AudioType.WAV);

            // ヘッダー設定 (JSONを送ることを明示)
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"VOICEVOX Synthesis Error: {www.error}\nURL: {synthesisUrl}");
                yield break;
            }

            // 3. 再生
            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);

            // クリップをセットして再生
            audioSource.clip = clip;
            audioSource.Play();
        }
    }
}
