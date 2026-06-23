using System.Diagnostics;
using System.Text;

namespace SocksToVpn
{
    public class Tun2SocksRunner
    {
        private readonly string _tun2SocksPath;
        private readonly ProxySettings _proxySettings;

        public Tun2SocksRunner(string tun2SocksPath, ProxySettings proxySettings)
        {
            _tun2SocksPath = tun2SocksPath;
            _proxySettings = proxySettings;
        }

        public async Task RunAsync()
        {
            OsInfo.OsType osType = OsInfo.GetOperatingSystem();
            
            Console.WriteLine($"Configuring tun2socks for {osType}...");
            
            switch (osType)
            {
                case OsInfo.OsType.Windows:
                    await RunOnWindowsAsync();
                    break;
                case OsInfo.OsType.MacOS:
                    await RunOnMacOSAsync();
                    break;
                case OsInfo.OsType.Linux:
                    await RunOnLinuxAsync();
                    break;
                default:
                    throw new NotSupportedException($"Operating system not supported: {osType}");
            }
        }

        private async Task RunOnWindowsAsync()
        {
            // On Windows, we need to:
            // 1. Create a TUN interface using Wintun
            // 2. Configure routing and DNS
            // 3. Run tun2socks with appropriate parameters

            // Ensure we're running with administrator privileges
            if (!IsAdministrator())
            {
                Console.WriteLine("Warning: This application requires administrator privileges to configure network interfaces.");
                Console.WriteLine("Please run the application as administrator.");
                return;
            }

            // Get the primary network interface name
            Console.WriteLine("Detecting primary network interface...");
            string interfaceName = NetworkHelper.GetPrimaryInterfaceName();
            Console.WriteLine($"Using {interfaceName} as the primary network interface");
            
            // Prepare the tun2socks command
            string proxyAuth = string.Empty;
            if (!string.IsNullOrEmpty(_proxySettings.Username) && !string.IsNullOrEmpty(_proxySettings.Password))
            {
                proxyAuth = $"{_proxySettings.Username}:{_proxySettings.Password}@";
            }

            string proxyServer = $"{proxyAuth}{_proxySettings.IpAddress}:{_proxySettings.Port}";
            string arguments = $"-device tun://wintun -proxy socks5://{proxyServer} -interface \"{interfaceName}\"";

            Console.WriteLine($"Starting tun2socks with command: {_tun2SocksPath} {arguments}");
            
            // Start the process
            var processInfo = new ProcessStartInfo
            {
                FileName = _tun2SocksPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = new Process
            {
                StartInfo = processInfo
            };

            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();

            // Set up async output handlers
            process.OutputDataReceived += (sender, e) => 
            {
                if (e.Data != null)
                {
                    Console.WriteLine($"[tun2socks] {e.Data}");
                    output.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) => 
            {
                if (e.Data != null)
                {
                    Console.WriteLine($"[tun2socks ERROR] {e.Data}");
                    error.AppendLine(e.Data);
                }
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                Console.WriteLine("Wait for the interface to be created by tun2sock");
                // Configure the interface (add this command after tun2socks has started)
                await Task.Delay(5000); // Wait a bit for the interface to be created

                // Configure IP address for the wintun interface
                Console.WriteLine("Configuring wintun interface...");
                ExecuteCommand("netsh", "interface ipv4 set address name=\"wintun\" source=static addr=192.168.123.1 mask=255.255.255.0");
                
                // Set DNS for the interface
                Console.WriteLine("Configuring DNS settings...");
                ExecuteCommand("netsh", "interface ipv4 set dnsservers name=\"wintun\" static address=8.8.8.8 register=none validate=no");

                // Configure routing to redirect traffic through the TUN interface
                Console.WriteLine("Configuring routing...");
                ExecuteCommand("netsh", "interface ipv4 add route 0.0.0.0/0 \"wintun\" 192.168.123.1 metric=1");

                Console.WriteLine("tun2socks is running. Press Ctrl+C to stop.");
                
                // Keep the process running
                await WaitForProcessExitAsync(process);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running tun2socks: {ex.Message}");
                if (output.Length > 0)
                    Console.WriteLine($"Output: {output}");
                if (error.Length > 0)
                    Console.WriteLine($"Error: {error}");
            }
        }

        private async Task RunOnMacOSAsync()
        {
            // On macOS, we need to:
            // 1. Start tun2socks first to create the TUN interface
            // 2. Configure the interface with ifconfig
            // 3. Set up routing

            // Check for root privileges
            if (!IsAdministrator())
            {
                Console.WriteLine("Warning: This application requires root privileges to configure network interfaces.");
                Console.WriteLine("Please run the application with sudo.");
                return;
            }

            // Get the primary network interface
            Console.WriteLine("Detecting primary network interface...");
            string primaryInterface = NetworkHelper.GetPrimaryInterfaceName();
            Console.WriteLine($"Using {primaryInterface} as primary network interface");
            
            // Prepare the tun2socks command
            string proxyAuth = string.Empty;
            if (!string.IsNullOrEmpty(_proxySettings.Username) && !string.IsNullOrEmpty(_proxySettings.Password))
            {
                proxyAuth = $"{_proxySettings.Username}:{_proxySettings.Password}@";
            }

            string proxyServer = $"{proxyAuth}{_proxySettings.IpAddress}:{_proxySettings.Port}";
            string arguments = $"-device utun123 -proxy socks5://{proxyServer} -interface {primaryInterface}";

            Console.WriteLine($"Starting tun2socks with command: {_tun2SocksPath} {arguments}");

            // Start the process
            var processInfo = new ProcessStartInfo
            {
                FileName = _tun2SocksPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = new Process
            {
                StartInfo = processInfo
            };

            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();

            process.OutputDataReceived += (sender, e) => 
            {
                if (e.Data != null)
                {
                    Console.WriteLine($"[tun2socks] {e.Data}");
                    output.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) => 
            {
                if (e.Data != null)
                {
                    Console.WriteLine($"[tun2socks ERROR] {e.Data}");
                    error.AppendLine(e.Data);
                }
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                Console.WriteLine("Wait for the interface to be created by tun2sock");
                // Wait for the interface to be created by tun2socks
                await Task.Delay(5000);

                // Configure the TUN interface with ifconfig
                Console.WriteLine("Configuring utun123 interface...");
                ExecuteCommand("sudo", "ifconfig utun123 198.18.0.1 198.18.0.1 up");
                
                // Configure routing to redirect traffic through the TUN interface
                Console.WriteLine("Configuring routing...");
                ExecuteCommand("sudo", "route add -net 1.0.0.0/8 198.18.0.1");
                ExecuteCommand("sudo", "route add -net 2.0.0.0/7 198.18.0.1");
                ExecuteCommand("sudo", "route add -net 4.0.0.0/6 198.18.0.1");
                ExecuteCommand("sudo", "route add -net 8.0.0.0/5 198.18.0.1");
                ExecuteCommand("sudo", "route add -net 16.0.0.0/4 198.18.0.1");
                ExecuteCommand("sudo", "route add -net 32.0.0.0/3 198.18.0.1");
                ExecuteCommand("sudo", "route add -net 64.0.0.0/2 198.18.0.1");
                ExecuteCommand("sudo", "route add -net 128.0.0.0/1 198.18.0.1");
                ExecuteCommand("sudo", "route add -net 198.18.0.0/15 198.18.0.1");

                Console.WriteLine("tun2socks is running. Press Ctrl+C to stop.");
                
                // Keep the process running
                await WaitForProcessExitAsync(process);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running tun2socks: {ex.Message}");
                if (output.Length > 0)
                    Console.WriteLine($"Output: {output}");
                if (error.Length > 0)
                    Console.WriteLine($"Error: {error}");
            }
        }

        private async Task RunOnLinuxAsync()
        {
            // On Linux, we need to:
            // 1. Create a TUN interface
            // 2. Configure routing
            // 3. Disable rp_filter
            // 4. Run tun2socks with primary interface binding

            // Check for root privileges
            if (!IsAdministrator())
            {
                Console.WriteLine("Warning: This application requires root privileges to configure network interfaces.");
                Console.WriteLine("Please run the application with sudo.");
                return;
            }
            
            // Get the primary network interface and gateway
            Console.WriteLine("Detecting primary network interface and gateway...");
            string primaryInterface = NetworkHelper.GetPrimaryInterfaceName();
            string primaryGateway = NetworkHelper.GetPrimaryGateway();
            
            Console.WriteLine($"Using {primaryInterface} as primary network interface");
            Console.WriteLine($"Using {primaryGateway} as primary gateway");

            // Create and configure TUN interface
            Console.WriteLine("Creating TUN interface...");
            ExecuteCommand("sudo", "ip tuntap add mode tun dev tun0");
            ExecuteCommand("sudo", "ip addr add 198.18.0.1/15 dev tun0");
            ExecuteCommand("sudo", "ip link set dev tun0 up");
            
            // Configure routing
            Console.WriteLine("Configuring routing...");
            ExecuteCommand("sudo", "ip route del default");
            ExecuteCommand("sudo", "ip route add default via 198.18.0.1 dev tun0 metric 1");
            ExecuteCommand("sudo", "ip route add default via " + primaryGateway + " dev " + primaryInterface + " metric 10");
            
            // Disable rp_filter to allow receiving packets from other interfaces
            Console.WriteLine("Configuring rp_filter settings...");
            ExecuteCommand("sudo", "sysctl net.ipv4.conf.all.rp_filter=0");
            ExecuteCommand("sudo", "sysctl net.ipv4.conf." + primaryInterface + ".rp_filter=0");

            // Prepare the tun2socks command
            string proxyAuth = string.Empty;
            if (!string.IsNullOrEmpty(_proxySettings.Username) && !string.IsNullOrEmpty(_proxySettings.Password))
            {
                proxyAuth = $"{_proxySettings.Username}:{_proxySettings.Password}@";
            }

            string proxyServer = $"{proxyAuth}{_proxySettings.IpAddress}:{_proxySettings.Port}";
            string arguments = $"-device tun0 -proxy socks5://{proxyServer} -interface {primaryInterface}";

            Console.WriteLine($"Starting tun2socks with command: {_tun2SocksPath} {arguments}");

            // Start the process
            var processInfo = new ProcessStartInfo
            {
                FileName = _tun2SocksPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = new Process
            {
                StartInfo = processInfo
            };

            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();

            process.OutputDataReceived += (sender, e) => 
            {
                if (e.Data != null)
                {
                    Console.WriteLine($"[tun2socks] {e.Data}");
                    output.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) => 
            {
                if (e.Data != null)
                {
                    Console.WriteLine($"[tun2socks ERROR] {e.Data}");
                    error.AppendLine(e.Data);
                }
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                Console.WriteLine("tun2socks is running. Press Ctrl+C to stop.");
                
                // Keep the process running
                await WaitForProcessExitAsync(process);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running tun2socks: {ex.Message}");
                if (output.Length > 0)
                    Console.WriteLine($"Output: {output}");
                if (error.Length > 0)
                    Console.WriteLine($"Error: {error}");
            }
        }

        private static Task WaitForProcessExitAsync(Process process)
        {
            var tcs = new TaskCompletionSource<bool>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => tcs.TrySetResult(true);
            if (process.HasExited)
                tcs.TrySetResult(true);
            return tcs.Task;
        }

        private void ExecuteCommand(string command, string arguments)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(output))
                    Console.WriteLine(output);
                
                if (!string.IsNullOrEmpty(error))
                    Console.WriteLine($"Error: {error}");

                Console.WriteLine($"Executed command: {command} {arguments}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing command: {ex.Message}");
            }
        }

        private bool IsAdministrator()
        {
            OsInfo.OsType osType = OsInfo.GetOperatingSystem();
            
            if (osType == OsInfo.OsType.Windows)
            {
#if WINDOWS
                // Check for admin rights in Windows
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
#else
                // Not running on Windows, can't check Windows admin rights
                Console.WriteLine("Unable to check for administrator rights on non-Windows platform.");
                return false;
#endif
            }
            else
            {
                // Check for root in Linux/macOS
                return Environment.UserName == "root";
            }
        }
    }
}
