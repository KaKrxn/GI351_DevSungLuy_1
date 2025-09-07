// ScoreUI.cs
using UnityEngine;
using TMPro;

public class ScoreUI : MonoBehaviour
{
    public DeliveryPointManager manager;
    public TMP_Text scoreText;

    void Start()
    {
        if (manager) manager.onScoreChanged.AddListener(UpdateScore);
        UpdateScore(manager ? manager.score : 0);
    }

    void UpdateScore(int s) => scoreText.text = s.ToString();
}
