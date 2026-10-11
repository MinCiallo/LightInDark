using System.Reflection;
namespace HarmonyLib
{
    public class HarmonyMethod(Type type,string name) { }
    public class Harmony { public void Patch(MethodInfo method,HarmonyMethod prefix) { } }
}
namespace UnityEngine
{
    public struct Vector2(float x,float y) { public float x=x,y=y; }
    public struct Vector3(float x,float y,float z) { public float x=x,y=y,z=z; }
}
public class PlayerControl { public byte PlayerId; }
public class ClientData { public PlayerControl Character; }
public class AmongUsClient
{
    public static AmongUsClient Instance = new();
    public bool AmHost;
    public int HostId;
    public ClientData GetClient(int id) => new() {Character = new() {PlayerId=(byte)id}};
}
namespace Hazel
{
    public enum SendOption { Reliable }
    public class MessageWriter
    {
        private readonly MemoryStream stream = new();
        private BinaryWriter Writer => new(stream,System.Text.Encoding.UTF8,true);
        public static MessageWriter Get(SendOption option) => new();
        public byte[] ToByteArray(bool header) => stream.ToArray();
        public void Write(int value) => Writer.Write(value);
        public void Write(byte value) => Writer.Write(value);
        public void Write(float value) => Writer.Write(value);
        public void Write(bool value) => Writer.Write(value);
        public void Write(string value) => Writer.Write(value);
        public void Write(byte[] value,int offset,int length) => Writer.Write(value,offset,length);
    }
    public class MessageReader(byte[] data)
    {
        private readonly BinaryReader reader = new(new MemoryStream(data));
        public int ReadInt32() => reader.ReadInt32();
        public byte ReadByte() => reader.ReadByte();
        public float ReadSingle() => reader.ReadSingle();
        public bool ReadBoolean() => reader.ReadBoolean();
        public string ReadString() => reader.ReadString();
    }
}
namespace LightInDark.Core
{
    public static class LightLogger
    {
        public static void Log(string value) { }
        public static void LogWarning(string value) { }
        public static void LogError(string value,Exception error) => Console.WriteLine(value+": "+error.Message);
    }
}
namespace LightInDark.RPCs
{
    public static class CustomRPC
    {
        public record Packet(string Name,byte[] Bytes);
        private static readonly Dictionary<string,Action<Hazel.MessageReader>> handlers = new();
        public static readonly List<Packet> Wire = new();
        public static byte CurrentSender = byte.MaxValue;
        public static void Register(string name,Action<Hazel.MessageReader> handler) => handlers[name]=handler;
        public static void SendOnly(string name,Action<Hazel.MessageWriter> write,bool reliable)
        {
            var writer = Hazel.MessageWriter.Get(Hazel.SendOption.Reliable);
            write(writer); Wire.Add(new(name,writer.ToByteArray(false)));
        }
        public static void Deliver(Packet packet,byte sender)
        {
            var previous = CurrentSender; CurrentSender=sender;
            try { handlers[packet.Name](new(packet.Bytes)); }
            finally { CurrentSender=previous; }
        }
    }
    public static class Extensions
    {
        public static int ComputeConstantHash(this string value) { int h=0; foreach (char c in value) h=h*31+c; return h; }
        public static void WritePlayer(this Hazel.MessageWriter writer,PlayerControl player) => writer.Write(player?.PlayerId ?? byte.MaxValue);
        public static void WriteVector2(this Hazel.MessageWriter writer,UnityEngine.Vector2 v) {writer.Write(v.x);writer.Write(v.y);}
        public static void WriteVector3(this Hazel.MessageWriter writer,UnityEngine.Vector3 v) {writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}
        public static PlayerControl ReadPlayer(this Hazel.MessageReader reader) => new(){PlayerId=reader.ReadByte()};
        public static UnityEngine.Vector2 ReadVector2(this Hazel.MessageReader reader) => new(reader.ReadSingle(),reader.ReadSingle());
        public static UnityEngine.Vector3 ReadVector3(this Hazel.MessageReader reader) => new(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
    }
}
