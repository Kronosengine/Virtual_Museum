using System;

// Классы, описывающие структуру JSON-файла комнаты.
// Имена полей должны совпадать с именами в JSON (JsonUtility читает по именам).

[Serializable]
public class RoomData
{
    public string id;
    public string title;
    public float[] size = new float[] { 20f, 20f };   // ширина (X) и глубина (Z), если нет roomModel
    public float wallHeight = 4f;
    public string roomModel;                          // путь к .glb со всей комнатой
    public float[] roomModelPos = new float[] { 0f, 0f, 0f };
    public float[] roomModelRot = new float[] { 0f, 0f, 0f };
    public float[] roomModelScale = new float[] { 1f, 1f, 1f };
    public string floorColor = "#BFBFBF";
    public string wallColor = "#F2EFE6";
    public float[] spawn = new float[] { 0f, 1f, 0f }; // где появляется игрок
    public float spawnYaw = 0f;                        // куда смотрит игрок при появлении (0 = вдоль +Z)
    public string ambientColor;                        // общий фоновый свет комнаты (пусто = как в сцене)
    public LightData[] lights = new LightData[0];      // источники света комнаты
    public ObjectData[] objects = new ObjectData[0];
}

[Serializable]
public class LightData
{
    public string type = "point";       // point, spot, directional
    public float[] pos = new float[] { 0f, 3f, 0f };
    public float[] rot = new float[] { 90f, 0f, 0f };  // для spot и directional (90,0,0 = светит вниз)
    public string color = "#FFFFFF";
    public float intensity = 1f;
    public float range = 10f;           // для point и spot
    public float spotAngle = 60f;       // для spot
    public bool shadows = false;        // тени дороже по производительности, включай осторожно
}

[Serializable]
public class ObjectData
{
    public string id;
    public string type = "furniture";   // furniture, portal, interactive
    public string shape = "cube";       // cube, sphere, cylinder, capsule (замена модели)
    public string model;                // путь к .glb
    public string image;                // путь к картинке (.jpg/.png), натягивается на объект (картины, дипломы)
    public float[] pos = new float[] { 0f, 0f, 0f };
    public float[] rot = new float[] { 0f, 0f, 0f };
    public float[] scale = new float[] { 1f, 1f, 1f };
    public string color = "#FFFFFF";
    public string label;                // название для подсказки «E - войти: ...» и заголовок панели
    public string target;               // для портала: id комнаты, куда ведёт
    public string info;                 // для interactive: текст в панели описания
}
