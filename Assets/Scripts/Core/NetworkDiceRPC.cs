using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

/// <summary>
/// PhotonView 없이 RaiseEvent로 주사위/점수 동기화.
/// PhotonView 등록 문제를 완전히 우회.
/// </summary>
public class NetworkDiceRPC : MonoBehaviour,
    IOnEventCallback
{
    const byte EV_DICE_ROLL      = 1;
    const byte EV_CATEGORY_SEL   = 2;

    private DiceController _diceCtrl;
    private DiceBox3D      _diceBox3D;
    private TurnManager    _turnManager;

    void Start()
    {
        _diceCtrl    = FindObjectOfType<DiceController>();
        _diceBox3D   = FindObjectOfType<DiceBox3D>();
        _turnManager = FindObjectOfType<TurnManager>();

        PhotonNetwork.AddCallbackTarget(this);
        NetLog.Info($"NetworkDiceRPC Start — InRoom={PhotonNetwork.InRoom}, IsOnline={GameManager.Instance?.IsOnline}, IsMaster={PhotonNetwork.IsMasterClient}");
    }

    void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    // ── 주사위 전송 ───────────────────────────────────────────────────
    public void SendDiceRoll(int[] finalValues, bool[] keepFlags, float gaugeValue, int seed)
    {
        byte[] keepBytes = new byte[keepFlags.Length];
        for (int i = 0; i < keepFlags.Length; i++)
            keepBytes[i] = keepFlags[i] ? (byte)1 : (byte)0;

        // int[] → object[] 로 패킹
        object[] data = new object[]
        {
            finalValues[0], finalValues[1], finalValues[2], finalValues[3], finalValues[4],
            keepBytes[0],   keepBytes[1],   keepBytes[2],   keepBytes[3],   keepBytes[4],
            gaugeValue, seed
        };

        var opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(EV_DICE_ROLL, data, opts, SendOptions.SendReliable);
        NetLog.Send($"DiceRoll [{string.Join(",", finalValues)}] gauge={gaugeValue:F2} seed={seed}");
    }

    // ── 점수 전송 ─────────────────────────────────────────────────────
    public void SendCategorySelected(int categoryIndex, int playerIndex, int score)
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsOnline) return;

        object[] data = new object[] { categoryIndex, playerIndex, score };
        var opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(EV_CATEGORY_SEL, data, opts, SendOptions.SendReliable);
        NetLog.Send($"CategorySelected cat={categoryIndex} player={playerIndex} score={score}");
    }

    // ── 이벤트 수신 ───────────────────────────────────────────────────
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == EV_DICE_ROLL)
        {
            var d = (object[])photonEvent.CustomData;
            var finalValues = new int[]  { (int)d[0], (int)d[1], (int)d[2], (int)d[3], (int)d[4] };
            var keepFlags   = new bool[] {
                (byte)d[5]==1, (byte)d[6]==1, (byte)d[7]==1, (byte)d[8]==1, (byte)d[9]==1
            };
            float gauge = (float)d[10];
            int   seed  = (int)d[11];

            NetLog.Recv($"DiceRoll [{string.Join(",", finalValues)}] gauge={gauge:F2} seed={seed}");

            for (int i = 0; i < finalValues.Length && i < _diceCtrl.Dice.Length; i++)
            {
                _diceCtrl.Dice[i].Value        = finalValues[i];
                _diceCtrl.Dice[i].IsKeptForced = keepFlags[i];
            }
            _diceCtrl.ReceiveRemoteRoll();
            _diceBox3D?.ThrowDiceAnimated(finalValues, keepFlags, gauge, seed);
        }
        else if (photonEvent.Code == EV_CATEGORY_SEL)
        {
            var d           = (object[])photonEvent.CustomData;
            int categoryIndex = (int)d[0];
            int playerIndex   = (int)d[1];
            int score         = (int)d[2];

            NetLog.Recv($"CategorySelected cat={categoryIndex} player={playerIndex} score={score}");
            _turnManager?.ReceiveRemoteCategorySelected(categoryIndex, playerIndex, score);
        }
    }
}
