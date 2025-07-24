using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;
using UnityMultiplayer;

public class JoinCodeSign : NetworkBehaviour
{
    [SerializeField]
    private TMP_Text _joinCodeText;

    private readonly SyncVar<string> _joinCode = new();

    public override void OnStartServer()
    {
        _joinCode.Value = UnityCloudManager.Instance.JoinCode;
        _joinCodeText.text = _joinCode.Value;
    }

    public override void OnStartNetwork()
    {
        _joinCodeText.text = _joinCode.Value;
    }
}