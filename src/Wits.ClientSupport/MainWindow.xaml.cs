using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;

namespace Wits.ClientSupport;

public partial class MainWindow : Window
{
    private bool _isRunning;

    public MainWindow()
    {
        InitializeComponent();
        Log("WITS Client Support v0.1.0");
        Log("Ready. No system settings will be changed.");
    }

    private void Log(string message)
    {
        LogConsole.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogConsole.ScrollToEnd();
    }

    private async void CheckButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;

        _isRunning = true;
        CheckButton.IsEnabled = false;
        StatusLabel.Text = "Running connectivity checks...";
        Log("");
        Log("=== Starting internet connectivity diagnostics ===");

        try
        {
            bool result = await CheckConnectivityAsync();
            StatusLabel.Text = result
                ? "Completed: internet access appears available."
                : "Completed: one or more essential connectivity checks failed.";
            Log(result ? "RESULT: Internet connectivity looks OK." :
                "RESULT: Internet connectivity needs attention. Review details above.");
        }
        catch (Exception ex)
        {
            Log($"ERROR: {ex.Message}");
            StatusLabel.Text = "Diagnostics encountered an error.";
        }
        finally
        {
            _isRunning = false;
            CheckButton.IsEnabled = true;
        }
    }

    private async Task<bool> CheckConnectivityAsync()
    {
        var active = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .ToList();

        Log($"Active network adapters: {active.Count}");
        foreach (var adapter in active)
        {
            Log($"  Adapter: {adapter.Name} ({adapter.NetworkInterfaceType})");
            foreach (var address in adapter.GetIPProperties().UnicastAddresses
                         .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork))
                Log($"  IPv4: {address.Address}");
        }

        if (active.Count == 0)
        {
            Log("FAIL: No active network adapter.");
            return false;
        }

        var gateways = active.SelectMany(a => a.GetIPProperties().GatewayAddresses)
            .Select(g => g.Address)
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.Any.Equals(a))
            .Distinct().ToList();

        if (gateways.Count == 0) Log("WARN: No IPv4 default gateway found.");
        foreach (var gateway in gateways)
            await PingAddressAsync("Gateway", gateway);

        var dnsServers = active.SelectMany(a => a.GetIPProperties().DnsAddresses)
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
            .Distinct().ToList();

        if (dnsServers.Count == 0) Log("WARN: No IPv4 DNS server configured.");
        foreach (var dns in dnsServers)
            await PingAddressAsync("DNS server (ICMP optional)", dns);

        bool resolved = false;
        try
        {
            var addresses = await Dns.GetHostAddressesAsync("google.com")
                .WaitAsync(TimeSpan.FromSeconds(6));
            resolved = addresses.Length > 0;
            Log(resolved
                ? $"PASS: DNS resolves google.com -> {string.Join(", ", addresses.Take(4))}"
                : "FAIL: DNS returned no addresses for google.com.");
        }
        catch (Exception ex)
        {
            Log($"FAIL: DNS resolution: {ex.Message}");
        }

        bool tcp = false;
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("1.1.1.1", 443)
                .WaitAsync(TimeSpan.FromSeconds(5));
            tcp = client.Connected;
            Log(tcp ? "PASS: TCP connection to 1.1.1.1:443." :
                "FAIL: TCP connection to 1.1.1.1:443.");
        }
        catch (Exception ex)
        {
            Log($"FAIL: TCP connection: {ex.Message}");
        }

        bool http = false;
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
            using var response = await client.GetAsync("https://www.google.com/generate_204");
            http = response.StatusCode == HttpStatusCode.NoContent;
            Log(http
                ? "PASS: HTTPS connectivity (Google returned HTTP 204)."
                : $"WARN: HTTPS returned {(int)response.StatusCode} {response.ReasonPhrase}. Could be a captive portal or filtering.");
        }
        catch (Exception ex)
        {
            Log($"FAIL: HTTPS connectivity: {ex.Message}");
        }

        Log("Note: some gateways and DNS servers block ICMP ping. Failed pings alone do not prove an outage.");
        Log("=== Diagnostics complete ===");
        return resolved && (tcp || http);
    }

    private async Task PingAddressAsync(string label, IPAddress address)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(address, 2000);
            Log(reply.Status == IPStatus.Success
                ? $"PASS: {label} {address} responded in {reply.RoundtripTime} ms."
                : $"WARN: {label} {address} did not reply ({reply.Status}).");
        }
        catch (Exception ex)
        {
            Log($"WARN: {label} {address}: {ex.Message}");
        }
    }
}
