using System.Collections.Generic;
using UnityEngine;

public sealed class MetroDashGame : MonoBehaviour
{
    private sealed class Obstacle
    {
        public int lane;
        public float y;
        public bool coin;
    }

    private readonly List<Obstacle> obstacles = new List<Obstacle>();
    private int playerLane = 1;
    private int score;
    private int coins;
    private float speed = 260f;
    private float spawnTimer;
    private Vector2 touchStart;
    private bool touching;
    private GUIStyle titleStyle;
    private GUIStyle scoreStyle;
    private GUIStyle buttonStyle;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        coins = PlayerPrefs.GetInt("metroCoins", 0);
        for (int i = 0; i < 5; i++) Spawn(i * -180f);
    }

    private void Update()
    {
        score += Mathf.RoundToInt(Time.deltaTime * 10f);
        speed = Mathf.Min(520f, 260f + score * 0.12f);
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            Spawn(-120f);
            spawnTimer = Mathf.Max(0.45f, 1.05f - score * 0.0006f);
        }

        foreach (Obstacle obstacle in obstacles) obstacle.y += speed * Time.deltaTime;
        obstacles.RemoveAll(o => o.y > Screen.height + 100f);
        HandleTouch();
    }

    private void HandleTouch()
    {
        if (Input.touchCount == 0)
        {
            touching = false;
            return;
        }

        Touch touch = Input.GetTouch(0);
        if (touch.phase == TouchPhase.Began)
        {
            touchStart = touch.position;
            touching = true;
        }
        else if (touching && (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled))
        {
            float delta = touch.position.x - touchStart.x;
            if (Mathf.Abs(delta) > 35f) Move(delta > 0f ? 1 : -1);
            touching = false;
        }
    }

    private void Spawn(float y)
    {
        obstacles.Add(new Obstacle
        {
            lane = Random.Range(0, 3),
            y = y,
            coin = Random.value < 0.38f
        });
    }

    private void Move(int direction)
    {
        playerLane = Mathf.Clamp(playerLane + direction, 0, 2);
    }

    private void InitStyles(float scale)
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(30 * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        titleStyle.normal.textColor = new Color(0.2f, 0.95f, 1f);
        scoreStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(20 * scale), fontStyle = FontStyle.Bold };
        scoreStyle.normal.textColor = Color.white;
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(30 * scale), fontStyle = FontStyle.Bold };
        buttonStyle.normal.textColor = Color.white;
    }

    private void OnGUI()
    {
        float scale = Mathf.Min(Screen.width / 430f, Screen.height / 932f);
        InitStyles(scale);
        Color old = GUI.color;
        GUI.color = new Color(0.025f, 0.055f, 0.1f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        float roadWidth = Screen.width * 0.76f;
        float roadLeft = (Screen.width - roadWidth) * 0.5f;
        GUI.color = new Color(0.08f, 0.12f, 0.18f);
        GUI.DrawTexture(new Rect(roadLeft, 0, roadWidth, Screen.height), Texture2D.whiteTexture);
        GUI.color = new Color(0.16f, 0.27f, 0.38f);
        float laneWidth = roadWidth / 3f;
        GUI.DrawTexture(new Rect(roadLeft + laneWidth - 2, 0, 4, Screen.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(roadLeft + laneWidth * 2 - 2, 0, 4, Screen.height), Texture2D.whiteTexture);

        Rect player = LaneRect(playerLane, Screen.height - 190f * scale, 60f * scale, 92f * scale, roadLeft, laneWidth);
        GUI.color = new Color(1f, 0.46f, 0.08f);
        GUI.DrawTexture(player, Texture2D.whiteTexture);

        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            Obstacle obstacle = obstacles[i];
            float size = obstacle.coin ? 38f * scale : 70f * scale;
            Rect rect = LaneRect(obstacle.lane, obstacle.y, size, obstacle.coin ? size : 96f * scale, roadLeft, laneWidth);
            GUI.color = obstacle.coin ? new Color(1f, 0.82f, 0.15f) : new Color(0.88f, 0.12f, 0.2f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            if (rect.Overlaps(player))
            {
                if (obstacle.coin)
                {
                    coins++;
                    PlayerPrefs.SetInt("metroCoins", coins);
                    PlayerPrefs.Save();
                    obstacles.RemoveAt(i);
                }
                else
                {
                    score = Mathf.Max(0, score - 250);
                    speed = 260f;
                    obstacle.y = -180f;
                    obstacle.lane = Random.Range(0, 3);
                }
            }
        }

        GUI.color = old;
        GUI.Label(new Rect(0, 24f * scale, Screen.width, 60f * scale), "METRO DASH", titleStyle);
        GUI.Label(new Rect(18f * scale, 80f * scale, 180f * scale, 40f * scale), "SCORE  " + score, scoreStyle);
        GUI.Label(new Rect(Screen.width - 150f * scale, 80f * scale, 135f * scale, 40f * scale), "COINS  " + coins, scoreStyle);

        if (GUI.Button(new Rect(24f * scale, Screen.height - 100f * scale, 92f * scale, 68f * scale), "‹", buttonStyle)) Move(-1);
        if (GUI.Button(new Rect(Screen.width - 116f * scale, Screen.height - 100f * scale, 92f * scale, 68f * scale), "›", buttonStyle)) Move(1);
    }

    private static Rect LaneRect(int lane, float y, float width, float height, float roadLeft, float laneWidth)
    {
        float x = roadLeft + laneWidth * lane + (laneWidth - width) * 0.5f;
        return new Rect(x, y, width, height);
    }
}
