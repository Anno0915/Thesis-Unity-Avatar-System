using UnityEngine;

public class BodyPartTag : MonoBehaviour
{
    public enum PartType
    {
        Head,   // 頭
        Body,   // 体（腹）
        Arm,    // 腕
        Leg,    // 足
        Hand,   // 手
        Chest,  //胸
        Buttocks,  //お尻
    }

    [Tooltip("このコライダーが担当する身体の部位")]
    public PartType partType = PartType.Body;
}
