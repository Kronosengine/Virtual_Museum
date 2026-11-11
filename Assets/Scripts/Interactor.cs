using System.Threading.Tasks;
using UnityEngine;

// Взаимодействие игрока с объектами: смотришь на картину/награду и жмёшь E.
// - portal (с target): затемнение, загрузка другой комнаты, появление
// - interactive: панель с описанием (label и info из JSON)
// Весь интерфейс рисуется кодом (OnGUI), настраивать Canvas не нужно.
// Повесить на объект Player.
public class Interactor : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Максимальное расстояние ПО ПОЛУ от игрока до объекта. Высоту не учитываем, чтобы можно было взаимодействовать с картинами под потолком.")]
    public float reach = 6f;
    public float fadeTime = 0.35f;   // длительность затемнения при переходе

    private const float MaxRayLength = 40f;   // как далеко вообще смотрит луч

    private Transform cam;
    private RoomLoader loader;
    private RoomObject looked;       // объект, на который смотрим
    private bool busy;               // идёт переход между комнатами
    private bool infoOpen;           // открыта панель описания
    private float fade;              // 0 = прозрачно, 1 = чёрный экран
    private string toast;
    private float toastUntil;

    private GUIStyle promptStyle, titleStyle, textStyle, toastStyle;

    void Start()
    {
        cam = Camera.main.transform;
        loader = FindObjectOfType<RoomLoader>();
        if (loader == null) Debug.LogError("Interactor: в сцене нет объекта с RoomLoader");
    }

    void Update()
    {
        looked = null;

        if (busy || Cursor.lockState != CursorLockMode.Locked)
        {
            infoOpen = false;
            return;
        }

        // Луч из центра экрана. Берём первый объект на пути (стена или мебель перекроет картину).
        // Объект доступен, если он в пределах reach по горизонтали от игрока.
        if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, MaxRayLength, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 flat = hit.point - transform.position;
            flat.y = 0f;

            if (flat.magnitude <= reach)
            {
                RoomObject ro = hit.collider.GetComponentInParent<RoomObject>();
                if (ro != null && IsUsable(ro.data)) looked = ro;
            }
        }

        if (looked == null) infoOpen = false;   // отвернулись - панель закрывается

        if (Input.GetKeyDown(KeyCode.E) && looked != null)
        {
            if (looked.data.type == "portal") UsePortal(looked.data.target);
            else infoOpen = !infoOpen;
        }
    }

    static bool IsUsable(ObjectData d)
    {
        if (d.type == "portal") return !string.IsNullOrEmpty(d.target);
        return d.type == "interactive";
    }

    // ---------- переход ----------

    async void UsePortal(string target)
    {
        busy = true;
        infoOpen = false;

        await FadeTo(1f);
        bool ok = await loader.LoadRoom(target);
        if (!ok) ShowToast("Не удалось открыть комнату: " + target);
        await FadeTo(0f);

        busy = false;
    }

    async Task FadeTo(float targetValue)
    {
        float from = fade, t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            fade = Mathf.Lerp(from, targetValue, t / fadeTime);
            await Task.Yield();
        }
        fade = targetValue;
    }

    void ShowToast(string text)
    {
        toast = text;
        toastUntil = Time.unscaledTime + 3f;
    }

    // ---------- интерфейс ----------

    void EnsureStyles()
    {
        if (promptStyle != null) return;

        promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        promptStyle.normal.textColor = Color.white;

        toastStyle = new GUIStyle(promptStyle) { fontSize = 20 };
        toastStyle.normal.textColor = new Color(1f, 0.8f, 0.5f);

        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, wordWrap = true };
        titleStyle.normal.textColor = Color.white;

        textStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true };
        textStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f);
    }

    void OnGUI()
    {
        EnsureStyles();
        float w = Screen.width, h = Screen.height;
        Texture2D white = Texture2D.whiteTexture;

        // Точка-прицел в центре
        GUI.color = new Color(1f, 1f, 1f, 0.8f);
        GUI.DrawTexture(new Rect(w / 2f - 2f, h / 2f - 2f, 4f, 4f), white);

        // Подсказка «E - ...»
        if (looked != null && !infoOpen && !busy)
        {
            ObjectData d = looked.data;
            string name = !string.IsNullOrEmpty(d.label) ? d.label : (d.type == "portal" ? d.target : d.id);
            string text = (d.type == "portal" ? "E — войти: " : "E — осмотреть: ") + name;

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(w / 2f - 300f, h * 0.78f, 600f, 46f), white);
            GUI.color = Color.white;
            GUI.Label(new Rect(w / 2f - 300f, h * 0.78f, 600f, 46f), text, promptStyle);
        }

        // Панель описания награды
        if (infoOpen && looked != null)
        {
            ObjectData d = looked.data;
            Rect panel = new Rect(w * 0.25f, h * 0.2f, w * 0.5f, h * 0.5f);

            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(panel, white);
            GUI.color = Color.white;

            string title = !string.IsNullOrEmpty(d.label) ? d.label : d.id;
            GUI.Label(new Rect(panel.x + 24f, panel.y + 20f, panel.width - 48f, 50f), title, titleStyle);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 80f, panel.width - 48f, panel.height - 140f), d.info ?? "", textStyle);
            GUI.Label(new Rect(panel.x, panel.yMax - 44f, panel.width, 36f), "E — закрыть", promptStyle);
        }

        // Сообщение об ошибке перехода
        if (Time.unscaledTime < toastUntil)
            GUI.Label(new Rect(0f, h * 0.15f, w, 40f), toast, toastStyle);

        // Затемнение рисуем последним, поверх всего
        if (fade > 0.001f)
        {
            GUI.color = new Color(0f, 0f, 0f, fade);
            GUI.DrawTexture(new Rect(0f, 0f, w, h), white);
        }
        GUI.color = Color.white;
    }
}
