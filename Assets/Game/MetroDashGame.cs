using System.Collections.Generic;
using UnityEngine;

public sealed class MetroDashGame : MonoBehaviour
{
    private sealed class RoadObject { public int lane; public float y; public bool coin; }
    private readonly List<RoadObject> objects = new List<RoadObject>();
    private int lane = 1, coins, best;
    private float elapsed, spawnTimer, distance;
    private bool playing, started, gameOver;
    private Vector2 touchStart;
    private GUIStyle title, label, button, small;
    public int Score => Mathf.FloorToInt(distance);
    public int Coins => coins;
    public bool IsPlaying => playing;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        coins = PlayerPrefs.GetInt("metroCoins", 0);
        best = PlayerPrefs.GetInt("metroBest", 0);
    }
    public void StartRun()
    {
        objects.Clear(); lane = 1; distance = 0; elapsed = 0;
        spawnTimer = 0.7f; gameOver = false; started = playing = true;
    }
    private void Update()
    {
        if (!playing) return;
        float dt = Mathf.Min(Time.deltaTime, 0.1f);
        elapsed += dt; distance += dt * 10f;
        float speed = Mathf.Min(520f, 260f + Score * 0.12f);
        spawnTimer -= dt;
        if (spawnTimer <= 0)
        {
            objects.Add(new RoadObject {lane = Random.Range(0,3), y = -110, coin = Random.value < 0.4f});
            spawnTimer = Mathf.Max(0.6f, 1.1f - Score * 0.0006f);
        }
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            RoadObject item = objects[i]; item.y += speed * dt;
            if (item.y > 1040) { objects.RemoveAt(i); continue; }
            if (ObjectRect(item).Overlaps(PlayerRect()))
            {
                objects.RemoveAt(i);
                if (item.coin) { coins++; Save(); }
                else { playing = false; gameOver = true; best = Mathf.Max(best, Score); Save(); break; }
            }
        }
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began) touchStart = touch.position;
            else if (touch.phase == TouchPhase.Ended)
            {
                float dx = touch.position.x - touchStart.x;
                if (Mathf.Abs(dx) > Screen.width * 0.07f) Move(dx > 0 ? 1 : -1);
            }
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow)) Move(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow)) Move(1);
    }
    public void Move(int direction) { if (playing) lane = Mathf.Clamp(lane + direction,0,2); }
    private Rect PlayerRect() => LaneRect(lane, 735, 60, 90);
    private Rect ObjectRect(RoadObject o) => LaneRect(o.lane,o.y,o.coin ? 32 : 68,o.coin ? 32 : 90);
    private Rect LaneRect(int l,float y,float width,float height) => new Rect(50 + l * 110 + (110-width)/2,y,width,height);
    private void Save() { PlayerPrefs.SetInt("metroCoins",coins); PlayerPrefs.SetInt("metroBest",best); PlayerPrefs.Save(); }
    private void OnApplicationPause(bool pause) { if (pause) { Save(); if (playing) playing = false; } }
    private void OnApplicationQuit() { Save(); }
    private void Styles()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) {fontSize=32,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter}; title.normal.textColor=new Color(.2f,.95f,1);
        label = new GUIStyle(GUI.skin.label) {fontSize=19,alignment=TextAnchor.MiddleCenter}; label.normal.textColor=Color.white;
        small = new GUIStyle(label) {fontSize=16,wordWrap=true};
        button = new GUIStyle(GUI.skin.button) {fontSize=23,fontStyle=FontStyle.Bold};
    }
    private void Fill(Rect rect,Color color) { GUI.color=color; GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=Color.white; }
    private void OnGUI()
    {
        Styles(); Rect safe=Screen.safeArea; float scale=Mathf.Min(safe.width/430f,safe.height/932f);
        Matrix4x4 old=GUI.matrix; Color previous=GUI.color;
        GUI.matrix=Matrix4x4.TRS(new Vector3(safe.x+(safe.width-430*scale)/2,Screen.height-safe.yMax+(safe.height-932*scale)/2,0),Quaternion.identity,new Vector3(scale,scale,1));
        Fill(new Rect(0,0,430,932),new Color(.025f,.055f,.1f));
        Fill(new Rect(48,115,334,730),new Color(.09f,.14f,.2f));
        for(int l=1;l<3;l++) for(int y=120;y<840;y+=100) Fill(new Rect(49+l*110,y+(elapsed*150)%100,2,42),new Color(.2f,.3f,.4f));
        foreach(RoadObject o in objects) Fill(ObjectRect(o),o.coin ? new Color(1,.82f,.15f) : new Color(.88f,.15f,.25f));
        Rect car=PlayerRect(); Fill(car,new Color(.05f,.75f,.9f)); Fill(new Rect(car.x+8,car.y+15,car.width-16,23),new Color(.1f,.2f,.3f));
        GUI.Label(new Rect(0,12,430,54),"METRO DASH",title);
        GUI.Label(new Rect(8,68,200,36),"DISTANCE  "+Score,label); GUI.Label(new Rect(225,68,200,36),"COINS  "+coins,label);
        if(playing)
        {
            if(GUI.Button(new Rect(24,850,100,62),"LEFT",button)) Move(-1);
            if(GUI.Button(new Rect(306,850,100,62),"RIGHT",button)) Move(1);
            if(GUI.Button(new Rect(155,850,120,62),"PAUSE",button)) playing=false;
        }
        else
        {
            Fill(new Rect(30,240,370,440),new Color(.035f,.08f,.14f,.98f));
            GUI.Label(new Rect(45,270,340,60),gameOver ? "RUN COMPLETE" : started ? "PAUSED" : "READY TO RUN?",title);
            GUI.Label(new Rect(55,350,320,90),gameOver ? "Distance: "+Score+"\nBest: "+best : "Swipe left or right, or use the buttons. Collect gold coins. Avoid red barriers.",small);
            if(GUI.Button(new Rect(80,485,270,70),gameOver ? "TRY AGAIN" : started ? "RESUME" : "PLAY",button))
            { if(started && !gameOver) playing=true; else StartRun(); }
            GUI.Label(new Rect(60,580,310,42),"Best distance  "+best,label);
        }
        GUI.matrix=old;GUI.color=previous;
    }
}
