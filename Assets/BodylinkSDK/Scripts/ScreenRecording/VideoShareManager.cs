using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using ZXing;
using ZXing.QrCode;
namespace BodylinkSDK
{
    public class VideoShareManager : MonoBehaviour
    {
        private int preferredPort = 8080;
        private int maxPortAttempts = 20;

        private int qrPixelSize = 320;

        private readonly object stateLock = new object();
        private TcpListener tcpListener;
        private Thread serverThread;
        private volatile bool serverRunning;

        private string sharedVideoPath;
        private string currentShareUrl;
        private string currentDownloadRoute;
        private int currentPort;
        private Texture2D qrTexture;

        public string CurrentShareUrl => currentShareUrl;
        public Texture2D CurrentQrTexture => qrTexture;

        public bool ShareVideo(string videoPath, UnityAction<Texture2D, string> onShareReady)
        {
            if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            {
                Debug.LogWarning($"ShareVideo failed. File does not exist: {videoPath}");
                onShareReady?.Invoke(null, null);
                return false;
            }

            string localIp = GetLocalIpv4Address();
            if (string.IsNullOrEmpty(localIp))
            {
                Debug.LogWarning("ShareVideo failed. Could not detect local IPv4 address.");
                onShareReady?.Invoke(null, null);
                return false;
            }

            string videoName = Path.GetFileName(videoPath);
            currentDownloadRoute = Uri.EscapeDataString(videoName);

            if (!StartServer(videoPath, out int selectedPort))
            {
                Debug.LogWarning("ShareVideo failed. Could not start local server.");
                currentDownloadRoute = null;
                onShareReady?.Invoke(null, null);
                return false;
            }

            currentPort = selectedPort;
            currentShareUrl = $"http://{localIp}:{currentPort}/{currentDownloadRoute}";

            Texture2D generatedQr = null;
            try
            {
                generatedQr = GenerateQrTexture(currentShareUrl);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"QR generation failed: {ex.Message}");
            }

            SetQrTexture(generatedQr);
            onShareReady?.Invoke(generatedQr, currentShareUrl);

            Debug.Log($"Video sharing started: {currentShareUrl} -> {videoPath}");
            return true;
        }

        public void StopSharing()
        {
            StopServer();
            currentShareUrl = null;
            currentDownloadRoute = null;
            currentPort = 0;
            lock (stateLock)
            {
                sharedVideoPath = null;
            }
            SetQrTexture(null);
        }

        private bool StartServer(string videoPath, out int selectedPort)
        {
            StopServer();

            selectedPort = -1;
            lock (stateLock)
            {
                sharedVideoPath = videoPath;
            }

            for (int offset = 0; offset < maxPortAttempts; offset++)
            {
                int candidatePort = preferredPort + offset;
                try
                {
                    tcpListener = new TcpListener(IPAddress.Any, candidatePort);
                    tcpListener.Start(10);
                    serverRunning = true;

                    serverThread = new Thread(ServerLoop)
                    {
                        IsBackground = true,
                        Name = "VideoShareServerThread"
                    };
                    serverThread.Start();

                    selectedPort = candidatePort;
                    return true;
                }
                catch (SocketException)
                {
                    tcpListener = null;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to start sharing server on port {candidatePort}: {ex.Message}");
                    tcpListener = null;
                }
            }

            serverRunning = false;
            return false;
        }

        private void StopServer()
        {
            serverRunning = false;

            if (tcpListener != null)
            {
                try
                {
                    tcpListener.Stop();
                }
                catch
                {
                    // Ignore shutdown errors.
                }
                tcpListener = null;
            }

            if (serverThread != null)
            {
                try
                {
                    if (serverThread.IsAlive)
                        serverThread.Join(200);
                }
                catch
                {
                    // Ignore shutdown errors.
                }
                serverThread = null;
            }
        }

