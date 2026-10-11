using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Light.Handshake;
using LightInDark.Configuration;
using LightInDark.Handshake;
using LightInDark.RPCs;
using LightInDark.UI.Ability;
using UnityEngine;
using Mono.Cecil;

class Program
{
    static int passed;
    static void Check(bool ok,string description) { if(!ok) throw new Exception(description); passed++; Console.WriteLine("PASS "+description); }
    static ActionButton Button(string name,Transform parent,bool active=true)
    {
        var go=new GameObject(name); go.transform.SetParent(parent,false); go.SetActive(active);
        return go.AddComponent<ActionButton>();
    }
    static void GridTests()
    {
        var hud=new GameObject("Hud").AddComponent<HudManager>(); HudManager.Instance=hud;
        hud.transform.localPosition=new(9,8,7);
        var aspect=hud.gameObject.AddComponent<AspectPosition>();
        hud.UseButton=Button("Use",hud.transform,false);
        hud.PetButton=Button("Pet",hud.transform,false);
        hud.ReportButton=Button("Report",hud.transform,false);
        var grid=HudGrid.Ensure();
        Check(grid!=null,"HUD grid created");
        Check(hud.transform.localPosition.x==9 && hud.transform.localPosition.y==8 && aspect.enabled && hud.gameObject.name=="Hud","shared HUD anchor, position and name preserved");
        var use=hud.UseButton;
        Check(use.transform.parent!=hud.transform && use.transform.parent.parent==hud.transform,"owned button container");
        grid.Tick();
        use.gameObject.SetActive(true); HudGridTicker.Postfix();
        float edge=3f*Screen.width/Screen.height-0.8f;
        Check(Math.Abs(use.transform.localPosition.x-edge)<0.001 && Math.Abs(use.transform.localPosition.y+2.3f)<0.001,"Use placed at bottom right on first activation in lobby");
        use.gameObject.SetActive(false); grid.Tick(); use.transform.localPosition=Vector3.zero;
        use.gameObject.SetActive(true); grid.Tick();
        Check(Math.Abs(use.transform.localPosition.x-edge)<0.001,"Use correctly placed when approaching an interactable again");
        var content=new HudContent(new GameObject("Ability")); content.CurrentPos=new(0,0); content.Tick();
        content.Transform.localPosition=Vector3.zero; Time.deltaTime=2; content.Tick();
        Check(Math.Abs(content.Transform.localPosition.x-edge)<0.001,"long frame cannot overshoot button target"); Time.deltaTime=1f/60;
        foreach(var size in new[]{(1920,1080),(1280,1024),(3440,1440)})
        {
            Screen.width=size.Item1; Screen.height=size.Item2; grid.Tick();
            Check(float.IsFinite(use.transform.localPosition.x),$"finite HUD layout at {size.Item1}x{size.Item2}");
        }
        Screen.width=1920; Screen.height=1080;
    }
    static void ConfigTests()
    {
        ConfigRegistry.Blocks.Clear();
        var role=new ConfigBlock{Key="role.a",Category=ConfigCategory.Crewmate};
        var count=new ConfigItem{Key="role.a.count",Block=role};
        var detail=new ConfigItem{Key="role.a.detail",Block=role,IsVisible=false}; role.Items.AddRange([count,detail]);
        var other=new ConfigBlock{Key="role.b",Category=ConfigCategory.Impostor}; other.Items.Add(new(){Key="role.b.count",Block=other});
        var mod=new ConfigBlock{Key="mod",Category=ConfigCategory.Mod}; mod.Items.Add(new(){Key="mod.option",Block=mod});
        ConfigRegistry.Blocks.AddRange([role,other,mod]); ConfigFixture.Setup(role);
        float before=ConfigFixture.Y(count);
        detail.IsVisible=true;
        Check(ConfigFixture.Refresh() && ConfigFixture.Count==2,"changing maximum count adds only selected role options");
        Check(Math.Abs(ConfigFixture.Y(count)-before)<0.0001,"role row origin unchanged after incremental update");
        Check(Math.Abs(ConfigFixture.Bottom()-(1.05f-0.38f-0.14f-0.63f-2*0.45f))<0.0001,"scroll height measures only selected role");
        for(int i=0;i<100;i++) { detail.IsVisible=i%2==0; if(!ConfigFixture.Refresh() || ConfigFixture.Count!=(detail.IsVisible?2:1)) throw new Exception("role visibility cycle "+i); }
        Check(true,"100 role count visibility changes preserve scope without accumulating rows");
    }
    static byte[] Payload(string account,string version,int nonce,int api,int mod,long exp)
    {
        using var stream=new MemoryStream(); using var w=new BinaryWriter(stream);
        var a=Encoding.UTF8.GetBytes(account); var v=Encoding.UTF8.GetBytes(version);
        w.Write((byte)a.Length); w.Write(a); w.Write((byte)v.Length); w.Write(v); w.Write(nonce); w.Write(api); w.Write(mod); w.Write(exp);
        return stream.ToArray();
    }
    static string Sign(ECDsa key,byte[] payload)=>Convert.ToBase64String(payload.Concat(key.SignData(payload,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation)).ToArray());
    static bool Parse(string ticket,string version,string pem)=>HandshakeCrypto.TryParseWithKey(ticket,version,pem,out _,out _,out _,out _,out _,out _);
    static void CryptoTests()
    {
        using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256); var pem=key.ExportSubjectPublicKeyInfoPem();
        var payload=Payload("账号","regression",int.MinValue,123,-456,DateTimeOffset.UtcNow.ToUnixTimeSeconds()+60);
        var ticket=Sign(key,payload);
        Check(HandshakeCrypto.TryParseWithKey(ticket,"regression",pem,out var account,out _,out var api,out var mod,out _,out var nonce) && account=="账号" && api==123 && mod==-456 && nonce==int.MinValue,"P-256/P1363 ticket round trip preserves UTF-8, signed hashes and nonce");
        var tampered=Convert.FromBase64String(ticket); tampered[2]^=1;
        Check(!Parse(Convert.ToBase64String(tampered),null,pem),"tampered ticket rejected");
        Check(!Parse(ticket,"different",pem),"wrong version rejected");
        Check(Parse(ticket,null,pem),"signature can be checked before classifying version mismatch");
        Check(!Parse(Sign(key,Payload("a","regression",1,1,1,DateTimeOffset.UtcNow.ToUnixTimeSeconds()-1)),null,pem),"expired ticket rejected");
        using var wrongKey=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Check(!Parse(ticket,null,wrongKey.ExportSubjectPublicKeyInfoPem()),"wrong signing key rejected");
        foreach(var bad in new[]{"", "not base64",new string('A',248),Convert.ToBase64String(new byte[85]),Convert.ToBase64String(new byte[183])})
            Check(!Parse(bad,null,pem),"malformed ticket rejected (length "+bad.Length+")");
        var max=Sign(key,Payload(new string('a',64),new string('v',32),1,1,1,DateTimeOffset.UtcNow.ToUnixTimeSeconds()+60));
        Check(Parse(max,null,pem),"maximum allowed account and version lengths accepted");
        Check(!Parse(Sign(key,Payload(new string('a',65),"v",1,1,1,DateTimeOffset.UtcNow.ToUnixTimeSeconds()+60)),null,pem),"oversized account rejected");
        Check(!Parse(Sign(key,Payload("a",new string('v',33),1,1,1,DateTimeOffset.UtcNow.ToUnixTimeSeconds()+60)),null,pem),"oversized version rejected");
        Check(!Parse(Sign(key,Payload("a","",1,1,1,DateTimeOffset.UtcNow.ToUnixTimeSeconds()+60)),null,pem),"empty version rejected");
    }
    static void PopupTests()
    {
        var cam=new GameObject("UI Camera").AddComponent<Camera>(); cam.transform.position=new(7,5,-1000); Camera.allCameras=[cam];
        var menu=new GameObject("Menu"); menu.transform.position=new(100,-100,-910);
        var anchor=new GameObject("Replay"); anchor.layer=5; anchor.transform.SetParent(menu.transform,false); anchor.AddComponent<SpriteRenderer>();
        Input.mousePosition=new(Screen.width*0.06f,Screen.height*0.93f,0);
        PopupFixture.Show("复盘信息",false,anchor.transform);
        var screen=cam.WorldToScreenPoint(PopupFixture.WorldPosition);
        Check(PopupFixture.Parent==cam.transform,"tooltip coordinates use actual camera parent");
        Check(screen.x<Screen.width*0.2 && screen.y>Screen.height*0.8,"replay tooltip stays near top-left cursor instead of moving to center");
        Check(Math.Abs(PopupFixture.WorldPosition.z+915)<0.001,"tooltip keeps reference UI rendering depth");
        Time.frameCount++; Input.Tab=true; PopupFixture.Update();
        PopupFixture.Hide(); Check(!PopupFixture.Visible,"tooltip without pin permission cannot be pinned");
        PopupFixture.Show("复盘信息",true,anchor.transform); Time.frameCount++; PopupFixture.Update();
        PopupFixture.Show("other",true,anchor.transform); PopupFixture.Hide();
        Check(PopupFixture.Visible && PopupFixture.Text=="复盘信息","pinned tooltip keeps content and visibility");
        Time.frameCount++; PopupFixture.Update(); Check(!PopupFixture.Visible,"unpin after leaving hover hides tooltip"); Input.Tab=false;
        var otherParent=new GameObject("OtherMenu"); anchor.transform.SetParent(otherParent.transform,false);
        PopupFixture.Show("复盘信息",true,anchor.transform);
        Check(PopupFixture.Text=="复盘信息","same tooltip content survives recreation under another menu");
        anchor.SetActive(false); Time.frameCount++; PopupFixture.Update();
        Check(!PopupFixture.Visible,"tooltip closes when anchor menu disappears");
    }
    static PlayerControl Player(byte pid)
    {
        var p=new GameObject("P"+pid).AddComponent<PlayerControl>(); p.PlayerId=pid; p.NetId=(uint)pid+100;
        PlayerControl.AllPlayerControls.Add(p); AmongUsClient.Instance.Clients[pid]=new(){Character=p,ProductUserId="account"+pid}; return p;
    }
    static T Field<T>(string name)=>(T)typeof(HandshakeManager).GetField(name,BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
    static void HandshakeTests()
    {
        HandshakeManager.Initialize(); HandshakeManager.Reset(); CustomRPC.Sent.Clear(); PlayerControl.AllPlayerControls.Clear();
        AmongUsClient.Instance=new(); Light.LightPlugin.LightSettingsData=new(); PlayerControl.LocalPlayer=null;
        var host=Player(0); var guest=Player(1);
        HandshakeManager.Tick(); Check(!HandshakeManager.HasPending(),"joining before local player is ready does not create a broken challenge");
        PlayerControl.LocalPlayer=host; HandshakeManager.Tick();
        Check(HandshakeManager.HasPending() && CustomRPC.Sent.Count==1,"lobby tick discovers previously joined player");
        int nonce=(int)CustomRPC.Sent[0].Values[1]; Time.realtimeSinceStartup=2.5f; HandshakeManager.Tick();
        Check(CustomRPC.Sent.Count==2 && (int)CustomRPC.Sent[1].Values[1]==nonce,"lost challenge is retried with same nonce");
        CustomRPC.CurrentSender=2;
        CustomRPC.Handlers["Light.Handshake"](new((byte)1,123,456,"invalid"));
        Check(HandshakeManager.HasPending() && !HandshakeManager.HasUnverified(),"forged player identity cannot fail another player");
        CustomRPC.CurrentSender=1;
        CustomRPC.Handlers["Light.Handshake"](new((byte)1,123,456,"invalid"));
        Check(!HandshakeManager.HasPending() && HandshakeManager.HasUnverified(),"invalid ticket ends pending state");
        HandshakeManager.CleanupPlayer(1); Field<HashSet<byte>>("_verified").Add(1);
        CustomRPC.Handlers["Light.Handshake"](new((byte)1,123,456,"invalid"));
        Check(Field<HashSet<byte>>("_verified").Contains(1) && !HandshakeManager.HasUnverified(),"duplicate/unsolicited response cannot revoke passed verification");
        HandshakeManager.CleanupPlayer(1); Time.realtimeSinceStartup=3; HandshakeManager.Tick();
        Time.realtimeSinceStartup=14; HandshakeManager.Tick();
        Check(!HandshakeManager.HasPending() && HandshakeManager.HasUnverified(),"timeout resolves pending state");
        Light.LightPlugin.LightSettingsData.EnableHandshake=false; HandshakeManager.Tick();
        Check(!HandshakeManager.HasPending() && !HandshakeManager.HasUnverified(),"disabling handshake clears gate and failure states");
        var gsm=new GameStartManager();
        Light.LightPlugin.LightSettingsData.EnableHandshake=true; Time.realtimeSinceStartup=15;
        HandshakeLobbyGatePatch.UpdatePostfix(gsm); Check(!gsm.StartButton.Enabled,"pending handshake blocks start");
        Light.LightPlugin.LightSettingsData.EnableHandshake=false;
        HandshakeLobbyGatePatch.UpdatePrefix(gsm);
        gsm.StartButton.SetButtonEnableState(false); // 原版 Update 判定人数不足。
        HandshakeLobbyGatePatch.UpdatePostfix(gsm);
        Check(!gsm.StartButton.Enabled,"restoring handshake gate preserves vanilla insufficient-player restriction");
        HandshakeManager.Reset(); Light.LightPlugin.LightSettingsData=new(){HandshakeMode=1};
        Time.realtimeSinceStartup=20; HandshakeManager.Tick(); Time.realtimeSinceStartup=31; HandshakeManager.Tick();
        var kick=Dispatcher.Instance.Coroutines.Last(); kick.MoveNext();
        AmongUsClient.Instance.GameId=2; HandshakeManager.Tick(); kick.MoveNext();
        Check(!RpcDefinitions.Kicked.Contains(1),"delayed kick cannot affect a different lobby");
        HandshakeManager.Reset();
    }
    static async Task ServerTests(string serverAssembly)
    {
        string dir=Path.GetFullPath("Tests/Regression/obj/server-fixture");
        Directory.CreateDirectory(dir);
        foreach(var file in Directory.GetFiles(Path.GetDirectoryName(serverAssembly))) File.Copy(file,Path.Combine(dir,Path.GetFileName(file)),true);
        Directory.CreateDirectory(Path.Combine(dir,"data"));
        File.WriteAllText(Path.Combine(dir,"data/official.json"),"{\"versions\":[{\"version\":\"regression\",\"apiHash\":123,\"modHash\":456}]}");
        using var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0); listener.Start();
        int port=((System.Net.IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var serverLog=new StringBuilder();
        string dotnet=Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),"../../..",OperatingSystem.IsWindows()?"dotnet.exe":"dotnet"));
        using var proc=new Process{StartInfo=new(dotnet,$"{Path.Combine(dir,Path.GetFileName(serverAssembly))} --urls http://127.0.0.1:{port}"){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false}};
        proc.OutputDataReceived+=(_,e)=>{lock(serverLog)serverLog.AppendLine(e.Data);}; proc.ErrorDataReceived+=(_,e)=>{lock(serverLog)serverLog.AppendLine(e.Data);}; proc.Start(); proc.BeginOutputReadLine(); proc.BeginErrorReadLine();
        try
        {
            using var http=new HttpClient{BaseAddress=new($"http://127.0.0.1:{port}"),Timeout=TimeSpan.FromSeconds(5)};
            bool ready=false;
            for(int i=0;i<50;i++) { try { ready=(await http.GetAsync("/health")).IsSuccessStatusCode; if(ready)break; }catch(HttpRequestException){} await Task.Delay(100); }
            Check(ready,"real verification server starts: "+(ready ? "ready" : serverLog.ToString()));
            var pem=await http.GetStringAsync("/pubkey");
            async Task Verify(int n)
            {
                using var resp=await http.PostAsJsonAsync("/verify",new{version="regression",apiHash=123,modHash=456,accountId="account",nonce=n});
                resp.EnsureSuccessStatusCode(); using var json=JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var ticket=json.RootElement.GetProperty("ticket").GetString();
                if(!HandshakeCrypto.TryParseWithKey(ticket,"regression",pem,out var account,out _,out var api,out var mod,out _,out var nonce) || account!="account" || api!=123 || mod!=456 || nonce!=n) throw new Exception("server/client protocol mismatch");
            }
            await Verify(int.MinValue); Check(true,"real HTTP server ticket accepted by production client parser");
            await Task.WhenAll(Enumerable.Range(0,32).Select(Verify)); Check(true,"32 concurrent signing requests produce valid bound tickets");
            // 实际客户端挑战入口 -> HTTP -> 主线程队列 -> 应答 RPC。
            HandshakeManager.Reset();
            AmongUsClient.Instance.AmHost=false; AmongUsClient.Instance.GameId=100;
            PlayerControl.LocalPlayer=PlayerControl.AllPlayerControls.Single(p=>p.PlayerId==1);
            Light.LightPlugin.LightSettingsData=new(){EnableHandshake=false,VerifyServerUrl=http.BaseAddress.ToString()};
            typeof(HandshakeManager).GetField("_localApiHash",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,123);
            typeof(HandshakeManager).GetField("_localModHash",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,456);
            CustomRPC.CurrentSender=0; CustomRPC.Sent.Clear();
            CustomRPC.Handlers["Light.Handshake.Challenge"](new((byte)1,42));
            for(int i=0;i<100 && Dispatcher.Instance.Queue.Count<2;i++) await Task.Delay(20);
            Dispatcher.Instance.Drain();
            Check(CustomRPC.Sent.Count==1 && CustomRPC.Sent[0].Hash=="Light.Handshake","client with local enforcement disabled still answers host via HTTP and main-thread dispatcher");
            var responseTicket=(string)CustomRPC.Sent[0].Values[3];
            Check(HandshakeCrypto.TryParseWithKey(responseTicket,"regression",pem,out var account,out _,out _,out _,out _,out var returnedNonce) && account=="account1" && returnedNonce==42,"client response is bound to captured account and host nonce");
            CustomRPC.Handlers["Light.Handshake.Challenge"](new((byte)1,42));
            Check(CustomRPC.Sent.Count==2,"repeated same challenge resends cached current ticket");
            CustomRPC.CurrentSender=2; CustomRPC.Handlers["Light.Handshake.Challenge"](new((byte)1,43));
            Check(CustomRPC.Sent.Count==2,"non-host challenge cannot trigger ticket request");
            CustomRPC.CurrentSender=0; CustomRPC.Handlers["Light.Handshake.Challenge"](new((byte)1,44));
            for(int i=0;i<100 && Dispatcher.Instance.Queue.Count<2;i++) await Task.Delay(20);
            AmongUsClient.Instance.GameId=101; Dispatcher.Instance.Drain();
            Check(CustomRPC.Sent.Count==2,"ticket finishing after room change cannot send stale handshake");
            using var mismatch=await http.PostAsJsonAsync("/verify",new{version="regression",apiHash=123,modHash=999,nonce=1});
            Check((int)mismatch.StatusCode==403,"server rejects unregistered build hash");
            using var zero=await http.PostAsJsonAsync("/verify",new{version="regression",apiHash=0,modHash=456,nonce=1});
            Check((int)zero.StatusCode==403,"server rejects unresolved local hash");
        }
        finally { if(!proc.HasExited)proc.Kill(true); await proc.WaitForExitAsync(); }
    }
    static void PatchTests(string assembly)
    {
        using var module=ModuleDefinition.ReadModule(assembly);
        IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> source) => source.SelectMany(t=>new[]{t}.Concat(Types(t.NestedTypes)));
        var types=Types(module.Types).ToArray();
        foreach(var name in new[]{"GameSettingMenuPatch","GameSettingMenuTickPatch","ConfigRowPatches","DetailPopupDriverPatch","HandshakeLobbyGatePatch","HandshakeJoinPatch","HandshakeResetPatch","GameStartCountdownPatch","ReplayLobbyButtonPatch"})
        {
            var t=types.Single(x=>x.Name==name);
            Check(t.CustomAttributes.Any(a=>a.AttributeType.Name=="HarmonyPatch"),"built patch discoverable: "+name);
        }
        using var api=ModuleDefinition.ReadModule(Path.Combine(Path.GetDirectoryName(assembly),"LightInDark.dll"));
        var ticker=Types(api.Types).Single(t=>t.Name=="HudGridTicker");
        var target=ticker.CustomAttributes.Single(a=>a.AttributeType.Name=="HarmonyPatch");
        Check(target.ConstructorArguments.Count==2 && ((TypeReference)target.ConstructorArguments[0].Value).Name=="HudManager" && (string)target.ConstructorArguments[1].Value=="Update","built HUD ticker targets HudManager.Update in lobby and game");
        var priority=ticker.Methods.Single(m=>m.Name=="Postfix").CustomAttributes.Single(a=>a.AttributeType.Name=="HarmonyPriority");
        Check((int)priority.ConstructorArguments[0].Value==0,"built HUD layout executes with Priority.Last");
    }
    static async Task<int> Main(string[] args)
    {
        try
        {
            if(args.Contains("--config-only")) { ConfigTests(); return 0; }
            if(args.Contains("--popup-only")) { PopupTests(); return 0; }
            GridTests(); ConfigTests(); PopupTests(); CryptoTests(); HandshakeTests();
            if(args.Length>0) await ServerTests(Path.GetFullPath(args[0]));
            if(args.Length>1) PatchTests(args[1]);
            Check(LightInDark.Core.LightLogger.Errors.Count==0,"no swallowed exceptions in regression scenarios");
            Console.WriteLine($"{passed} checks passed"); return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine("FAIL "+ex); return 1; }
    }
}
