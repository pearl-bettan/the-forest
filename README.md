# The Forest
The Forest of Knowledge Game

Build and deploy
----------------
1. select WEB platform in 'File -> Build Profiles'
2. In 'Edit - > Project Settings -> Player Settings' 
2.1 In 'Resolution and Presentation' 
2.1.1 Set Resolution to '1280x720'
2.1.2 Enable 'Run in Background'
2.2 In 'Publishing Settings'
2.2.1 Set 'Compression Format' to 'Disabled'
2.2.2 Disable 'Data Caching'
2. Build 
3. Copy the content of output folder to 'Game' folder(In the main index.html you can define the path to the build <iframe src="Game/index.html" width="1280" height="720" scrolling="no"></iframe>)