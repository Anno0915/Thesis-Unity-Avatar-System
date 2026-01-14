using UnityEngine;

public class TouchReactionHandler : MonoBehaviour
{
    [Header("Gemini連携")]
    public UnityAndGeminiV3 geminiManager;

    [Header("クールタイム")]
    public float interactionCooldown = 2.0f; // 連続で触れないようにする
    private float lastInteractionTime = -10f;

    public void OnBodyPartTouched(BodyPartTag.PartType partType)
    {
        // クールタイムチェック
        if (Time.time - lastInteractionTime < interactionCooldown) return;
        lastInteractionTime = Time.time;

        string messageToSend = "";

        switch (partType)
        {
            case BodyPartTag.PartType.Head:
                Debug.Log("頭が触られました");
                // AIに送るメッセージ（ト書き形式）
                messageToSend = "(ユーザーがあなたの頭を優しく撫でました)";
                break;

            case BodyPartTag.PartType.Body:
                Debug.Log("腹が触られました");
                messageToSend = "(ユーザーがあなたの腹をつつきました)";
                break;

            case BodyPartTag.PartType.Arm:
                Debug.Log("腕が触られました");
                messageToSend = "(ユーザーがあなたの腕に触れました)";
                break;

            case BodyPartTag.PartType.Hand:
                Debug.Log("手が触られました");
                messageToSend = "(ユーザーがあなたの手を握りました)";
                break;

            case BodyPartTag.PartType.Chest:
                Debug.Log("胸が触られました");
                messageToSend = "(ユーザーがあなたの胸に触れました)";
                break;

            case BodyPartTag.PartType.Buttocks:
                Debug.Log("尻が触られました");
                messageToSend = "(ユーザーがあなたの尻に触れました)";
                break;

            

            default:
                return;
        }

        // Geminiに状況を送信して、反応を生成させる
        if (geminiManager != null && !string.IsNullOrEmpty(messageToSend))
        {
            geminiManager.SendSystemMessage(messageToSend);
        }
    }
}
