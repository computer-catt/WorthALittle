using Photon.Pun;
using UnityEngine;

namespace WorthALittle;

/// <summary>
/// This is a MonoBehaviour object placed on gameobjects with hinges that are valid to be made valuable
/// the purpose for its existence is proper syncing with rpc.
/// </summary>
public class WorthALittleMore : MonoBehaviour {
    private PhotonView? _view = null;
    private PhysGrabHinge? _grabHinge = null;
    private bool _madeValuable = false;

    private void Awake() {
        _view = GetComponent<PhotonView>();
        _grabHinge = GetComponent<PhysGrabHinge>();
    }

    public void MakeValuable() {
        if (_madeValuable) return;
        if (SemiFunc.IsMultiplayer())
            _view?.RPC("SetBrokenHingeValuable", RpcTarget.All);
        else SetBrokenHingeValuable();
        _madeValuable = true;
    }


    [PunRPC]
    private void SetBrokenHingeValuable() => WorthALittle.SetBrokenHingeValuable(_grabHinge);
}