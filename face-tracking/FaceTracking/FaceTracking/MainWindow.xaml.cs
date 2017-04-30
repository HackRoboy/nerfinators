//--------------------------------------------------------------------------------------
// Copyright 2015 Intel Corporation
// All Rights Reserved
//
// Permission is granted to use, copy, distribute and prepare derivative works of this
// software for any purpose and without fee, provided, that the above copyright notice
// and this statement appear in all copies.  Intel makes no representations about the
// suitability of this software for any purpose.  THIS SOFTWARE IS PROVIDED "AS IS."
// INTEL SPECIFICALLY DISCLAIMS ALL WARRANTIES, EXPRESS OR IMPLIED, AND ALL LIABILITY,
// INCLUDING CONSEQUENTIAL AND OTHER INDIRECT DAMAGES, FOR THE USE OF THIS SOFTWARE,
// INCLUDING LIABILITY FOR INFRINGEMENT OF ANY PROPRIETARY RIGHTS, AND INCLUDING THE
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE.  Intel does not
// assume any responsibility for any errors which may appear in this software nor any
// responsibility to update it.
//--------------------------------------------------------------------------------------
using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Windows.Controls;
using System.IO.Ports;
using System.Net.Sockets;
using System.Net;
using System.Text;
using Rosbridge.Client;


namespace FaceTracking
{
    public partial class MainWindow : Window
    {
        private PXCMSession session;
        private PXCMSenseManager senseManager;
        private PXCMFaceData faceData;
        private Thread update;
        private string alertMsg;
        private SerialPort serialPort;
        private int angleValue;
        private string action; // 2 to SHOOT, 1 for LED_ON, 0 for LED_OFF
        private MessageDispatcher md;
        private Subscriber subscriber;
        private Publisher publisher;

        /**
         * width of the color stream 
        */
        public static int cWidth = 640;

        /**
         * height of the color stream
         */
        public static int cHeight = 480;

        /**
         * vertical field of view of the realsense
         */
        public static double vfov = 43;

        /**
         * horizontal field of view of the realsense
         */
        public static double hfov = 70;


        public Int32 faceH;
        public Int32 faceW;
        public Int32 faceX;
        public Int32 faceY;

        public MainWindow()
        {
            InitializeComponent();

            ConfigureROSBridge();
            // Start SenseManager and configure the face module
            ConfigureRealSense();
            

            // Start the Update thread
            update = new Thread(new ThreadStart(Update));
            update.Start();
        }

