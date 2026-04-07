using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class NetworkDiceRPC : MonoBehaviour, IOnEventCallback
{
    const byte EV_DICE_ROLL = 1;
    const byte EV_SCORE_REQUEST = 2;
    const byte EV_SCORE_CONFIRMED = 3;
    const byte EV_CUP_SHAKE_START = 4;
    const byte EV_CUP_SHAKE_STOP = 5;

    private DiceController _diceCtrl;
    private DiceBox3D _diceBox3D;
    private TurnManager _turnManager;
    private UICupShaker _uiCupShaker;
    private bool IsOnlineGame => GameManager.Instance != null && GameManager.Instance.IsOnline;

    void Start()
    {
        EnsureReferences();
        PhotonNetwork.AddCallbackTarget(this);
        NetLog.Info($"NetworkDiceRPC Start InRoom={PhotonNetwork.InRoom}, IsOnline={GameManager.Instance?.IsOnline}, IsMaster={PhotonNetwork.IsMasterClient}");
    }

    void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    public void SendDiceRoll(int[] finalValues, bool[] keepFlags, float gaugeValue, int seed, int turnSerial, int rollIndex)
    {
        byte[] keepBytes = new byte[keepFlags.Length];
        for (int i = 0; i < keepFlags.Length; i++)
            keepBytes[i] = keepFlags[i] ? (byte)1 : (byte)0;

        object[] data = new object[]
        {
            finalValues[0], finalValues[1], finalValues[2], finalValues[3], finalValues[4],
            keepBytes[0], keepBytes[1], keepBytes[2], keepBytes[3], keepBytes[4],
            gaugeValue, seed, turnSerial, rollIndex
        };

        var opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(EV_DICE_ROLL, data, opts, SendOptions.SendReliable);
        NetLog.Send($"DiceRoll [{string.Join(",", finalValues)}] gauge={gaugeValue:F2} seed={seed} turn={turnSerial} roll={rollIndex}");
    }

    public void SendScoreRequest(int categoryIndex, int playerIndex, int turnSerial)
    {
        if (!IsOnlineGame) return;

        object[] data = new object[] { categoryIndex, playerIndex, turnSerial };
        var opts = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
        PhotonNetwork.RaiseEvent(EV_SCORE_REQUEST, data, opts, SendOptions.SendReliable);
        NetLog.Send($"ScoreRequest cat={categoryIndex} player={playerIndex} turn={turnSerial}");
    }

    public void SendScoreConfirmed(int categoryIndex, int playerIndex, int score,
        int turnSerial, int nextPlayerIndex, int nextRound, int nextTurnSerial, bool gameOver)
    {
        if (!IsOnlineGame) return;

        object[] data = new object[]
        {
            categoryIndex, playerIndex, score, turnSerial,
            nextPlayerIndex, nextRound, nextTurnSerial, gameOver
        };

        var opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(EV_SCORE_CONFIRMED, data, opts, SendOptions.SendReliable);
        NetLog.Send($"ScoreConfirmed cat={categoryIndex} player={playerIndex} score={score} turn={turnSerial}->{nextTurnSerial}");
    }

    public void SendCupShakeStart()
    {
        if (!IsOnlineGame) return;
        int turnSerial = _turnManager != null ? _turnManager.CurrentTurnSerial : 0;
        object[] data = new object[] { turnSerial };
        var opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(EV_CUP_SHAKE_START, data, opts, SendOptions.SendReliable);
        NetLog.Send($"CupShakeStart turn={turnSerial}");
    }

    public void SendCupShakeStop(float gaugeValue)
    {
        if (!IsOnlineGame) return;
        int turnSerial = _turnManager != null ? _turnManager.CurrentTurnSerial : 0;
        object[] data = new object[] { turnSerial, gaugeValue };
        var opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(EV_CUP_SHAKE_STOP, data, opts, SendOptions.SendReliable);
        NetLog.Send($"CupShakeStop turn={turnSerial} gauge={gaugeValue:F2}");
    }

    public void OnEvent(EventData photonEvent)
    {
        EnsureReferences();

        switch (photonEvent.Code)
        {
            case EV_DICE_ROLL:
                HandleDiceRollEvent((object[])photonEvent.CustomData);
                break;
            case EV_SCORE_REQUEST:
                HandleScoreRequestEvent((object[])photonEvent.CustomData);
                break;
            case EV_SCORE_CONFIRMED:
                HandleScoreConfirmedEvent((object[])photonEvent.CustomData);
                break;
            case EV_CUP_SHAKE_START:
                HandleCupShakeStartEvent((object[])photonEvent.CustomData);
                break;
            case EV_CUP_SHAKE_STOP:
                HandleCupShakeStopEvent((object[])photonEvent.CustomData);
                break;
        }
    }

    void EnsureReferences()
    {
        if (_diceCtrl == null) _diceCtrl = FindObjectOfType<DiceController>();
        if (_diceBox3D == null) _diceBox3D = FindObjectOfType<DiceBox3D>();
        if (_turnManager == null) _turnManager = FindObjectOfType<TurnManager>();
        if (_uiCupShaker == null) _uiCupShaker = FindObjectOfType<UICupShaker>();
    }

    void HandleDiceRollEvent(object[] data)
    {
        var finalValues = new[] { (int)data[0], (int)data[1], (int)data[2], (int)data[3], (int)data[4] };
        var keepFlags = new[]
        {
            (byte)data[5] == 1, (byte)data[6] == 1, (byte)data[7] == 1, (byte)data[8] == 1, (byte)data[9] == 1
        };
        float gauge = (float)data[10];
        int seed = (int)data[11];
        int turnSerial = (int)data[12];
        int rollIndex = (int)data[13];

        if (!IsCurrentTurn(turnSerial))
        {
            NetLog.Warn($"Ignore stale dice roll. remoteTurn={turnSerial} localTurn={_turnManager?.CurrentTurnSerial}");
            return;
        }

        if (_diceCtrl != null && rollIndex <= _diceCtrl.RollCount)
        {
            NetLog.Warn($"Ignore duplicate dice roll. remoteRoll={rollIndex} localRoll={_diceCtrl.RollCount}");
            return;
        }

        NetLog.Recv($"DiceRoll [{string.Join(",", finalValues)}] gauge={gauge:F2} seed={seed} turn={turnSerial} roll={rollIndex}");

        if (_diceCtrl != null)
        {
            for (int i = 0; i < finalValues.Length && i < _diceCtrl.Dice.Length; i++)
            {
                _diceCtrl.Dice[i].Value = finalValues[i];
                _diceCtrl.Dice[i].IsKeptForced = keepFlags[i];
            }

            _diceCtrl.ReceiveRemoteRoll(rollIndex);
        }

        _diceBox3D?.ThrowDiceAnimated(finalValues, keepFlags, gauge, seed);
    }

    void HandleScoreRequestEvent(object[] data)
    {
        int categoryIndex = (int)data[0];
        int playerIndex = (int)data[1];
        int turnSerial = (int)data[2];

        NetLog.Recv($"ScoreRequest cat={categoryIndex} player={playerIndex} turn={turnSerial}");
        _turnManager?.ReceiveRemoteScoreRequest(categoryIndex, playerIndex, turnSerial);
    }

    void HandleScoreConfirmedEvent(object[] data)
    {
        int categoryIndex = (int)data[0];
        int playerIndex = (int)data[1];
        int score = (int)data[2];
        int turnSerial = (int)data[3];
        int nextPlayerIndex = (int)data[4];
        int nextRound = (int)data[5];
        int nextTurnSerial = (int)data[6];
        bool gameOver = (bool)data[7];

        NetLog.Recv($"ScoreConfirmed cat={categoryIndex} player={playerIndex} score={score} turn={turnSerial}->{nextTurnSerial}");
        _turnManager?.ReceiveRemoteScoreConfirmed(
            categoryIndex,
            playerIndex,
            score,
            turnSerial,
            nextPlayerIndex,
            nextRound,
            nextTurnSerial,
            gameOver);
    }

    void HandleCupShakeStartEvent(object[] data)
    {
        int turnSerial = (int)data[0];
        if (!IsCurrentTurn(turnSerial)) return;

        NetLog.Recv($"CupShakeStart turn={turnSerial}");
        _uiCupShaker?.StartRemoteShakeVisual();
    }

    void HandleCupShakeStopEvent(object[] data)
    {
        int turnSerial = (int)data[0];
        float gaugeValue = (float)data[1];
        if (!IsCurrentTurn(turnSerial)) return;

        NetLog.Recv($"CupShakeStop turn={turnSerial} gauge={gaugeValue:F2}");
        _uiCupShaker?.StopRemoteShakeVisual(gaugeValue);
    }

    bool IsCurrentTurn(int turnSerial)
    {
        return _turnManager == null || turnSerial == _turnManager.CurrentTurnSerial;
    }
}
