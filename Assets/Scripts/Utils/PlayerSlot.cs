using UnityEngine;
using UnityEngine.UI;

public class PlayerSlot : MonoBehaviour
{
    [SerializeField] private Text _nameText;
    [SerializeField] private Image _readyStatusImage;

    public void SetData(string playerName, bool isReady)
    {
        _nameText.text = playerName;
        _readyStatusImage.color = isReady ? Color.green : Color.red;
    }
}