        private void ServerLoop()
        {
            while (serverRunning)
            {
                TcpClient client = null;
                try
                {
                    client = tcpListener.AcceptTcpClient();
                    HandleClient(client);
                }
                catch (SocketException)
                {
                    if (!serverRunning)
                        break;
                }
                catch (ObjectDisposedException)
                {
                    if (!serverRunning)
                        break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Share server client error: {ex.Message}");
                }
                finally
                {
                    if (client != null)
                    {
                        try { client.Close(); } catch { }
                    }
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            client.ReceiveTimeout = 5000;
            client.SendTimeout = 5000;

            using (NetworkStream stream = client.GetStream())
            {
                string requestText = ReadRequest(stream);
                if (string.IsNullOrEmpty(requestText))
                    return;

                string[] lines = requestText.Split(new[] { "\r\n" }, StringSplitOptions.None);
                if (lines.Length == 0)
                {
                    SendTextResponse(stream, "400 Bad Request", "text/plain", "Bad Request");
                    return;
                }

                string[] requestLine = lines[0].Split(' ');
                if (requestLine.Length < 2)
                {
                    SendTextResponse(stream, "400 Bad Request", "text/plain", "Bad Request");
                    return;
                }

                string method = requestLine[0].ToUpperInvariant();
                string path = requestLine[1];

                if (method != "GET")
                {
                    SendTextResponse(stream, "405 Method Not Allowed", "text/plain", "Method Not Allowed");
                    return;
                }

                if (path == "/" || path.StartsWith("/index", StringComparison.OrdinalIgnoreCase))
                {
                    SendIndexResponse(stream);
                    return;
                }

                string expectedRoute = "/" + currentDownloadRoute;
                if (path.StartsWith(expectedRoute, StringComparison.OrdinalIgnoreCase))
                {
                    SendVideoResponse(stream);
                    return;
                }

                SendTextResponse(stream, "404 Not Found", "text/plain", "Not Found");
            }
        }

        private static string ReadRequest(NetworkStream stream)
        {
            byte[] buffer = new byte[8192];
            int totalRead = 0;

            while (totalRead < buffer.Length)
            {
                int bytesRead = stream.Read(buffer, totalRead, buffer.Length - totalRead);
                if (bytesRead <= 0)
                    break;

                totalRead += bytesRead;

                if (totalRead >= 4)
                {
                    for (int i = 3; i < totalRead; i++)
                    {
                        if (buffer[i - 3] == '\r' && buffer[i - 2] == '\n' && buffer[i - 1] == '\r' && buffer[i] == '\n')
                        {
                            return Encoding.ASCII.GetString(buffer, 0, totalRead);
                        }
                    }
                }
            }

            return totalRead > 0 ? Encoding.ASCII.GetString(buffer, 0, totalRead) : null;
        }

        private void SendIndexResponse(NetworkStream stream)
        {
            string route = "/" + currentDownloadRoute;
            string html =
                "<!doctype html><html><head><meta charset='utf-8'><title>Video Share</title></head><body>" +
                "<h2>Video share server</h2>" +
                "<p><a href='" + route + "'>Download video</a></p>" +
                "</body></html>";

            SendTextResponse(stream, "200 OK", "text/html; charset=utf-8", html);
        }

        private void SendVideoResponse(NetworkStream stream)
        {
            string videoPath;
            lock (stateLock)
            {
                videoPath = sharedVideoPath;
            }

            if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            {
                SendTextResponse(stream, "404 Not Found", "text/plain", "Video file not found");
                return;
            }

            string fileName = Path.GetFileName(videoPath);

            try
            {
                using (FileStream fileStream = new FileStream(videoPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    StringBuilder headerBuilder = new StringBuilder();
                    headerBuilder.Append("HTTP/1.1 200 OK\r\n");
                    headerBuilder.Append("Content-Type: video/mp4\r\n");
                    headerBuilder.Append("Content-Length: ").Append(fileStream.Length).Append("\r\n");
                    headerBuilder.Append("Content-Disposition: attachment; filename=\"").Append(fileName).Append("\"\r\n");
                    headerBuilder.Append("Connection: close\r\n\r\n");

                    byte[] headerBytes = Encoding.UTF8.GetBytes(headerBuilder.ToString());
                    stream.Write(headerBytes, 0, headerBytes.Length);

                    byte[] copyBuffer = new byte[64 * 1024];
                    int read;
                    while ((read = fileStream.Read(copyBuffer, 0, copyBuffer.Length)) > 0)
                    {
                        stream.Write(copyBuffer, 0, read);
                    }
                }
            }
            catch (Exception ex)
            {
                SendTextResponse(stream, "500 Internal Server Error", "text/plain", "Failed to stream video: " + ex.Message);
            }
        }

        private static void SendTextResponse(NetworkStream stream, string status, string contentType, string body)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body ?? string.Empty);

            StringBuilder headerBuilder = new StringBuilder();
            headerBuilder.Append("HTTP/1.1 ").Append(status).Append("\r\n");
            headerBuilder.Append("Content-Type: ").Append(contentType).Append("\r\n");
            headerBuilder.Append("Content-Length: ").Append(bodyBytes.Length).Append("\r\n");
            headerBuilder.Append("Connection: close\r\n\r\n");

            byte[] headerBytes = Encoding.UTF8.GetBytes(headerBuilder.ToString());
            stream.Write(headerBytes, 0, headerBytes.Length);
            if (bodyBytes.Length > 0)
                stream.Write(bodyBytes, 0, bodyBytes.Length);
        }

        private string GetLocalIpv4Address()
        {
            string fallback = null;

            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                NetworkInterface networkInterface = interfaces[i];
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;

                UnicastIPAddressInformationCollection addresses = networkInterface.GetIPProperties().UnicastAddresses;
                foreach (UnicastIPAddressInformation unicastAddress in addresses)
                {
                    IPAddress address = unicastAddress.Address;
                    if (address.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    if (IPAddress.IsLoopback(address))
                        continue;

                    string ip = address.ToString();
                    if (IsPrivateIpv4(ip))
                        return ip;

                    if (string.IsNullOrEmpty(fallback))
                        fallback = ip;
                }
            }

            return fallback;
        }

        private static bool IsPrivateIpv4(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress))
                return false;

            string[] parts = ipAddress.Split('.');
            if (parts.Length != 4)
                return false;

            if (!int.TryParse(parts[0], out int first) || !int.TryParse(parts[1], out int second))
                return false;

            if (first == 10)
                return true;
            if (first == 192 && second == 168)
                return true;
            if (first == 172 && second >= 16 && second <= 31)
                return true;

            return false;
        }

        private static Color32[] EncodeQr(string textForEncoding, int width, int height)
        {
            BarcodeWriter writer = new BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = height,
                    Width = width
                }
            };

            return writer.Write(textForEncoding);
        }

        private Texture2D GenerateQrTexture(string text)
        {
            Texture2D encoded = new Texture2D(qrPixelSize, qrPixelSize, TextureFormat.RGBA32, false);
            Color32[] color32 = EncodeQr(text, encoded.width, encoded.height);
            encoded.SetPixels32(color32);
            encoded.Apply();
            return encoded;
        }

        private void SetQrTexture(Texture2D texture)
        {
            if (qrTexture != null)
                Destroy(qrTexture);

            qrTexture = texture;
        }

        private void OnDestroy()
        {
            StopSharing();
        }
    }
}