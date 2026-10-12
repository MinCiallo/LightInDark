using System.Reflection;
using LightInDark.RPCs;

int checks = 0;
void Check(bool condition,string name)
{
    if (!condition) throw new Exception("FAIL: "+name);
    checks++; Console.WriteLine("PASS: "+name);
}
LidRpcRegistry.ScanAndPatch(new HarmonyLib.Harmony());
AmongUsClient.Instance.AmHost = true;
TestRpc.Result("wolf", [1,2]);
Check(TestRpc.Executions == 1 && CustomRPC.Wire.Count == 1, "host sends and locally executes once");
var result = CustomRPC.Wire[0];
AmongUsClient.Instance.AmHost = false;
CustomRPC.Deliver(result,0);
Check(TestRpc.Executions == 2 && CustomRPC.Wire.Count == 1 && TestRpc.Team == "wolf"
    && TestRpc.Winners.SequenceEqual(new byte[] {1,2}), "client accepts host result without echoing it");
CustomRPC.Deliver(result,1);
Check(TestRpc.Executions == 2, "client rejects forged host-only result");
TestRpc.Result("forged",[7]);
Check(TestRpc.Executions == 2 && CustomRPC.Wire.Count == 1, "client cannot send host-only result");
CustomRPC.Deliver(result,byte.MaxValue);
Check(TestRpc.Executions == 2, "unknown sender cannot deliver host-only RPC");

AmongUsClient.Instance.AmHost = false;
TestRpc.RequestRole("Caller");
Check(TestRpc.RoleRequests == 1 && CustomRPC.Wire.Count == 2, "client request executes locally and sends one message");
var request = CustomRPC.Wire[1];
AmongUsClient.Instance.AmHost = true;
CustomRPC.Deliver(request,1);
Check(TestRpc.RoleRequests == 2 && CustomRPC.Wire.Count == 3 && TestRpc.RoleChanges == 1,
    "host handling a request broadcasts nested role change");
var role = CustomRPC.Wire[2];
AmongUsClient.Instance.AmHost = false;
CustomRPC.Deliver(role,0);
Check(TestRpc.RoleChanges == 2 && TestRpc.Role == "Caller" && CustomRPC.Wire.Count == 3,
    "other clients apply nested host role change without an echo");

AmongUsClient.Instance.AmHost = true;
TestRpc.Throwing();
AmongUsClient.Instance.AmHost = false;
int before = CustomRPC.Wire.Count;
TestRpc.Throwing();
Check(CustomRPC.Wire.Count == before, "exception does not leave local bypass enabled");

Console.WriteLine($"RPC regression: {checks} checks passed.");

public static class TestRpc
{
    public static int Executions, RoleRequests, RoleChanges;
    public static string Team, Role;
    public static byte[] Winners;
    // The fake Harmony boundary explicitly calls the real production prefix.
    private static bool Enter(string method,params object[] args)
        => LidRpcRegistry.RpcPrefix(typeof(TestRpc).GetMethod(method),args);
    [LidRPC(OnlyHost=true)]
    public static void Result(string team,byte[] winners)
    {
        if (!Enter(nameof(Result),team,winners)) return;
        Executions++; Team=team; Winners=winners;
    }
    [LidRPC]
    public static void RequestRole(string role)
    {
        if (!Enter(nameof(RequestRole),role)) return;
        RoleRequests++;
        if (AmongUsClient.Instance.AmHost) ChangeRole(role);
    }
    [LidRPC(OnlyHost=true)]
    public static void ChangeRole(string role)
    {
        if (!Enter(nameof(ChangeRole),role)) return;
        RoleChanges++; Role=role;
    }
    [LidRPC(OnlyHost=true)]
    public static void Throwing()
    {
        if (!Enter(nameof(Throwing))) return;
        throw new InvalidOperationException("expected test failure");
    }
}
