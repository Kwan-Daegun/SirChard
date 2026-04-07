using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerData
{
    public string playerName;
    public GameObject playerObject;
    [HideInInspector] public float score;
}

public class PlayerManager : MonoBehaviour
{
    public EnergyBall energyBall;
    public float pointsPerSecond = 10f;
    [Range(0f, 1f)] public float deathPenaltyPercent = 0.25f;

    public List<PlayerData> players = new List<PlayerData>();

    private void Start()
    {
        // ++ ADDED: Send custom names to the GameManager right when the game starts
        if (GameManager.Instance != null)
        {
            for (int i = 0; i < players.Count; i++)
            {
                // i + 1 ensures we pass Player 1 (index 1), Player 2 (index 2), etc.
                GameManager.Instance.SetPlayerName(i + 1, players[i].playerName);
            }
        }
    }

    private void Update()
    {
        if (energyBall == null) return;

        GameObject currentOwner = energyBall.currentOwner;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].playerObject == currentOwner)
            {
                players[i].score += pointsPerSecond * Time.deltaTime;
            }

            // Prevent score from dropping below zero
            players[i].score = Mathf.Max(players[i].score, 0f);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetPlayerScore(i + 1, players[i].score);
            }
        }
    }

    public void ApplyDeathPenalty(GameObject playerObject)
    {
        if (playerObject == null) return;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].playerObject != playerObject) continue;

            float penalty = players[i].score * Mathf.Clamp01(deathPenaltyPercent);
            players[i].score = Mathf.Max(players[i].score - penalty, 0f);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetPlayerScore(i + 1, players[i].score);
            }

            break;
        }
    }
}
