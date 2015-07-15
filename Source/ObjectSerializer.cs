using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace ProceduralParts
{

    interface ISerialize
    {
        void OnSerialization();
        void OnDeserialization();
    }

    internal static class ObjectSerializer
    {

        internal static byte[] Serialize<T>(T obj)
        {

            ISerialize serializableObject = obj as ISerialize;

            if(serializableObject != null)
            {
                Debug.Log("OnSerialize");
                serializableObject.OnSerialization();
            }

            MemoryStream stream = new MemoryStream();
            using (stream)
            {
                BinaryFormatter fmt = new BinaryFormatter();
                fmt.Serialize(stream, obj);
            }
            return stream.ToArray();
        }

        internal static void Deserialize<T>(byte[] data, out T value)
        {
            using (MemoryStream stream = new MemoryStream(data))
            {
                BinaryFormatter fmt = new BinaryFormatter();
                value = (T)fmt.Deserialize(stream);
            }

            ISerialize serializableObject = value as ISerialize;

            if (serializableObject != null)
            {
                serializableObject.OnDeserialization();
            }
        }
    }

}