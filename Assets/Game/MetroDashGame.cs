using System.Collections.Generic;
using UnityEngine;

// Independent 3D runner. World advances toward a fixed runner; all collision and
// spawning use metres, never screen rectangles. No third-party game assets.
public sealed class MetroDashGame : MonoBehaviour
{
    private sealed class RoadObject
    {
        public int lane, kind; // 0 coin, 1 train, 2 hurdle, 3 overhead, 4 electrified gap
        public float z, length;
        public GameObject view;
        public bool coin => kind == 0;
        public float y => z; // read-only legacy probe compatibility
    }
    private readonly List<RoadObject> objects = new List<RoadObject>();
    private readonly List<Transform> tiles = new List<Transform>();
    private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    private int lane = 1, coins, best, selectedLevel, unlockedLevel, currentLevel, runCoins;
    private float distance, elapsed, spawnTimer, runnerX, height, verticalSpeed, slideTimer, speed, bannerTimer;
    private bool playing, started, gameOver, inputHeld;
    private Vector2 touchStart;
    private Transform runner, torso, head, leftArm, rightArm, leftLeg, rightLeg;
    private Camera gameCamera;
    private readonly List<GameObject> canalWater = new List<GameObject>();
    private GUIStyle title, label, small, button, giant;
    private Texture2D panelTexture;
    private static readonly string[] Districts = { "SUNRISE YARD", "CANAL QUARTER", "NIGHT EXPRESS" };
    private const float LaneWidth = 2.65f, DistrictLength = 600f;
    public int Score => Mathf.FloorToInt(distance);
    public int Coins => coins;
    public bool IsPlaying => playing;
    public bool IsJumping => height > .05f;
    public bool IsSliding => slideTimer > 0;
    public float PlayerHeight => height;
    public float Speed => speed;
    public int Level => currentLevel;
    public int UnlockedLevel => unlockedLevel;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        Input.simulateMouseWithTouches = false;
        coins = PlayerPrefs.GetInt("metroCoins", 0);
        best = PlayerPrefs.GetInt("metroBest", 0);
        unlockedLevel = Mathf.Clamp(PlayerPrefs.GetInt("metroDistrict", 0), 0, 2);
        BuildWorld();
    }
    private Material Mat(string name, Color color, bool glow = false)
    {
        if (materials.TryGetValue(name, out var cached)) return cached;
        var material = new Material(Resources.Load<Material>("RunnerBase"));
        material.color = color;
        material.SetFloat("_Glossiness", .2f);
        if (glow) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * .6f); }
        materials.Add(name, material); return material;
    }
    private GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 size, Material mat)
    {
        var obj = GameObject.CreatePrimitive(type); obj.name = name;
        obj.transform.SetParent(parent, false); obj.transform.localPosition = pos; obj.transform.localScale = size;
        obj.GetComponent<Renderer>().sharedMaterial = mat;
        Destroy(obj.GetComponent<Collider>()); return obj;
    }
    private GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        => Shape(name, PrimitiveType.Cube, parent, pos, size, mat);
    private void BuildWorld()
    {
        RenderSettings.ambientLight = new Color(.62f, .69f, .8f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.82f, .78f, .73f);
        RenderSettings.fogStartDistance = 65; RenderSettings.fogEndDistance = 155;
        var cameraObject = new GameObject("Runner chase camera"); cameraObject.tag = "MainCamera";
        gameCamera = cameraObject.AddComponent<Camera>(); gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = new Color(.77f, .83f, .9f); gameCamera.fieldOfView = 62;
        gameCamera.nearClipPlane = .1f; gameCamera.farClipPlane = 180;
        gameCamera.transform.position = new Vector3(0, 4.8f, -8.5f);
        gameCamera.transform.LookAt(new Vector3(0, 1.3f, 13));
        var sun = new GameObject("Warm morning sun").AddComponent<Light>(); sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(43, -28, 0); sun.color = new Color(1, .86f, .69f); sun.intensity = 1.2f;
        sun.shadows = LightShadows.Soft; QualitySettings.shadowDistance = 45;
        var ground = Mat("ballast", new Color(.28f, .33f, .39f));
        var platform = Mat("platform", new Color(.66f, .69f, .68f));
        var rail = Mat("rail", new Color(.59f, .67f, .73f));
        var wood = Mat("sleepers", new Color(.24f, .27f, .3f));
        var edge = Mat("platform edge", new Color(1, .7f, .2f));
        var glass = Mat("building glass", new Color(.24f, .49f, .6f));
        var cyan = Mat("neon", new Color(.14f, .92f, .92f), true);
        for (int i = 0; i < 13; i++)
        {
            Transform tile = new GameObject("Track segment " + i).transform; tile.position = new Vector3(0, 0, -14 + i * 14);
            tiles.Add(tile);
            Box("Track bed", tile, new Vector3(0, -.25f, 0), new Vector3(10, .4f, 14), ground);
            for (int l = 0; l < 3; l++)
            {
                float x = (l - 1) * LaneWidth;
                for (int k = 0; k < 2; k++) Box("Steel rail", tile, new Vector3(x + (k == 0 ? -.68f : .68f), .03f, 0), new Vector3(.1f, .12f, 14), rail);
                for (int z = -6; z <= 6; z += 2) Box("Rail sleeper", tile, new Vector3(x, -.02f, z), new Vector3(1.85f, .1f, .24f), wood);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Raised sidewalk", tile, new Vector3(side * 6, -.05f, 0), new Vector3(2, .6f, 14), platform);
                var water = Box("Canal water", tile, new Vector3(side * 7.3f, -.05f, 0), new Vector3(.6f, .15f, 14), Mat("water", new Color(.12f, .64f, .76f)));
                water.SetActive(false); canalWater.Add(water);
                Box("Safety edge", tile, new Vector3(side * 4.95f, .28f, 0), new Vector3(.14f, .08f, 14), edge);
                for (int b = 0; b < 2; b++)
                {
                    float h = 5 + (i * 7 + b * 3 + (side + 1)) % 9;
                    var facade = Mat("facade" + i + side + b, Color.Lerp(new Color(.65f, .47f, .4f), new Color(.34f, .55f, .64f), ((i + b * 3) % 7) / 6f));
                    Box("City block", tile, new Vector3(side * (10 + b), h / 2, -3.5f + b * 7), new Vector3(5, h, 5.8f), facade);
                    Box("Roof cap", tile, new Vector3(side * (10 + b), h + .15f, -3.5f + b * 7), new Vector3(5.3f, .3f, 6.1f), platform);
                    for (int floor = 1; floor < h - 1; floor += 2)
                        Box("Window ribbon", tile, new Vector3(side * (7.48f + b), floor, -3.5f + b * 7), new Vector3(.06f, .9f, 4.5f), glass);
                }
                Box("Light column", tile, new Vector3(side * 5.8f, 2.4f, 0), new Vector3(.16f, 4.4f, .16f), rail);
                Box("Light strip", tile, new Vector3(side * 5.8f, 4.6f, 0), new Vector3(.55f, .15f, .5f), cyan);
            }
            if (i % 3 == 0)
            {
                Box("Station gantry", tile, new Vector3(0, 6, 0), new Vector3(12, .3f, .45f), rail);
                Box("District marker", tile, new Vector3(0, 5.6f, 0), new Vector3(3.3f, .65f, .14f), cyan);
            }
        }
        BuildRunner();
    }
    private Transform Limb(string name, Vector3 pivot, Vector3 size, Material mat)
    {
        var root = new GameObject(name).transform; root.SetParent(runner, false); root.localPosition = pivot;
        Box(name + " mesh", root, new Vector3(0, -size.y / 2, 0), size, mat); return root;
    }
    private void BuildRunner()
    {
        runner = new GameObject("Ari - metro courier").transform;
        var jacket = Mat("courier teal", new Color(.06f, .73f, .72f));
        var trousers = Mat("ink trousers", new Color(.1f, .16f, .29f));
        var skin = Mat("skin", new Color(.72f, .43f, .27f));
        var orange = Mat("saffron", new Color(1, .5f, .1f));
        torso = Box("Courier jacket", runner, new Vector3(0, 1.05f, 0), new Vector3(.61f, .65f, .38f), jacket).transform;
        head = Shape("Head", PrimitiveType.Sphere, runner, new Vector3(0, 1.65f, 0), new Vector3(.44f, .47f, .44f), skin).transform;
        Shape("Helmet", PrimitiveType.Sphere, head, new Vector3(0, .3f, -.02f), new Vector3(1.08f, .75f, 1.08f), orange);
        Box("Helmet stripe", head, new Vector3(0, .58f, -.05f), new Vector3(.18f, .08f, .95f), jacket);
        Box("Courier pack", torso, new Vector3(0, 0, -.66f), new Vector3(.7f, .75f, .55f), orange);
        Box("Reflector", torso, new Vector3(0, 0, -.96f), new Vector3(.52f, .13f, .04f), Mat("reflector", Color.white));
        leftArm = Limb("Left arm", new Vector3(-.39f, 1.3f, 0), new Vector3(.19f, .56f, .23f), jacket);
        rightArm = Limb("Right arm", new Vector3(.39f, 1.3f, 0), new Vector3(.19f, .56f, .23f), jacket);
        leftLeg = Limb("Left leg", new Vector3(-.17f, .75f, 0), new Vector3(.22f, .69f, .25f), trousers);
        rightLeg = Limb("Right leg", new Vector3(.17f, .75f, 0), new Vector3(.22f, .69f, .25f), trousers);
        var shoe = Mat("shoe", new Color(.94f, .94f, .86f));
        Box("Left trainer", leftLeg, new Vector3(0, -.63f, .08f), new Vector3(.26f, .16f, .46f), shoe);
        Box("Right trainer", rightLeg, new Vector3(0, -.63f, .08f), new Vector3(.26f, .16f, .46f), shoe);
    }
    public void StartRun()
    {
        foreach (var o in objects) Destroy(o.view); objects.Clear();
        lane = 1; runnerX = height = verticalSpeed = slideTimer = elapsed = 0;
        distance = selectedLevel * DistrictLength; currentLevel = selectedLevel; spawnTimer = 1.4f;
        runCoins = 0; bannerTimer = 3; gameOver = false; started = playing = true;
        for (int i = 0; i < tiles.Count; i++) tiles[i].position = new Vector3(0, 0, -14 + i * 14);
    }
    public void Move(int direction) { if (playing) lane = Mathf.Clamp(lane + direction, 0, 2); }
    public void Jump()
    {
        if (!playing || height > .05f) return;
        slideTimer = 0; verticalSpeed = 9.5f;
    }
    public void Slide() { if (playing && height < .05f) slideTimer = .95f; else if (playing) verticalSpeed = -13; }
    private void ReadInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Move(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Move(1);
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Space)) Jump();
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Slide();
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began) { touchStart = t.position; inputHeld = true; }
            if (t.phase == TouchPhase.Moved && inputHeld) Gesture(t.position);
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) inputHeld = false;
        }
        else
        {
            if (Input.GetMouseButtonDown(0)) { touchStart = Input.mousePosition; inputHeld = true; }
            if (Input.GetMouseButton(0) && inputHeld) Gesture(Input.mousePosition);
            if (Input.GetMouseButtonUp(0)) inputHeld = false;
        }
    }
    private void Gesture(Vector2 position)
    {
        Vector2 delta = position - touchStart;
        if (delta.magnitude < Mathf.Min(Screen.width, Screen.height) * .075f) return;
        inputHeld = false;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) Move(delta.x > 0 ? 1 : -1);
        else if (delta.y > 0) Jump(); else Slide();
    }
    private void Update()
    {
        if (!playing) return;
        ReadInput(); float dt = Mathf.Min(Time.deltaTime, .05f);
        elapsed += dt; speed = Mathf.Min(23, 11.5f + distance * .004f); distance += speed * dt;
        int district = Mathf.Min(2, Mathf.FloorToInt(distance / DistrictLength));
        if (district != currentLevel) { currentLevel = district; unlockedLevel = Mathf.Max(unlockedLevel, district); bannerTimer = 3; Save(); }
        bannerTimer = Mathf.Max(0, bannerTimer - dt);
        runnerX = Mathf.MoveTowards(runnerX, (lane - 1) * LaneWidth, dt * 19);
        if (height > 0 || verticalSpeed > 0) { verticalSpeed -= 22 * dt; height = Mathf.Max(0, height + verticalSpeed * dt); }
        if (height == 0 && verticalSpeed < 0) verticalSpeed = 0;
        slideTimer = Mathf.Max(0, slideTimer - dt);
        AnimateRunner(); UpdateDistrict();
        foreach (var tile in tiles)
        {
            tile.position -= Vector3.forward * (speed * dt);
            if (tile.position.z < -28) tile.position += Vector3.forward * (tiles.Count * 14);
        }
        spawnTimer -= dt;
        if (spawnTimer <= 0) { SpawnRow(); spawnTimer = 2.25f; }
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            var o = objects[i]; o.z -= speed * dt;
            o.view.transform.position = new Vector3((o.lane - 1) * LaneWidth, 0, o.z);
            if (o.coin) o.view.transform.Rotate(0, dt * 150, 0);
            if (o.z < -12) { Destroy(o.view); objects.RemoveAt(i); continue; }
            if (Mathf.Abs(o.z) > o.length / 2 + .3f || Mathf.Abs(runnerX - (o.lane - 1) * LaneWidth) > (o.coin ? .7f : 1f)) continue;
            if (o.coin)
            {
                if (height > 1.6f) continue;
                coins++; runCoins++; Destroy(o.view); objects.RemoveAt(i); Save();
            }
            else if (o.kind == 1 || (o.kind == 2 && height < 1.05f) || (o.kind == 3 && !IsSliding) || (o.kind == 4 && height < .6f))
            { EndRun(); break; }
        }
    }
    private void AnimateRunner()
    {
        runner.position = new Vector3(runnerX, height, 0);
        runner.localScale = new Vector3(1, IsSliding ? .43f : 1, IsSliding ? 1.35f : 1);
        runner.rotation = Quaternion.Euler(0, ((lane - 1) * LaneWidth - runnerX) * 7, 0);
        float step = Mathf.Sin(elapsed * 16) * 35;
        leftLeg.localRotation = Quaternion.Euler(IsJumping ? -32 : step, 0, 0);
        rightLeg.localRotation = Quaternion.Euler(IsJumping ? 25 : -step, 0, 0);
        leftArm.localRotation = Quaternion.Euler(-step, 0, -10); rightArm.localRotation = Quaternion.Euler(step, 0, 10);
        Vector3 cp = gameCamera.transform.position; cp.x = Mathf.Lerp(cp.x, runnerX * .17f, Time.deltaTime * 4); gameCamera.transform.position = cp;
    }
    private void UpdateDistrict()
    {
        Color sky = currentLevel == 0 ? new Color(.77f, .83f, .9f) : currentLevel == 1 ? new Color(.5f, .76f, .83f) : new Color(.1f, .15f, .3f);
        gameCamera.backgroundColor = Color.Lerp(gameCamera.backgroundColor, sky, Time.deltaTime);
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, sky, Time.deltaTime);
        RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, currentLevel == 2 ? new Color(.34f, .4f, .6f) : new Color(.62f, .69f, .8f), Time.deltaTime);
        foreach (var water in canalWater) if (water.activeSelf != (currentLevel == 1)) water.SetActive(currentLevel == 1);
    }
    private void SpawnRow()
    {
        int safe = Random.Range(0, 3); // Every row retains a clear ground-level escape lane.
        int difficulty = currentLevel;
        for (int l = 0; l < 3; l++)
        {
            if (l == safe)
            {
                for (int c = 0; c < 6; c++) Spawn(l, 0, 60 + c * 2.2f);
                continue;
            }
            int kind = Random.Range(1, difficulty > 0 ? 5 : 4);
            Spawn(l, kind, 70);
            if (kind == 2) for (int c = 0; c < 3; c++) Spawn(l, 0, 75 + c * 2.2f);
        }
    }
    private void Spawn(int l, int kind, float z)
    {
        var root = new GameObject(kind == 0 ? "Gold token" : kind == 1 ? "Metro train" : kind == 2 ? "Jump hurdle" : kind == 3 ? "Slide gate" : "Live track");
        root.transform.position = new Vector3((l - 1) * LaneWidth, 0, z);
        var yellow = Mat("gold", new Color(1, .72f, .12f), true);
        var warning = Mat("coral", new Color(.96f, .3f, .2f));
        var dark = Mat("train window", new Color(.08f, .2f, .28f));
        float length = 1;
        if (kind == 0)
        {
            var disk = Shape("Token", PrimitiveType.Cylinder, root.transform, new Vector3(0, 1, 0), new Vector3(.55f, .09f, .55f), yellow);
            disk.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Shape("Token inset", PrimitiveType.Sphere, root.transform, new Vector3(0, 1, -.1f), new Vector3(.23f, .23f, .05f), Mat("token light", new Color(1, .92f, .5f)));
        }
        else if (kind == 1)
        {
            length = 8;
            var train = Mat("train teal", new Color(.1f, .61f, .67f));
            Box("Carriage", root.transform, new Vector3(0, 1.55f, 0), new Vector3(2.12f, 2.7f, 8), train);
            Box("Roof", root.transform, new Vector3(0, 2.95f, 0), new Vector3(2.2f, .2f, 8), Mat("train roof", new Color(.81f, .85f, .84f)));
            Box("Windscreen", root.transform, new Vector3(0, 2.05f, -4.02f), new Vector3(1.7f, .85f, .04f), dark);
            Box("Safety bumper", root.transform, new Vector3(0, .6f, -4.1f), new Vector3(2.15f, .25f, .2f), warning);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Headlight", root.transform, new Vector3(side * .76f, 1.1f, -4.06f), new Vector3(.28f, .18f, .05f), yellow);
                for (int w = 0; w < 4; w++) Box("Passenger window", root.transform, new Vector3(side * 1.075f, 2, -2.8f + w * 1.85f), new Vector3(.04f, .9f, 1.1f), dark);
            }
        }
        else if (kind == 2)
        {
            Box("Hurdle", root.transform, new Vector3(0, .55f, 0), new Vector3(2, 1.1f, .65f), warning);
            for (int s = -1; s <= 1; s++) Box("Reflective stripes", root.transform, new Vector3(s * .62f, .65f, -.34f), new Vector3(.23f, .7f, .03f), yellow);
        }
        else if (kind == 3)
        {
            for (int side = -1; side <= 1; side += 2) Box("Gate pillar", root.transform, new Vector3(side * 1.05f, 1.5f, 0), new Vector3(.15f, 3, .3f), warning);
            Box("Low clearance", root.transform, new Vector3(0, 2.05f, 0), new Vector3(2.2f, 1.65f, .7f), yellow);
            Box("Clearance stripe", root.transform, new Vector3(0, 1.36f, -.36f), new Vector3(2.2f, .16f, .04f), dark);
        }
        else
        {
            length = 2;
            Box("Electrified track", root.transform, new Vector3(0, .02f, 0), new Vector3(2.2f, .12f, 2), dark);
            for (int i = -2; i <= 2; i++) Box("Live warning", root.transform, new Vector3(0, .1f, i * .4f), new Vector3(2.1f, .07f, .12f), Mat("electric cyan", new Color(.1f, .95f, 1), true));
        }
        objects.Add(new RoadObject { lane = l, kind = kind, z = z, length = length, view = root });
    }
    private void EndRun() { playing = false; gameOver = true; best = Mathf.Max(best, Score); Save(); }
    private void Save()
    {
        PlayerPrefs.SetInt("metroCoins", coins); PlayerPrefs.SetInt("metroBest", Mathf.Max(best, Score));
        PlayerPrefs.SetInt("metroDistrict", unlockedLevel); PlayerPrefs.Save();
    }
    private void Pause() { playing = false; inputHeld = false; best = Mathf.Max(best, Score); Save(); }
    private void OnApplicationPause(bool pause) { if (pause) { if (playing) Pause(); else Save(); } }
    private void OnApplicationQuit() { Save(); }
    private void Styles()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        title.normal.textColor = Color.white;
        giant = new GUIStyle(title) { fontSize = 48 };
        label = new GUIStyle(title) { fontSize = 20 };
        small = new GUIStyle(label) { fontSize = 14, fontStyle = FontStyle.Normal, wordWrap = true };
        button = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        panelTexture = new Texture2D(1, 1); panelTexture.SetPixel(0, 0, Color.white); panelTexture.Apply();
        button.normal.background = panelTexture; button.hover.background = panelTexture; button.active.background = panelTexture;
        button.normal.textColor = button.hover.textColor = button.active.textColor = new Color(.04f, .12f, .18f);
    }
    private void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }
    private bool Button(Rect r, string text, bool primary = true)
    {
        GUI.backgroundColor = primary ? new Color(.25f, .95f, .85f) : new Color(.86f, .91f, .94f);
        bool result = GUI.Button(r, text, button); GUI.backgroundColor = Color.white; return result;
    }
    private void OnGUI()
    {
        Styles(); Rect safe = Screen.safeArea; float scale = Mathf.Min(safe.width / 430f, safe.height / 932f);
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x + (safe.width - 430 * scale) / 2, Screen.height - safe.yMax + (safe.height - 932 * scale) / 2, 0), Quaternion.identity, new Vector3(scale, scale, 1));
        Fill(new Rect(16, 12, 398, 85), new Color(.025f, .07f, .14f, .92f));
        GUI.Label(new Rect(28, 18, 145, 24), "METRO / DASH", small);
        GUI.Label(new Rect(28, 43, 144, 39), Score + " m", title);
        GUI.Label(new Rect(181, 18, 130, 24), "GOLD TOKENS", small);
        GUI.Label(new Rect(181, 43, 130, 39), coins.ToString(), title);
        if (playing && Button(new Rect(330, 29, 64, 50), "II", false)) Pause();
        Fill(new Rect(16, 104, 398, 34), new Color(.025f, .07f, .14f, .85f));
        GUI.Label(new Rect(20, 104, 390, 28), "0" + (currentLevel + 1) + " / " + Districts[currentLevel], small);
        Fill(new Rect(16, 136, 398 * Mathf.Repeat(distance, DistrictLength) / DistrictLength, 3), new Color(.25f, .95f, .85f));
        if (playing)
        {
            if (bannerTimer > 0)
            {
                Fill(new Rect(45, 190, 340, 75), new Color(.025f, .07f, .14f, .9f));
                GUI.Label(new Rect(45, 198, 340, 30), Districts[currentLevel], title);
                GUI.Label(new Rect(45, 232, 340, 22), "SWIPE TO DODGE / UP JUMP / DOWN SLIDE", small);
            }
            if (Button(new Rect(16, 850, 86, 62), "LEFT", false)) Move(-1);
            if (Button(new Rect(110, 850, 101, 62), "JUMP")) Jump();
            if (Button(new Rect(219, 850, 101, 62), "SLIDE")) Slide();
            if (Button(new Rect(328, 850, 86, 62), "RIGHT", false)) Move(1);
            GUI.Label(new Rect(45, 814, 340, 25), IsSliding ? "SLIDING" : IsJumping ? "JUMPING" : "SWIPE ANYWHERE TO MOVE", small);
        }
        else
        {
            Fill(new Rect(22, 280, 386, 508), new Color(.025f, .07f, .14f, .96f));
            Fill(new Rect(22, 280, 386, 5), new Color(.25f, .95f, .85f));
            GUI.Label(new Rect(36, 300, 358, 34), gameOver ? "RUN COMPLETE" : started ? "TAKE A BREATHER" : "THE CITY IS YOURS", title);
            GUI.Label(new Rect(36, 342, 358, 67), gameOver ? Score + " m" : "METRO DASH", giant);
            GUI.Label(new Rect(42, 420, 346, 62), gameOver ? runCoins + " tokens collected  /  Best " + best + " m" : "Three tracks. One way forward.\nDodge trains, jump hurdles, slide under gates.", small);
            if (Button(new Rect(57, 506, 316, 66), gameOver ? "RUN AGAIN" : started ? "RESUME RUN" : "START RUN"))
            { if (started && !gameOver) playing = true; else StartRun(); }
            if (!started || gameOver)
            {
                GUI.Label(new Rect(42, 594, 346, 28), "CHOOSE YOUR STARTING DISTRICT", small);
                for (int i = 0; i < 3; i++)
                {
                    GUI.enabled = i <= unlockedLevel;
                    if (Button(new Rect(44 + i * 116, 634, 110, 58), i > unlockedLevel ? "LOCKED" : (i == selectedLevel ? "• " : "") + "0" + (i + 1), i == selectedLevel)) selectedLevel = i;
                    GUI.enabled = true;
                }
                GUI.Label(new Rect(42, 704, 346, 45), "New districts unlock every 600 m.\n" + Districts[selectedLevel], small);
            }
            else if (Button(new Rect(57, 600, 316, 55), "END RUN", false)) EndRun();
            GUI.Label(new Rect(42, 752, 346, 24), "ORIGINAL COURIER / ENDLESS CITY", small);
        }
        GUI.matrix = old;
    }
}