        private async void ConfigureROSBridge()
        {
            // for Rosbridge

            // Start the message dispatcher
            md = new MessageDispatcher(new Rosbridge.Client.Socket(new Uri("ws://10.183.126.141:9090")), new MessageSerializerV2_0());
            await md.StartAsync();


            // Publish to a topic
            publisher = new Publisher("/balloonPosition", "geometry_msgs/Point32", md);
            await publisher.AdvertiseAsync();
            await publisher.PublishAsync(new { x = 2.2, y = 5.5, z = 3.2 });
            Console.Write(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> sent message");

            // Clean-up
            //await subscriber.UnsubscribeAsync();
            //await publisher.UnadvertiseAsync();
            //await md.StopAsync();
        }

        private void ConfigureRealSense()
        {
            PXCMFaceModule faceModule;
            PXCMFaceConfiguration faceConfig;

            // Start the SenseManager and session  
            session = PXCMSession.CreateInstance();
            senseManager = session.CreateSenseManager();

            // Enable the color stream
            senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_COLOR, 640, 480, 30);

            // Enable the face module
            senseManager.EnableFace();
            faceModule = senseManager.QueryFace();
            faceConfig = faceModule.CreateActiveConfiguration();

            // Configure for 3D face tracking
            faceConfig.SetTrackingMode(PXCMFaceConfiguration.TrackingModeType.FACE_MODE_COLOR_PLUS_DEPTH);

            // Known issue: Pose isEnabled must be set to false for R200 face tracking to work correctly
            faceConfig.pose.isEnabled = false;

            // Track faces based on their appearance in the scene
            faceConfig.strategy = PXCMFaceConfiguration.TrackingStrategyType.STRATEGY_APPEARANCE_TIME;
            
            // Set the module to track four faces in this example
            faceConfig.detection.maxTrackedFaces = 4;

            // Enable alert monitoring and subscribe to alert event hander
            faceConfig.EnableAllAlerts();
            faceConfig.SubscribeAlert(FaceAlertHandler);

            // Apply changes and initialize
            faceConfig.ApplyChanges();
            senseManager.Init();
            faceData = faceModule.CreateOutput();   

            // Mirror the image
            senseManager.QueryCaptureManager().QueryDevice().SetMirrorMode(PXCMCapture.Device.MirrorMode.MIRROR_MODE_HORIZONTAL);

            // Release resources
            faceConfig.Dispose();
            faceModule.Dispose();

            // For ports
            try
            {
                // initialize serial ports
                string[] ports = SerialPort.GetPortNames();
                if (ports.Length > 0)
                {

                    Console.WriteLine(">>>>>>>>>Found port " + ports[0]);
                    serialPort = new SerialPort(ports[0], 9600);
                    Console.WriteLine(">>>>>>>>Serialport isopen" + serialPort.IsOpen);
                    Console.WriteLine(">>>>>>>Port Opened");
                    if (!serialPort.IsOpen)
                    {
                        serialPort.Open();
                        // test to open a serial port



                    }
                }
                else
                {
                    Console.WriteLine(">>>>>>No port");
                    serialPort = default(SerialPort);
                }
            }catch
            {
                Console.Write("No port found");
            }


          

        }

        private void FaceAlertHandler(PXCMFaceData.AlertData alert)
        {
            alertMsg = Convert.ToString(alert.label);

            Console.Write(">>>>>>>>"+alertMsg);

            if(alertMsg == "ALERT_NEW_FACE_DETECTED")
            {
                action = "2"; // shoot
                writeToSerialPort(action);
                sendROSCommand(action);
                createAndSendUDP(action);

            }

        }

        private async void sendROSCommand(string action)
        {
            // compute angle relative to target.
            double width = (double)cWidth;
            double height = (double)cHeight;
            double faceWidth = (double)faceW;
            double faceHeight = (double)faceH;

            double azimuth = (hfov / 2) * (faceX + faceWidth / 2 - width / 2) / (width / 2);
            double elevation = -(vfov / 2) * (faceY + faceHeight / 2 - height / 2) / (height / 2);

            Console.WriteLine("Azimuth: " + azimuth);
            Console.WriteLine("Elevation: " + elevation);

            await publisher.AdvertiseAsync();
            await publisher.PublishAsync(new { x = azimuth, y = elevation, z = 3.2 });
            Console.Write(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> sent message");

        }

        private void createAndSendUDP(String action)
        {

            System.Net.Sockets.Socket sock = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Dgram,ProtocolType.Udp);
            // send shooting command to nerf gun
            IPAddress serverAddr = IPAddress.Parse("192.168.1.142");
      
            IPEndPoint endPoint = new IPEndPoint(serverAddr, 4210);
            string text = action;
            byte[] send_buffer = Encoding.ASCII.GetBytes(text);
            sock.SendTo(send_buffer, endPoint);
            Console.Write("Sent to UDP value:"+action);

        }

        

        private void writeToSerialPort(String action)
        {

            if (serialPort.IsOpen)
            {
                try
                {
                    // serialPort.Open();
                    serialPort.Write(action);
                    Console.WriteLine(">>>>>>>Written to port action:"+ action);

                }
                catch
                {
                    MessageBox.Show("There was an error. Please make sure that the correct port was selected, and the device, plugged in.");
                }
            }

        }

