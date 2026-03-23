using System.Collections.Generic;
using UnityEngine;
using TMPro;

[System.Serializable]
public class PlayerData
{
    public string playerName;
    public GameObject playerObject;
    public TextMeshProUGUI scoreText;
    [HideInInspector] public float score;
}

public class PlayerManager : MonoBehaviour
{
    public EnergyBall energyBall;
    public float pointsPerSecond = 10f;

    public List<PlayerData> players = new List<PlayerData>();

    private void Update()
    {
        if (energyBall == null || energyBall.currentOwner == null) return;

        foreach (PlayerData p in players)
        {
            if (p.playerObject == energyBall.currentOwner)
            {
                p.score += pointsPerSecond * Time.deltaTime;

                if (p.scoreText != null)
                {
                    p.scoreText.text = p.playerName + ": " + Mathf.FloorToInt(p.score).ToString();
                }
                break;
            }
        }
    }
}