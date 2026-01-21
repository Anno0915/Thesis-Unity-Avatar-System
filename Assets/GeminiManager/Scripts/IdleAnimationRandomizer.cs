using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Animator))]
public class IdleAnimationRandomizer : MonoBehaviour
{
    [Header("タイミング設定")]
    [Tooltip("次のモーションまでの最小待機時間")]
    [SerializeField] private float minWaitSeconds = 3.0f;
    [Tooltip("次のモーションまでの最大待機時間")]
    [SerializeField] private float maxWaitSeconds = 8.0f;

    [Header("モーション設定")]
    [Tooltip("再生したい待機モーションのIndex番号リスト")]
    [SerializeField] private List<int> specialIdleIndexes;

    [Tooltip("「何もしない」が選ばれる確率（個数）。数が多いほど、モーション再生頻度が下がります")]
    [SerializeField] private int nothingWeight = 3;

    private Animator animator;
    private Coroutine idleCoroutine;

    // Animatorパラメータのハッシュ化（高速化のため）
    private static readonly int IdleIndexHash = Animator.StringToHash("IdleIndex");
    private static readonly int IsChattingHash = Animator.StringToHash("IsChatting");

    // これにより「全てのモーションを一通り再生するまで重複しない」挙動を実現
    private Queue<int> animationQueue = new Queue<int>();

    void OnEnable()
    {
        UnityAndGeminiV3.OnChatStarted += HandleChatStarted;
        UnityAndGeminiV3.OnChatFinished += HandleChatFinished;
    }

    void OnDisable()
    {
        UnityAndGeminiV3.OnChatStarted -= HandleChatStarted;
        UnityAndGeminiV3.OnChatFinished -= HandleChatFinished;
    }

    void Start()
    {
        animator = GetComponent<Animator>();

        if (specialIdleIndexes == null || specialIdleIndexes.Count == 0)
        {
            Debug.LogWarning("IdleAnimationRandomizer: 特別な待機モーションが設定されていません。", this);
            return;
        }

        // 最初のキューを作成
        RefillQueue();

        // 起動時は会話していないので、待機モーション処理を開始
        HandleChatFinished();
    }

    // モーションの補充とシャッフル
    private void RefillQueue()
    {
        animationQueue.Clear();

        // 1. 設定されたモーションをリストに入れる
        List<int> tempList = new List<int>(specialIdleIndexes);

        // 2. 「何もしない(-1)」を設定された重みの分だけ入れる
        for (int i = 0; i < nothingWeight; i++)
        {
            tempList.Add(-1);
        }

        // 3. リストをシャッフルする（フィッシャー–イェーツのシャッフル）
        int n = tempList.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            int value = tempList[k];
            tempList[k] = tempList[n];
            tempList[n] = value;
        }

        // 4. キューに詰め込む
        foreach (int index in tempList)
        {
            animationQueue.Enqueue(index);
        }

        Debug.Log($"IdleRandomizer: モーションリストをリセットしました。次のサイクル数: {animationQueue.Count}");
    }

    private void HandleChatStarted()
    {
        // 会話中はフラグを立てて、待機モーションのコルーチンを止める
        animator.SetBool(IsChattingHash, true);

        if (idleCoroutine != null)
        {
            StopCoroutine(idleCoroutine);
            idleCoroutine = null;
        }
    }

    private void HandleChatFinished()
    {
        // 会話終了時はフラグを下ろして、待機モーション処理を再開
        animator.SetBool(IsChattingHash, false);

        // 即座に再開せず、AnimationFinished経由で自然に開始させる
        AnimationFinished();
    }

    // アニメーション終了時（または待機時間終了時）に呼ばれるメソッド
    public void AnimationFinished()
    {
        // 会話中なら何もしない
        if (animator.GetBool(IsChattingHash)) return;

        if (idleCoroutine != null)
        {
            StopCoroutine(idleCoroutine);
        }
        idleCoroutine = StartCoroutine(RandomIdleCoroutine());
    }

    private IEnumerator RandomIdleCoroutine()
    {
        // まずパラメータをリセット（待機状態に戻す）
        animator.SetInteger(IdleIndexHash, 0);

        // ランダムな時間待機する
        float waitTime = Random.Range(minWaitSeconds, maxWaitSeconds);
        yield return new WaitForSeconds(waitTime);

        // 会話が始まっていたら中断
        if (animator.GetBool(IsChattingHash)) yield break;

        // キューから次のモーションを取り出す 
        if (animationQueue.Count == 0)
        {
            RefillQueue();
        }

        int nextIdleIndex = animationQueue.Dequeue();

        // -1 は「何もしない（標準待機継続）」
        if (nextIdleIndex != -1)
        {
            // モーション再生
            animator.SetInteger(IdleIndexHash, nextIdleIndex);

        }
        else
        {
            // 「何もしない」が選ばれた場合、もう一度待機時間をやり直す
            AnimationFinished();
        }
    }
}
