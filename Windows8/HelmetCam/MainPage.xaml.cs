using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using System.Runtime.InteropServices.WindowsRuntime;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace HelmetCam
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void OnConnected()
        {
            VisualStateManager.GoToState(this, "Connected", false);

            RpcClient rpcClient = new RpcClient(serviceBaseAddress);

            var liveViewUrl = await rpcClient.CameraMethod<string>("startLiveview");

            Uri liveViewUri = new Uri(liveViewUrl);

            CancellationTokenSource cts = new CancellationTokenSource();

            CancellationToken ct = cts.Token;

            IProgress<byte[]> liveViewProgress = new Progress<byte[]>(async (frameBytes) =>
            {
                using (var jpegStream = new InMemoryRandomAccessStream())
                {
                    await jpegStream.WriteAsync(frameBytes.AsBuffer());
                    await jpegStream.FlushAsync();

                    jpegStream.Seek(0);

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        var jpegImage = new BitmapImage();

                        await jpegImage.SetSourceAsync(jpegStream);

                        CameraScreen.Source = jpegImage;
                    });
                }
            });

            // Start a background LiveView task  
            Task receiving = LiveViewClient.LiveViewAsync(liveViewUri, ct, liveViewProgress);
        }


        private static string ssdpIp = "239.255.255.250";
        private static string ssdpPort = "1900";
        private static string ssdpQuery = "M-SEARCH * HTTP/1.1\r\n" +
                                            "HOST: " + ssdpIp + ":" + ssdpPort + "\r\n" +
                                            "MAN: \"ssdp:discover\"\r\n" +
                                            "MX: 3\r\n" +
                                            "ST: urn:schemas-sony-com:service:ScalarWebAPI:1\r\n" +
                                            "\r\n";

        private async void ConnectWifi()
        {
            var remoteIP = new Windows.Networking.HostName(ssdpIp);
            var reqBuff = Encoding.UTF8.GetBytes(ssdpQuery);

            using (var socket = new Windows.Networking.Sockets.DatagramSocket())
            {
                socket.MessageReceived += (socketSource, args) =>
                {
                    // This is invoked for each device that responds to the query...
                    Task.Run(async () =>
                    {
                        using (var reader = args.GetDataReader())
                        {
                            byte[] respBuff = new byte[reader.UnconsumedBufferLength];
                            reader.ReadBytes(respBuff);
                            string response = Encoding.UTF8.GetString(respBuff, 0, respBuff.Length);

                            string[] stringSeparators = new string[] { "\r\n", "\n" };

                            var descriptionUri = (from line in response.Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries)
                                                  where line.ToLowerInvariant().StartsWith("location:")
                                                  select new Uri(line.Substring(9).Trim())).FirstOrDefault();

                            XmlDocument descriptionDoc = await XmlDocument.LoadFromUriAsync(descriptionUri);

                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, OnConnected);
                        }
                    });
                };

                await socket.BindEndpointAsync(null, "");

                socket.JoinMulticastGroup(remoteIP);

                using (var stream = await socket.GetOutputStreamAsync(remoteIP, ssdpPort))
                {
                    await stream.WriteAsync(reqBuff.AsBuffer());
                }

                await Task.Delay(3000);
            }
        }

        Uri serviceBaseAddress = new Uri("http://10.0.0.1:10000/sony/");

        private async void VideoButton_Click(object sender, RoutedEventArgs e)
        {
            RpcClient rpcClient = new RpcClient(serviceBaseAddress);

            int result = await rpcClient.CameraMethod<int>("setShootMode", "movie");
        }

        private async void StillsButton_Click(object sender, RoutedEventArgs e)
        {
            RpcClient rpcClient = new RpcClient(serviceBaseAddress);

            int result = await rpcClient.CameraMethod<int>("setShootMode", "still");
        }

        private async void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            RpcClient rpcClient = new RpcClient(serviceBaseAddress);

            int result = await rpcClient.CameraMethod<int>("startMovieRec");

            VisualStateManager.GoToState(this, "Recording", false);
        }

        private async void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            RpcClient rpcClient = new RpcClient(serviceBaseAddress);

            string thumbnailUrl = await rpcClient.CameraMethod<string>("stopMovieRec");

            VisualStateManager.GoToState(this, "Idle", false);
        }

        private void WifiButton_Click(object sender, RoutedEventArgs e)
        {
            ConnectWifi();
        }
    }
}
