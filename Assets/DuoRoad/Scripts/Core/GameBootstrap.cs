using UnityEngine;
namespace DuoRoad
{

// Builds the whole game scene from code. Add this single script to an empty GameObject
// in the scene, press Play, and you get the road, the car, the gun, the traffic, the enemies
// (behind and in front), their bombs, the potions, the zones (desert, beach, rain), the controls and the HUD.
public class GameBootstrap : MonoBehaviour
{
    CarController car;
    Camera cam;
    Light sun;
    float camX;

    static readonly Color Sky = new Color(0.53f, 0.81f, 0.92f);

    // Camera is pulled back and up so the enemies chasing from BEHIND are visible too.
    // Tweak these two lines if you want a different view.
    static readonly Vector3 CamOffset = new Vector3(0f, 7f, -16f);
    const float CamPitch = 20f;

    void Awake()
    {
        Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        SetupCameraAndLight();

        var road = new GameObject("Road").AddComponent<RoadScroller>();
        var input = gameObject.AddComponent<TouchInput>();
        input.voice = gameObject.AddComponent<VoiceInput>();   // rounds 4 and 5: brake / shoot / bomb by voice

        var carTransform = CarBuilder.Build("PlayerCar", new Color(0.85f, 0.12f, 0.12f));
        car = carTransform.gameObject.AddComponent<CarController>();
        car.road = road;
        car.controls = input;

        // The gun is added BEFORE the health so the turret also blinks when the car is hit
        var gun = carTransform.gameObject.AddComponent<GunController>();
        gun.controls = input;

        var health = carTransform.gameObject.AddComponent<PlayerHealth>();

        var traffic = new GameObject("Traffic").AddComponent<ObstacleSpawner>();
        traffic.player = car;
        traffic.health = health;
        traffic.road = road;

        // Enemy cars + the bombs they throw
        var bombs = new GameObject("Bombs").AddComponent<BombLauncher>();
        bombs.player = car;
        bombs.health = health;

        var enemies = new GameObject("Enemies").AddComponent<EnemySpawner>();
        enemies.player = car;
        enemies.bombs = bombs;
        gun.enemies = enemies;

        var game = gameObject.AddComponent<GameManager>();
        game.car = car;
        game.health = health;
        game.spawner = traffic;
        game.enemies = enemies;
        game.gun = gun;
        game.touch = input;
        enemies.game = game;
        bombs.game = game;

        var abilities = gameObject.AddComponent<PlayerAbilities>();
        abilities.controls = input;
        abilities.enemies = enemies;
        abilities.traffic = traffic;
        abilities.car = car;
        abilities.game = game;

        var pickups = new GameObject("Pickups").AddComponent<PickupSpawner>();
        pickups.player = car;
        pickups.health = health;
        pickups.road = road;
        pickups.traffic = traffic;
        pickups.abilities = abilities;
        pickups.game = game;

        game.abilities = abilities;
        game.pickups = pickups;

        // Zones: the road look, sky, fog, sun and rain change with the distance driven
        var zones = new GameObject("Zones").AddComponent<ZoneManager>();
        zones.car = car;
        zones.game = game;
        zones.road = road;
        zones.cam = cam;
        zones.sun = sun;
        road.zones = zones;
        game.zones = zones;

        var hud = gameObject.AddComponent<HUD>();
        hud.car = car;
        hud.health = health;
        hud.game = game;
    }

    void SetupCameraAndLight()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }

        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Sky;
        cam.fieldOfView = 60f;
        cam.farClipPlane = 400f;
        cam.transform.position = CamOffset;
        cam.transform.rotation = Quaternion.Euler(CamPitch, 0f, 0f);

        sun = FindFirstObjectByType<Light>();
        if (sun == null)
        {
            sun = new GameObject("Sun").AddComponent<Light>();
        }
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Fog hides the far end of the road, so segment recycling is invisible
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Sky;
        RenderSettings.fogStartDistance = 90f;
        RenderSettings.fogEndDistance = 250f;
    }

    void LateUpdate()
    {
        if (cam == null || car == null) return;

        camX = Mathf.Lerp(camX, car.transform.position.x * 0.6f, 5f * Time.deltaTime);
        var pos = new Vector3(camX, CamOffset.y, CamOffset.z);
        if (car.Shake > 0f) pos += Random.insideUnitSphere * car.Shake * 0.5f;
        cam.transform.position = pos;
    }
}
}
