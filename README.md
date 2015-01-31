# StorjVirtualDisk

# Introduction
Storj Virtual Disk for Windows is a proof of concept to demonstrate it is possible to mount a virtual drive to connect to Storj, a decentralized data storage solution.
The current state of the application is not optimized and only allows uploading of small sized files (< 1 Mb).

# Features and impressions

1 - Tray network notifications

![Alt text](/../screenshots/01 - TrayApplication.PNG?raw=true "Tray network notifications")
![Alt text](/../screenshots/05 - Network sync indication.PNG?raw=true "Network sync indication")

2 - A virtual disk accesable in Explorer

![Alt text](/../screenshots/02 - Drive in Explorer.PNG?raw=true "A virtual disk accesable in Explorer")

3 - Files and folders on decentralized data storage platform visible in Explorer

![Alt text](/../screenshots/03 - Files in Explorer.PNG?raw=true "Files and folders on decentralized data storage platform visible in Explorer")

4 - Support for navigating, copying and deleting of files on the decentralized data storage platform

![Alt text](/../screenshots/04 - Copying - Uploading.PNG?raw=true "Support for navigating, copying and deleting of files on the decentralized data storage platform")


For a video demo look at: http://youtu.be/LJcl4P_FXDQ

# Installation and requirements
The application uses a third part virtual disk driver called Dokan. To install teh application follow teh following steps:
1) Install Dokan by running the setup from install\DokanInstall_0.6.0.exe (note for Windows 8 you must run the installer in compatibility mode)
2) Instal the Storj Virtual Disk by running the setup from install\setup.msi

# Mounting the virtual disk
After a successful installation, run the application by using the shortcuts from the desktop or start menu. After startup a notification icon should be visible in the tray and in Explorer the mounted virtual drive should be visible.