using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using HelmetCam.Resources;

using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Windows.Media.Imaging;

using Windows.Storage.Streams;
using System.IO;

namespace HelmetCam
{
    public partial class MainPage : PhoneApplicationPage
    {
        // Constructor
        public MainPage()
        {
            InitializeComponent();

            // Sample code to localize the ApplicationBar
            //BuildLocalizedApplicationBar();
        }

        private void WifiButton_Click(object sender, EventArgs e)
        {
            ConnectWifi();
        }

        private async void OnConnected()
        {
            VisualStateManager.GoToState(this, "Connected", false);

            RpcClient rpcClient = new RpcClient(serviceBaseAddress);

            var liveViewUrl = await rpcClient.CameraMethod<string>("startLiveview");

            Uri liveViewUri = new Uri(liveViewUrl);

            CancellationTokenSource cts = new CancellationTokenSource();

            CancellationToken ct = cts.Token;

            IProgress<byte[]> liveViewProgress = new Progress<byte[]>((frameBytes) =>
            {
                var jpegStream = new MemoryStream(frameBytes);
                //using ()
                //{
                    Dispatcher.BeginInvoke(() =>
                    {
                        var jpegImage = new BitmapImage();

                        jpegImage.SetSource(jpegStream);

                        CameraScreen.Source = jpegImage;
                    });
                //}
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
                    Task.Run(() =>
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

                            Dispatcher.BeginInvoke(OnConnected);
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

        // Sample code for building a localized ApplicationBar
        //private void BuildLocalizedApplicationBar()
        //{
        //    // Set the page's ApplicationBar to a new instance of ApplicationBar.
        //    ApplicationBar = new ApplicationBar();

        //    // Create a new button and set the text value to the localized string from AppResources.
        //    ApplicationBarIconButton appBarButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/appbar.add.rest.png", UriKind.Relative));
        //    appBarButton.Text = AppResources.AppBarButtonText;
        //    ApplicationBar.Buttons.Add(appBarButton);

        //    // Create a new menu item with the localized string from AppResources.
        //    ApplicationBarMenuItem appBarMenuItem = new ApplicationBarMenuItem(AppResources.AppBarMenuItemText);
        //    ApplicationBar.MenuItems.Add(appBarMenuItem);
        //}
    }
}