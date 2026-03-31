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
        if (energyBall == null || energyBall.currentOwner == null) return;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].playerObject == energyBall.currentOwner)
            {
                players[i].score += pointsPerSecond * Time.deltaTime;

                if (GameManager.Instance != null)
                {
                    GameManager.Instance.SetPlayerScore(i + 1, players[i].score);
                }

                break;
            }
        }
    }
}