using System;

// Классы, описывающие структуру JSON-файла комнаты.
// Имена полей должны совпадать с именами в JSON (JsonUtility читает по именам).

[Serializable]
public class RoomData
{
    public string id;
    public string title;
    public float[] size = new float[] { 20f, 20f };   // ширина (X) и глубина (Z) комнаты
    public float wallHeight = 4f;
    public string roomModel;                              // путь к .glb со всей комнатой (если задан, пол и стены из size не строятся)
    public float[] roomModelPos = new float[] { 0f, 0f, 0f };
    public float[] roomModelRot = new float[] { 0f, 0f, 0f };
    public float[] roomModelScale = new float[] { 1f, 1f, 1f };
    public string floorColor = "#BFBFBF";
    public string wallColor = "#F2EFE6";
    public float[] spawn = new float[] { 0f, 1f, 0f }; // где появляется игрок
    public ObjectData[] objects = new ObjectData[0];
}

[Serializable]
public class ObjectData
{
    public string id;
    public string type = "furniture";   // furniture, portal, interactive, video (пока просто метка)
    public string shape = "cube";       // cube, sphere, cylinder, capsule (временная замена модели)
    public string model;                // путь к .glb
    public float[] pos = new float[] { 0f, 0f, 0f };
    public float[] rot = new float[] { 0f, 0f, 0f };
    public float[] scale = new float[] { 1f, 1f, 1f };
    public string color = "#FFFFFF";
    public string target;               // для портала: id комнаты, куда ведёт
    public string info;                 // для interactive: текст подсказки
}