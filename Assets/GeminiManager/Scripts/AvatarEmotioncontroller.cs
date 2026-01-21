using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Animatorコンポーネントが必須であることをUnityに伝えます（アタッチ忘れ防止）
[RequireComponent(typeof(Animator))]
public class AvatarEmotionController : MonoBehaviour
{
    // 表情がデフォルトに戻ったことを他のスクリプト（GeminiManagerなど）に知らせるイベント
    public static event System.Action OnEmotionResetToDefault;

    [Header("表情設定")]
    [Tooltip("Geminiから受け取る感情名のリスト (例: joy, sadness, anger)")]
    public List<string> emotionNames;

    [Tooltip("上記感情名に対応するAnimatorのFaceパラメータの値 (例: 1, 2, 3)")]
    public List<int> faceIndexes;

    [Header("自動リセット設定")]
    [Tooltip("表情を変えた後、標準の表情に戻るまでの秒数")]
    public float timeToReturnToDefault = 3.0f;

    // 内部変数
    private Animator animator;
    private Coroutine returnToDefaultCoroutine; // 現在実行中のタイマー処理を保持
    private Dictionary<string, int> emotionDictionary; // 高速検索用の辞書

    // 標準の表情（真顔）のインデックス番号。通常は0
    private const int FACE_DEFAULT_INDEX = 0;

    void Awake()
    {
        animator = GetComponent<Animator>();

        // 起動時に必ず標準の表情にリセットする
        SetFace(FACE_DEFAULT_INDEX);
        Debug.Log("AvatarEmotionController: 初期化完了。表情をデフォルト(0)に設定しました。");

        // リストから辞書を作成する（実行時の検索速度を上げるため）
        InitializeEmotionDictionary();
    }

    // イベントの登録：Geminiから感情を受け取れるようにする
    void OnEnable()
    {
        UnityAndGeminiV3.OnEmotionReceived += HandleEmotion;
    }

    // イベントの解除：メモリリーク防止
    void OnDisable()
    {
        UnityAndGeminiV3.OnEmotionReceived -= HandleEmotion;
    }

    // 辞書の初期化処理
    private void InitializeEmotionDictionary()
    {
        emotionDictionary = new Dictionary<string, int>();

        // 設定ミスチェック
        if (emotionNames.Count != faceIndexes.Count)
        {
            Debug.LogError("【設定エラー】'Emotion Names'と'Face Indexes'の数が一致していません！Inspectorを確認してください。", this.gameObject);
            return;
        }

        for (int i = 0; i < emotionNames.Count; i++)
        {
            // 小文字化＆空白削除で、表記ゆれに強くする
            string key = emotionNames[i].ToLower().Trim();

            if (!string.IsNullOrEmpty(key))
            {
                // 重複チェック（同じ感情名が既に登録されていないか）
                if (!emotionDictionary.ContainsKey(key))
                {
                    emotionDictionary.Add(key, faceIndexes[i]);
                }
                else
                {
                    Debug.LogWarning($"【警告】感情名 '{key}' が重複しています。2つ目は無視されます。");
                }
            }
        }
    }

    // Geminiから感情を受け取った時に呼ばれる関数
    private void HandleEmotion(string emotion)
    {
        // 既に「表情リセットタイマー」が動いていたら、一旦キャンセルする
        if (returnToDefaultCoroutine != null)
        {
            StopCoroutine(returnToDefaultCoroutine);
            returnToDefaultCoroutine = null;
        }

        // 受け取った感情名を整形（小文字化など）
        string cleanedEmotion = emotion.ToLower().Trim();

        // 辞書に登録されている感情かチェック
        if (emotionDictionary.TryGetValue(cleanedEmotion, out int faceIndex))
        {
            // 表情を変更
            SetFace(faceIndex);
            Debug.Log($"表情を変更しました: {cleanedEmotion} (Index: {faceIndex})");

            // デフォルト以外の表情なら、数秒後に戻すタイマーを開始
            if (faceIndex != FACE_DEFAULT_INDEX)
            {
                returnToDefaultCoroutine = StartCoroutine(ReturnToDefaultFace());
            }
        }
        else
        {
            // 知らない感情が来たらデフォルトに戻す
            SetFace(FACE_DEFAULT_INDEX);
            Debug.LogWarning($"未登録の感情 '{emotion}' を受信しました。デフォルトの表情に戻します。");
        }
    }

    // 実際にAnimatorのパラメータを操作する関数
    private void SetFace(int index)
    {
        if (animator != null)
        {
            animator.SetInteger("Face", index);
        }
    }

    // 指定時間待ってから表情を元に戻すコルーチン
    private IEnumerator ReturnToDefaultFace()
    {
        // 指定された秒数待機
        yield return new WaitForSeconds(timeToReturnToDefault);

        // 表情をリセット
        SetFace(FACE_DEFAULT_INDEX);
        Debug.Log("時間が経過したため、表情をデフォルトに戻しました。");

        returnToDefaultCoroutine = null;

        // 「表情が戻った」ことを他のスクリプトに通知（会話終了の合図など）
        Debug.Log("AvatarEmotionController: OnEmotionResetToDefaultイベントを発行します。");
        OnEmotionResetToDefault?.Invoke();
    }
}
