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

namespace FaceTracking
{
    public partial class MainWindow : Window
    {
        private PXCMSession session;
        private PXCMSenseManager senseManager;
        private PXCMFaceData faceData;
        private Thread update;
        private string alertMsg;

        public MainWindow()
        {
            InitializeComponent();

            // Start SenseManager and configure the face module
            ConfigureRealSense();

            // Start the Update thread
            update = new Thread(new ThreadStart(Update));
            update.Start();
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
        }

        private void FaceAlertHandler(PXCMFaceData.AlertData alert)
        {
            alertMsg = Convert.ToString(alert.label);
        }

        private void Update()
        {
            Int32 facesDetected = 0;
            Int32 faceH = 0;
            Int32 faceW = 0;
            Int32 faceX = 0;
            Int32 faceY = 0;
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
