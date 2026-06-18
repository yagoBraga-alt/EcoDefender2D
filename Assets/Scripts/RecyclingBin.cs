using UnityEngine;

// Simple bin that Player.cs queries when delivering.
// Place 4 instances in the scene, one per TrashType.
public class RecyclingBin : MonoBehaviour
{
    [Header("Which trash this bin accepts")]
    public TrashItem.TrashType AcceptedType;

    void Start()
    {
        // Auto-colour so designers can tell them apart in the editor
        var sr = GetComponent<SpriteRenderer>();
        if (sr) sr.color = TrashItem.ColorFor(AcceptedType);
    }
}
