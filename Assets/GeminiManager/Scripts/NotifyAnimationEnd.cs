using UnityEngine;

public class NotifyAnimationEnd : StateMachineBehaviour
{
    // このステート（アニメーション）の再生が終了し、次のステートへ遷移する瞬間に呼ばれます
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 【重要】StateMachineBehaviourはアセットとして扱われ、複数のキャラクターで共有される可能性があります。
        // そのため、メンバ変数に `randomizer` をキャッシュ（保存）してはいけません。
        // 毎回、引数の `animator` からコンポーネントを取得するのが、安全で正しい実装です。

        IdleAnimationRandomizer randomizer = animator.GetComponent<IdleAnimationRandomizer>();

        if (randomizer != null)
        {
            // 次のランダム待機モーションの抽選を依頼する
            randomizer.AnimationFinished();
        }
    }
}
