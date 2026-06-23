using System.IO.Compression;
using System.Net.Http;

namespace SocksToVpn
{
    public class Downloader
    {
        private readonly HttpClient _httpClient;
        private readonly string _appDirectory;

        public Downloader()
        {
            _httpClient = new HttpClient();
            _appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        }

        public async Task<string> DownloadTun2SocksAsync()
        {
            string executableName = OsInfo.GetTun2SocksExecutableName();
            string executablePath = Path.Combine(_appDirectory, executableName);

            // Check if the file already exists
            if (File.Exists(executablePath))
            {
                Console.WriteLine($"tun2socks already exists at: {executablePath}");
                return executablePath;
            }

            string downloadUrl = OsInfo.GetTun2SocksDownloadUrl();
            string zipFilePath = Path.Combine(_appDirectory, "tun2socks.zip");
            string extractPath = Path.Combine(_appDirectory, "tun2socks_extract");
            
            Console.WriteLine($"Downloading tun2socks from: {downloadUrl}");

            try
            {
                // Download the zip file
                byte[] zipBytes = await _httpClient.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(zipFilePath, zipBytes);
                Console.WriteLine("Downloaded tun2socks zip file");

                // Extract the zip file
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }
                Directory.CreateDirectory(extractPath);
                
                ZipFile.ExtractToDirectory(zipFilePath, extractPath);
                Console.WriteLine("Extracted tun2socks zip file");

                // Find the executable in the extracted files
                string[] extractedFiles = Directory.GetFiles(extractPath);
                string extractedExecutablePath = string.Empty;
                
                // Look for the executable based on OS
                if (OsInfo.GetOperatingSystem() == OsInfo.OsType.Windows)
                {
                    extractedExecutablePath = extractedFiles.FirstOrDefault(f => f.EndsWith(".exe"));
                }
                else
                {
                    // For non-Windows, find the file that matches the OS pattern
                    string osPattern = OsInfo.GetOperatingSystem() == OsInfo.OsType.MacOS ? "darwin" : "linux";
                    string archPattern = OsInfo.GetArchitecture() == OsInfo.ArchitectureType.Arm64 ? "arm64" : "amd64";
                    
                    extractedExecutablePath = extractedFiles.FirstOrDefault(f => f.Contains(osPattern) && f.Contains(archPattern));
                }

                if (string.IsNullOrEmpty(extractedExecutablePath))
                {
                    throw new FileNotFoundException("Could not find the tun2socks executable in extracted files");
                }

                // Copy to destination with the appropriate name
                File.Copy(extractedExecutablePath, executablePath, true);
                
                // Set executable permissions on Unix systems
                if (OsInfo.GetOperatingSystem() != OsInfo.OsType.Windows)
                {
                    SetExecutablePermission(executablePath);
                }

                // Clean up temporary files
                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                }
                
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }

                Console.WriteLine($"Successfully extracted tun2socks to: {executablePath}");
                return executablePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading or extracting tun2socks: {ex.Message}");
                throw;
            }
        }

        public async Task<string> DownloadAndExtractWintunAsync()
        {
            if (OsInfo.GetOperatingSystem() != OsInfo.OsType.Windows)
            {
                return string.Empty; // Not needed for non-Windows systems
            }

            const string wintunUrl = "https://www.wintun.net/builds/wintun-0.14.1.zip";
            string wintunZipPath = Path.Combine(_appDirectory, "wintun.zip");
            string extractPath = Path.Combine(_appDirectory, "wintun_extract");
            string wintunDllDestination = Path.Combine(_appDirectory, "wintun.dll");

            // Check if the Wintun DLL already exists in the app directory
            if (File.Exists(wintunDllDestination))
            {
                Console.WriteLine($"Wintun already exists at: {wintunDllDestination}");
                return wintunDllDestination;
            }

            Console.WriteLine($"Downloading Wintun from: {wintunUrl}");

            try
            {
                // Download the Wintun ZIP file
                byte[] zipBytes = await _httpClient.GetByteArrayAsync(wintunUrl);
                await File.WriteAllBytesAsync(wintunZipPath, zipBytes);

                // Extract the ZIP file
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }
                Directory.CreateDirectory(extractPath);
                
                ZipFile.ExtractToDirectory(wintunZipPath, extractPath);
                Console.WriteLine("Extracted Wintun ZIP file");

                // Get the appropriate DLL based on architecture
                string sourceDllPath = OsInfo.GetWintunDllPath(extractPath);
                
                if (!File.Exists(sourceDllPath))
                {
                    throw new FileNotFoundException($"Could not find Wintun DLL at: {sourceDllPath}");
                }

                // Copy the DLL to the application directory
                File.Copy(sourceDllPath, wintunDllDestination, true);
                
                Console.WriteLine($"Copied Wintun DLL to: {wintunDllDestination}");

                // Clean up temporary files
                if (File.Exists(wintunZipPath))
                {
                    File.Delete(wintunZipPath);
                }
                
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }

                return wintunDllDestination;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading or extracting Wintun: {ex.Message}");
                throw;
            }
        }

        private void SetExecutablePermission(string filePath)
        {
            try
            {
                // For Linux/macOS, we need to use chmod to make the file executable
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{filePath}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();
                Console.WriteLine("Set executable permission on file");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not set executable permissions: {ex.Message}");
                Console.WriteLine("You may need to manually make the file executable with: chmod +x <file>");
            }
        }
    }
}
