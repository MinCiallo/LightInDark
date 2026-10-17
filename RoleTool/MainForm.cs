using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace RoleTool;

/// <summary>RoleTool 主窗口：承载 WebView2 并加载前端页面。</summary>
public sealed class MainForm : Form
{
    private const string HostName = "roletool.local";
    private readonly WebView2 _webView;

    public MainForm()
    {
        Text = "RoleTool";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1440, 900);

        _webView = new WebView2 { Dock = DockStyle.Fill };
        Controls.Add(_webView);
        Load += OnFormLoad;
    }

    private async void OnFormLoad(object? sender, EventArgs e)
    {
        try
        {
            await _webView.EnsureCoreWebView2Async(null);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "未能初始化 WebView2 运行时，请先安装 “Microsoft Edge WebView2 Runtime”：\n" +
                "https://developer.microsoft.com/microsoft-edge/webview2/\n\n" +
                $"详细信息：{ex.Message}",
                "RoleTool - 缺少 WebView2 运行时",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Close();
            return;
        }

        CoreWebView2 core = _webView.CoreWebView2;

        // 将本地 Resources 目录映射为虚拟主机，便于以 https 方式加载前端
        string resourcesDir = Path.Combine(AppContext.BaseDirectory, "Resources");
        core.SetVirtualHostNameToFolderMapping(
            HostName,
            resourcesDir,
            CoreWebView2HostResourceAccessKind.Allow);

        core.Settings.AreDevToolsEnabled = true;
        core.WebMessageReceived += OnWebMessageReceived;

        _webView.Source = new Uri($"https://{HostName}/index.html");
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string raw = e.TryGetWebMessageAsString();

        // 交给桥接层处理，返回的 JSON 直接回传给前端
        string result = Bridge.JsBridge.Handle(raw);

        if (sender is CoreWebView2 core)
        {
            core.PostWebMessageAsJson(result);
        }
    }
}
