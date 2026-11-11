using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

// Читает JSON комнаты из StreamingAssets/Rooms и собирает её:
// модель комнаты (.glb), свет, объекты (модели .glb, картины, заглушки-фигуры).
public class RoomLoader : MonoBehaviour
{
    [Header("Настройки")]
    public string startRoomId = "hall";
    public Transform player;                 // перетащить сюда Player

    public string CurrentRoomId { get; private set; }

    private Transform roomRoot;              // родитель всех объектов текущей комнаты
    private readonly List<GltfImport> loadedModels = new List<GltfImport>();
    private readonly List<UnityEngine.Object> tempAssets = new List<UnityEngine.Object>(); // текстуры картин
    private AmbientMode defaultAmbientMode;
    private Color defaultAmbientLight;

    void Awake()
    {
        // Запоминаем фон сцены, чтобы возвращать его в комнатах без своего ambientColor
        defaultAmbientMode = RenderSettings.ambientMode;
        defaultAmbientLight = RenderSettings.ambientLight;
    }

    async void Start()
    {
        await LoadRoom(startRoomId);
    }

    // Возвращает true, если комната загружена
    public async Task<bool> LoadRoom(string roomId)
    {
        RoomData data = ReadRoomFile(roomId);
        if (data == null) return false;      // старая комната остаётся на месте

        ClearRoom();
        CurrentRoomId = roomId;

        roomRoot = new GameObject("Room_" + roomId).transform;
        Transform root = roomRoot;           // запоминаем: к моменту окончания загрузки комната может смениться

        // Пока грузится комната, игрока замораживаем, иначе он провалится в пустоту
        SetPlayerControl(false);

        ApplyAmbient(data);
        await BuildEnvironment(data, root);
        if (root == null) return false;      // за время загрузки комнату уже сменили

        BuildLights(data, root);
        MovePlayerToSpawn(data);

        // Все объекты грузятся параллельно
        List<Task> tasks = new List<Task>();
        foreach (ObjectData obj in data.objects)
            tasks.Add(SpawnObject(obj, root));
        await Task.WhenAll(tasks);
        if (root == null) return false;

        SetPlayerControl(true);
        Debug.Log("Комната готова: " + data.title + " (" + data.objects.Length + " объектов, " + data.lights.Length + " ламп)");
        return true;
    }

    // ---------- чтение файла ----------

