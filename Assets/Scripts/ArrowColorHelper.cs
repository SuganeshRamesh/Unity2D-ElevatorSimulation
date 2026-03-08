using UnityEngine;
using TMPro;

/// <summary>
/// Lets ElevatorManager color the direction arrow TMP text.
/// </summary>
public class ArrowColorHelper : MonoBehaviour
{
    public TextMeshProUGUI tmp;
    public Color offColor;
    public Color onColor;

    public void SetActive(bool active)
    {
        if (tmp != null)
            tmp.color = active ? onColor : offColor;
    }
}
