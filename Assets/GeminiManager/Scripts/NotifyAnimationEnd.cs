using UnityEngine;

public class NotifyAnimationEnd : StateMachineBehaviour
{
    // このステート（アニメーション）の再生が終了し、次のステートへ遷移する瞬間に呼ばれます
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {

        IdleAnimationRandomizer randomizer = animator.GetComponent<IdleAnimationRandomizer>();

        if (randomizer != null)
        {
            // 次のランダム待機モーションの抽選を依頼する
            randomizer.AnimationFinished();
        }
    }
}