        private void Update()
        {
            Int32 facesDetected = 0;
            /* Int32 faceH = 0;
            Int32 faceW = 0;
            Int32 faceX = 0;
            Int32 faceY = 0;
            */
            float faceDepth = 0;
            String json = "";

            // Start AcquireFrame-ReleaseFrame loop
            while (senseManager.AcquireFrame(true) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                // Acquire color image data
                PXCMCapture.Sample sample = senseManager.QuerySample();
                Bitmap colorBitmap;
                PXCMImage.ImageData colorData;
                sample.color.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB32, out colorData);
                colorBitmap = colorData.ToBitmap(0, sample.color.info.width, sample.color.info.height);

                // Acquire face data
                if (faceData != null)
                {
                    faceData.Update();
                    facesDetected = faceData.QueryNumberOfDetectedFaces();

                    if (facesDetected > 0)
                    {
                        // Get the first face detected (index 0)
                        PXCMFaceData.Face face = faceData.QueryFaceByIndex(0);

                        // Retrieve face location data
                        PXCMFaceData.DetectionData faceDetectionData = face.QueryDetection();
                        if (faceDetectionData != null)
                        {
                            PXCMRectI32 faceRectangle;
                            faceDetectionData.QueryBoundingRect(out faceRectangle);
                            faceH = faceRectangle.h;
                            faceW = faceRectangle.w;
                            faceX = faceRectangle.x;
                            faceY = faceRectangle.y;

                            // Get average depth value of detected face
                            faceDetectionData.QueryFaceAverageDepth(out faceDepth);

                            //Console.WriteLine(">>>>>>>Face detected");
                            
                         

                        }
                    }
                }

                // Update UI
                Render(colorBitmap, facesDetected, faceH, faceW, faceX, faceY, faceDepth);




                // Release the color frame
                colorBitmap.Dispose();
                sample.color.ReleaseAccess(colorData);
                senseManager.ReleaseFrame();
            }
        }

        private void Render(Bitmap bitmap, Int32 count, Int32 h, Int32 w, Int32 x, Int32 y, float depth)
        {
            BitmapImage bitmapImage = ConvertBitmap(bitmap);

            if (bitmapImage != null)
            {
                // Update the UI controls
                this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate()
                {
                    // Update the bitmap image
                    imgStream.Source = bitmapImage;

                    // Update the data labels
                    lblFacesDetected.Content = string.Format("Faces Detected: {0}", count);
                    lblFaceH.Content = string.Format("Face Height: {0}", h);
                    lblFaceW.Content = string.Format("Face Width: {0}", w);
                    lblFaceX.Content = string.Format("Face X Coord: {0}", x);
                    lblFaceY.Content = string.Format("Face Y Coord: {0}", y);
                    lblFaceDepth.Content = string.Format("Face Depth: {0}", depth);
                    lblFaceAlert.Content = string.Format("Last Alert: {0}", alertMsg);

                    // Show or hide the face marker
                    if (count > 0)
                    {
                        // Show face marker
                        rectFaceMarker.Height = h;
                        rectFaceMarker.Width = w;
                        Canvas.SetLeft(rectFaceMarker, x);
                        Canvas.SetTop(rectFaceMarker, y);
                        rectFaceMarker.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        // Hide the face marker
                        rectFaceMarker.Visibility = Visibility.Hidden;
                    }
                }));
            }
        }

        private BitmapImage ConvertBitmap(Bitmap bitmap)
        {
            BitmapImage bitmapImage = null;

            if (bitmap != null)
            {
                MemoryStream memoryStream = new MemoryStream();
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Bmp);
                memoryStream.Position = 0;
                bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
            }

            return bitmapImage;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ShutDown();
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            ShutDown();
            this.Close();
        }

        private void ShutDown()
        {
            // Stop the Update thread
            update.Abort();

            // Dispose RealSense objects
            faceData.Dispose();
            senseManager.Dispose();
            session.Dispose();
        }
    }
}
