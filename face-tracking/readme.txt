APPLICABLE LICENSES
See license.txt contained in this zip file.

DESCRIPTION
This C#/XAML code sample demonstrates the basics of using the face tracking algorithm in the Intel® RealSense™ SDK for Windows* to detect and track a person’s face in real time using the R200 camera. The code sample performs the following functions:
 - Displays the live color stream of the R200 RGB camera in an Image control
 - Superimposes a rectangle control that tracks the user’s face (based on his or her appearance in the scene)
 - Displays the number of faces detected by the R200 camera
 - Displays the height and width of the tracked face
 - Displays the 2D coordinates (X and Y) of the tracked face
 - Indicates the face depth or distance between the face and the R200 camera
 - Enables and displays alert monitoring and subscribes to an alert event handler

NOTES
1. This project uses an explicit path to libpxcclr.cs.dll (the managed RealSense DLL): C:\Program Files (x86)\Intel\RSSDK\bin\x64. This reference will need to be changed if your installation path is different.

Prepared by: Bryan Brown
http://software.intel.com/realsense

*Other names and brands may be claimed as the property of others
© 2015 Intel Corporation.