    RoomData ReadRoomFile(string roomId)
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Rooms", roomId + ".json");
        if (!File.Exists(path))
        {
            Debug.LogError("Не найден файл комнаты: " + path);
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            RoomData data = JsonUtility.FromJson<RoomData>(json);
            if (data == null) throw new System.Exception("пустой результат");
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Ошибка в JSON комнаты '" + roomId + "': " + e.Message);
            return null;
        }
    }

    // ---------- очистка ----------

    void ClearRoom()
    {
        if (roomRoot != null) Destroy(roomRoot.gameObject);

        // Освобождаем память, занятую загруженными моделями и картинами
        foreach (GltfImport gltf in loadedModels) gltf.Dispose();
        loadedModels.Clear();

        foreach (UnityEngine.Object a in tempAssets)
            if (a != null) Destroy(a);
        tempAssets.Clear();
    }

    // ---------- окружение и свет ----------

    void ApplyAmbient(RoomData d)
    {
        if (!string.IsNullOrEmpty(d.ambientColor))
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ParseColor(d.ambientColor);
        }
        else
        {
            RenderSettings.ambientMode = defaultAmbientMode;
            RenderSettings.ambientLight = defaultAmbientLight;
        }
    }

    void BuildLights(RoomData d, Transform root)
    {
        if (d.lights == null) return;

        for (int i = 0; i < d.lights.Length; i++)
        {
            LightData l = d.lights[i];
            GameObject go = new GameObject("Light_" + i + "_" + l.type);
            go.transform.SetParent(root, false);
            go.transform.localPosition = ToVector3(l.pos);
            go.transform.localEulerAngles = ToVector3(l.rot);

            Light light = go.AddComponent<Light>();
            switch ((l.type ?? "").ToLower())
            {
                case "spot": light.type = LightType.Spot; break;
                case "directional": light.type = LightType.Directional; break;
                default: light.type = LightType.Point; break;
            }
            light.color = ParseColor(l.color);
            light.intensity = l.intensity;
            light.range = l.range;
            light.spotAngle = l.spotAngle;
            light.shadows = l.shadows ? LightShadows.Soft : LightShadows.None;
        }
    }

    // Окружение комнаты: либо одна большая модель (.glb), либо простые пол и стены из JSON
    async Task BuildEnvironment(RoomData d, Transform root)
    {
        if (!string.IsNullOrEmpty(d.roomModel))
        {
            bool ok = await SpawnRoomModel(d, root);
            if (ok) return;
            Debug.LogWarning("Не удалось загрузить модель комнаты '" + d.roomModel + "'. Строю простые стены.");
        }

        if (root != null) BuildShell(d, root);
    }

    async Task<bool> SpawnRoomModel(RoomData d, Transform root)
    {
        GltfImport gltf = new GltfImport();
        bool loaded = await gltf.Load(ResolveUri(d.roomModel));
        if (!loaded)
        {
            gltf.Dispose();
            return false;
        }

        if (root == null)
        {
            gltf.Dispose();
            return true;
        }

        GameObject go = new GameObject("RoomModel");
        go.transform.SetParent(root, false);
        go.transform.localPosition = ToVector3(d.roomModelPos);
        go.transform.localEulerAngles = ToVector3(d.roomModelRot);
        go.transform.localScale = ToVector3(d.roomModelScale, Vector3.one);

        bool ok = await gltf.InstantiateMainSceneAsync(go.transform);
        if (!ok)
        {
            Destroy(go);
            gltf.Dispose();
            return false;
        }
        loadedModels.Add(gltf);

        // Для комнаты нужны точные коллайдеры по форме стен и пола (а не одна коробка)
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }
        return true;
    }

    // Пол и четыре стены по размерам из JSON (запасной вариант)
    void BuildShell(RoomData d, Transform root)
    {
        float w = d.size[0], l = d.size[1], h = d.wallHeight, t = 0.3f;
        Color floorC = ParseColor(d.floorColor), wallC = ParseColor(d.wallColor);

        MakeBox(root, "Floor", new Vector3(0, -t / 2f, 0), new Vector3(w, t, l), floorC);
        MakeBox(root, "Wall_N", new Vector3(0, h / 2f, l / 2f), new Vector3(w, h, t), wallC);
        MakeBox(root, "Wall_S", new Vector3(0, h / 2f, -l / 2f), new Vector3(w, h, t), wallC);
        MakeBox(root, "Wall_E", new Vector3(w / 2f, h / 2f, 0), new Vector3(t, h, l), wallC);
        MakeBox(root, "Wall_W", new Vector3(-w / 2f, h / 2f, 0), new Vector3(t, h, l), wallC);
    }

    void MakeBox(Transform root, string name, Vector3 pos, Vector3 scale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().material.color = color;
    }

    // ---------- объекты ----------

    async Task SpawnObject(ObjectData o, Transform root)
    {
        // 1. Если указана модель, пробуем загрузить её
        if (!string.IsNullOrEmpty(o.model))
        {
            bool ok = await SpawnModel(o, root);
            if (ok) return;
            Debug.LogWarning("Не удалось загрузить модель '" + o.model + "' (объект " + o.id + "). Ставлю заглушку.");
        }

        if (root == null) return;   // комнату уже сменили

        // 2. Если указана картинка, делаем «картину» с этой текстурой
        Texture2D tex = null;
        if (!string.IsNullOrEmpty(o.image))
        {
            tex = await LoadTexture(o.image);
            if (root == null)
            {
                if (tex != null) Destroy(tex);
                return;
            }
            if (tex != null) tempAssets.Add(tex);
        }

        // 3. Иначе (или если ничего не загрузилось) - простая фигура
        SpawnPrimitive(o, root, tex);
    }

    // Фигура-заглушка; если есть текстура, то с картинкой
    void SpawnPrimitive(ObjectData o, Transform root, Texture2D tex)
    {
        GameObject go = GameObject.CreatePrimitive(ShapeToPrimitive(o.shape));
        go.name = string.IsNullOrEmpty(o.id) ? o.type : o.id;
        go.transform.SetParent(root, false);
        ApplyTransform(go.transform, o);

        Renderer r = go.GetComponent<Renderer>();
        if (tex != null)
        {
            r.material.mainTexture = tex;
            r.material.color = Color.white;
        }
        else
        {
            r.material.color = ParseColor(o.color);
        }

        go.AddComponent<RoomObject>().data = o;
    }

    // Настоящая 3D-модель из .glb (с диска или по ссылке)
    async Task<bool> SpawnModel(ObjectData o, Transform root)
    {
        GltfImport gltf = new GltfImport();
        bool loaded = await gltf.Load(ResolveUri(o.model));
        if (!loaded)
        {
            gltf.Dispose();
            return false;
        }

        if (root == null)                     // пока грузили, игрок ушёл в другую комнату
        {
            gltf.Dispose();
            return true;
        }

        GameObject go = new GameObject(string.IsNullOrEmpty(o.id) ? o.type : o.id);
        go.transform.SetParent(root, false); // пока без позиции/поворота/масштаба

        bool ok = await gltf.InstantiateMainSceneAsync(go.transform);
        if (!ok)
        {
            Destroy(go);
            gltf.Dispose();
            return false;
        }
        loadedModels.Add(gltf);

        AddColliderFromBounds(go);            // считаем границы, пока трансформ единичный
        ApplyTransform(go.transform, o);
        go.AddComponent<RoomObject>().data = o;
        return true;
    }

    // Коробка-коллайдер по размерам модели
    void AddColliderFromBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        BoxCollider col = go.AddComponent<BoxCollider>();
        col.center = go.transform.InverseTransformPoint(b.center);
        col.size = b.size;
    }

    // Загрузка картинки из StreamingAssets (или по ссылке)
    async Task<Texture2D> LoadTexture(string path)
    {
        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(ResolveUri(path)))
        {
            UnityWebRequestAsyncOperation op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("Не удалось загрузить картинку '" + path + "': " + req.error);
                return null;
            }
            return DownloadHandlerTexture.GetContent(req);
        }
    }

    // ---------- вспомогательное ----------

    // Относительный путь = файл в StreamingAssets, http(s) = ссылка
    string ResolveUri(string path)
    {
        if (path.StartsWith("http://") || path.StartsWith("https://")) return path;

        string full = Path.Combine(Application.streamingAssetsPath, path);
        return new System.Uri(full).AbsoluteUri;   // file:///...
    }

    void ApplyTransform(Transform t, ObjectData o)
    {
        t.localPosition = ToVector3(o.pos);
        t.localEulerAngles = ToVector3(o.rot);
        t.localScale = ToVector3(o.scale, Vector3.one);
    }

    void SetPlayerControl(bool enabled)
    {
        if (player == null) return;
        FirstPersonController fpc = player.GetComponent<FirstPersonController>();
        if (fpc != null) fpc.enabled = enabled;
    }

    void MovePlayerToSpawn(RoomData d)
    {
        if (player == null) return;

        // CharacterController мешает телепортации, поэтому выключаем на момент переноса
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = ToVector3(d.spawn);
        player.rotation = Quaternion.Euler(0f, d.spawnYaw, 0f);
        if (cc != null) cc.enabled = true;
    }

    static PrimitiveType ShapeToPrimitive(string shape)
    {
        switch ((shape ?? "").ToLower())
        {
            case "sphere": return PrimitiveType.Sphere;
            case "cylinder": return PrimitiveType.Cylinder;
            case "capsule": return PrimitiveType.Capsule;
            default: return PrimitiveType.Cube;
        }
    }

    static Vector3 ToVector3(float[] a, Vector3? fallback = null)
    {
        if (a == null || a.Length < 3) return fallback ?? Vector3.zero;
        return new Vector3(a[0], a[1], a[2]);
    }

    static Color ParseColor(string hex)
    {
        if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color c)) return c;
        return Color.white;
    }
}

// Метка на созданном объекте: хранит его данные из JSON
public class RoomObject : MonoBehaviour
{
    public ObjectData data;
}
