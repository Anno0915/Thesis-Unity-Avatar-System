using UnityEngine;

[RequireComponent(typeof(Animator))]
public class SimpleHeadLook : MonoBehaviour
{
    [Header("ターゲット設定")]
    [Tooltip("視線を向ける対象。空欄の場合、自動的にMain Cameraを追従します")]
    public Transform target;

    [Header("動きの調整")]
    [Tooltip("視線の追従速度（滑らかさ）。数値が大きいほどゆっくり動きます")]
    public float smoothTime = 0.2f;

    [Header("IKの重み設定 (0〜1)")]
    [Tooltip("全体の重み (1で完全追従, 0で無効)")]
    [Range(0, 1)]
    public float lookAtWeight = 1.0f;

    [Tooltip("体をどれくらい向けるか (0だと首だけ動く, 1だと体ごと向く)")]
    [Range(0, 1)]
    public float bodyWeight = 0.2f;

    [Tooltip("頭をどれくらい向けるか")]
    [Range(0, 1)]
    public float headWeight = 0.9f;

    [Tooltip("目をどれくらい向けるか")]
    [Range(0, 1)]
    public float eyesWeight = 1.0f;

    [Tooltip("関節の制限 (0だと制限なし、1だと完全に制限＝動かない)")]
    [Range(0, 1)]
    public float clampWeight = 0.5f;

    private Animator animator;
    private Vector3 currentLookPos;
    private Vector3 velocity;

    void Start()
    {
        animator = GetComponent<Animator>();

        // ターゲット自動設定
        if (target == null)
        {
            if (Camera.main != null)
            {
                target = Camera.main.transform;
            }
        }

        // 【修正箇所】初期位置の計算ロジックを変更
        if (target != null)
        {
            // ターゲットがあるなら、最初からその位置を初期値にする
            // これにより、開始直後に視線が移動するのを防ぎ、最初からカメラを見ている状態にする
            currentLookPos = target.position;
        }
        else
        {
            // ターゲットがない場合、「頭の高さ」を基準に正面を見るようにする
            // (以前は足元基準だったため下を向いてしまっていた)
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
            {
                currentLookPos = head.position + transform.forward;
            }
            else
            {
                // Headボーンが取れない場合のフォールバック（高さ1.5mと仮定）
                currentLookPos = transform.position + Vector3.up * 1.5f + transform.forward;
            }
        }
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || target == null) return;

        // ターゲットの位置へ滑らかに移動
        currentLookPos = Vector3.SmoothDamp(currentLookPos, target.position, ref velocity, smoothTime);

        // IKの適用
        animator.SetLookAtWeight(lookAtWeight, bodyWeight, headWeight, eyesWeight, clampWeight);
        animator.SetLookAtPosition(currentLookPos);
    }
}
