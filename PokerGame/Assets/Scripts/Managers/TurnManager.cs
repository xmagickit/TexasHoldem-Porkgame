﻿using System;
using TMPro;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }
    public event Action<PlayerManager> OnPlayerTurn;

    [SerializeField] private TextMeshProUGUI _playerMoveInfoText;

    public PlayerManager CurrentPlayer { get; private set; }
    private int _currentPlayerIndex;

    public bool IsPreFlop
    {
        get => _isPreFlop;
    }
    [SerializeField] private bool _isPreFlop;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        GameManager.OnGameStateChanged += GameManager_OnGameStateChanged;
    }

    private void GameManager_OnGameStateChanged(GameManager.GameState state)
    {
        switch (state)
        {
            case GameManager.GameState.PreFlop:
                _isPreFlop = true;
                SetFirstPlayer(IsPreFlop); //true for IsPreFlop
                                           // GameManager.Instance.SetGameState(GameManager.GameState.PlayerTurn);
                break;
            case GameManager.GameState.PlayerTurn:
                OnPlayerTurn?.Invoke(CurrentPlayer);
                break;
            case GameManager.GameState.Flop:
                break;
            case GameManager.GameState.PostFlop:
                _isPreFlop = false;
                ResetTurnStatus();
                SetFirstPlayer(IsPreFlop); //false for IsPreFlop
                //PlayerTurn'e gecis bu sefer Poker Deck Manager'da. Bu yaklasımı sevmiyorum
                break;
            case GameManager.GameState.Turn:
                break;
            case GameManager.GameState.PostTurn:
                _isPreFlop = false;
                ResetTurnStatus();
                SetFirstPlayer(IsPreFlop); //false for IsPreFlop
                //PlayerTurn'e gecis bu sefer Poker Deck Manager'da. Bu yaklasımı sevmiyorum
                break;
            case GameManager.GameState.River:
                break;
            case GameManager.GameState.PostRiver:
                _isPreFlop = false;
                ResetTurnStatus();
                SetFirstPlayer(IsPreFlop); //false for IsPreFlop
                //PlayerTurn'e gecis bu sefer Poker Deck Manager'da. Bu yaklasımı sevmiyorum
                break;
            default:
                break;
        }
    }

    private void SetFirstPlayer(bool isPreFlop)
    {
        var activePlayers = GameManager.Instance.ActivePlayers;
        PlayerManager firstPlayer;

        if (isPreFlop)
        {
            // Pre-flop: Start after the big blind
            firstPlayer = DealerManager.Instance.GetFirstPlayerAfterBigBlind();
        }
        else
        {
            // Post-flop: Start from the small blind or the first active player to the left of the dealer button
            firstPlayer = DealerManager.Instance.GetFirstActivePlayerFromDealer();
            Debug.Log("After the flop, first player selected as: " + firstPlayer);
        }

        if (firstPlayer != null)
        {
            CurrentPlayer = firstPlayer;
            _currentPlayerIndex = activePlayers.IndexOf(CurrentPlayer);
        }
        else
        {
            //If Player is null, means all players are all in, cant make a move.
            // change the turn, flop>river> etc. 
        }
    }


    public void ChangePlayerTurn(bool isPreviousPlayerFolded)
    {
        if (GameManager.Instance.GetState() != GameManager.GameState.PlayerTurn) return;
        BetManager.Instance.CollectBets(CurrentPlayer);

        CurrentPlayer.IsPlayerTurn = false;

        if (IsBettingRoundConcludable())
        {
            // Proceed to collect bets into the pot, move to the next stage

            //BetManager.Instance.CurrentHighestBetAmount = 0; bunu sildik çünkü highest bet'i showdown'a kadar tutucaz. (ki o bet'e erisebilene kadar raise/call etsinelr)

            switch (GameManager.Instance.GetMainGameState())
            {
                case GameManager.GameState.PreFlop:
                    GameManager.Instance.SetGameState(GameManager.GameState.Flop);
                    return;
                case GameManager.GameState.PostFlop:
                    GameManager.Instance.SetGameState(GameManager.GameState.Turn);
                    return;
                case GameManager.GameState.PostTurn:
                    GameManager.Instance.SetGameState(GameManager.GameState.River);
                    return;
                case GameManager.GameState.PostRiver:
                    GameManager.Instance.SetGameState(GameManager.GameState.Showdown);
                    return;
                case GameManager.GameState.Showdown:
                    GameManager.Instance.SetGameState(GameManager.GameState.PotDistribution);
                    return;
                case GameManager.GameState.PotDistribution:
                    GameManager.Instance.SetGameState(GameManager.GameState.GameOver);
                    return;
                case GameManager.GameState.GameOver:
                    GameManager.Instance.SetGameState(GameManager.GameState.NewRound);
                    return;
            }
        }

        // Check if the previous player folded to make sure the next player is the correct one
        if (isPreviousPlayerFolded)
        {
            _currentPlayerIndex = (_currentPlayerIndex) % GameManager.Instance.ActivePlayers.Count;
            CurrentPlayer = GameManager.Instance.ActivePlayers[_currentPlayerIndex];

            // Check if the player is all in and skip their turn if they are
            if (CurrentPlayer.IsPlayerAllIn == true)
            {
                _currentPlayerIndex = (_currentPlayerIndex + 1) % GameManager.Instance.ActivePlayers.Count;
                CurrentPlayer = GameManager.Instance.ActivePlayers[_currentPlayerIndex];
            }
        }
        else
        {
            _currentPlayerIndex = (_currentPlayerIndex + 1) % GameManager.Instance.ActivePlayers.Count;
            CurrentPlayer = GameManager.Instance.ActivePlayers[_currentPlayerIndex];

            // Check if the player is all in and skip their turn if they are
            if (CurrentPlayer.IsPlayerAllIn == true)
            {
                _currentPlayerIndex = (_currentPlayerIndex + 1) % GameManager.Instance.ActivePlayers.Count;
                CurrentPlayer = GameManager.Instance.ActivePlayers[_currentPlayerIndex];
            }
        }

        CurrentPlayer.IsPlayerTurn = true;
        GameManager.Instance.SetGameState(GameManager.GameState.PlayerTurn);
    }

    private bool IsBettingRoundConcludable()
    {
        // Check if there has been any bet made
        if (BetManager.Instance.CurrentHighestBetAmount == 0)
        {
            // If no bet has been made, the round can conclude if everyone has had a chance to act and chosen to check.
            return AreAllActivePlayersChecked();
        }

        // Check if all active players have their bets equal to the highest current bet
        if (!BetManager.Instance.AreAllActivePlayersBetsEqual())
        {
            return false;
        }

        // Check if the last player to raise has had other players act after them
        return AreAllActivePlayersChecked();
    }

    private void ResetTurnStatus()
    {
        var activePlayers = GameManager.Instance.ActivePlayers;
        foreach (var player in activePlayers)
        {
            player.ResetTurnStatus();
        }
    }

    private bool AreAllActivePlayersChecked()
    {
        foreach (var player in GameManager.Instance.ActivePlayers)
        {
            if (player.IsPlayerActive && player.HasActedSinceLastRaise == false)
            {
                if (player.IsPlayerAllIn)
                {
                    continue;
                }
                return false;
            }
        }
        return true;
    }

    private void OnDestroy()
    {
        GameManager.OnGameStateChanged -= GameManager_OnGameStateChanged;
        Instance = null;
    }
}