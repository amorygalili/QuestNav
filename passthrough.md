Yes — as of Horizon OS v74 (released around January 2025), Meta lets Unity apps access Quest 3/3S passthrough cameras via the new Passthrough Camera API. Here's what’s important:

---

## ✅ Can Unity Apps Access Passthrough Cameras?

### 1. **Official Support via Passthrough Camera API**

Meta officially launched the **Passthrough Camera API** (experimental/public in v74+), allowing developers to grab passthrough camera frames in Unity, Unreal, Native, etc. You must request a specific permission (`horizonos.permission.HEADSET_CAMERA`) and once granted, you can access raw RGB frames along with camera pose and metadata ([developers.meta.com][1], [UploadVR][2]).

### 2. **How It Works in Unity**

* Uses standard `WebCamTexture` API under the hood, which internally calls Android’s Camera2 API ([The Ghost Howls][3], [GitHub][4]).
* Unity sample project available on GitHub: **Unity-PassthroughCameraApiSamples**. Includes scenes showing how to get texture feed, do brightness estimation, object detection, shader effects, etc. ([GitHub][4]).

### 3. **Requirements & Known Limitations**

* Requires **Meta Quest 3 or 3S** running **Horizon OS v74 or newer** ([The Ghost Howls][3], [GitHub][4], [Wikipedia][5]).
* Unity versions tested: Unity 2022.3.58f1 and 6000.0.38f1 (Unity 6). Minor adjustments might be needed if using newer versions ([The Ghost Howls][3], [GitHub][4]).
* Limitations:

  * **Single camera** (left or right only) at a time.
  * **Max resolution** \~1280×960 at \~30 FPS.
  * \~40–60 ms latency delay between capture and texture ([GitHub][4], [UploadVR][2]).
  * Permission prompt is required at runtime.

### 4. **Before v74: No Raw Access**

Earlier (prior to Horizon OS v74), there was no developer access to raw passthrough frames—only Meta’s processed depth/mesh layers were available. Developers confirmed no camera feed via Unity or Unreal ([Meta Community Forums][6], [Meta Community Forums][7]). But that changed with v74.

---

## 🔧 How to Try It in Your Unity App

1. **Update your headset** to Horizon OS v74+ (should be available if you're on the latest firmware).
2. Use a supported **Unity version** (Meta suggests 2022.3.58f1 or Unity 6 builds).
3. Clone or import the **Unity-PassthroughCameraApiSamples** project as reference ([The Ghost Howls][3], [GitHub][4]).
4. Make sure to include **Meta MRUK package** (`com.meta.xr.mrutilitykit`) and add `horizonos.permission.HEADSET_CAMERA` to your Android manifest.
5. Use `WebCamTextureManager` prefab or similar to request permission and begin streaming camera texture.
6. Always check for permission and call `.Play()` after initialization; sample code even suggests waiting a frame to avoid issues ([The Ghost Howls][3], [GitHub][4]).

---

### 🧩 In Summary

* **Yes**, Quest 3 and 3S allow Unity apps to access passthrough camera frames—starting with Horizon OS v74.
* Access is via the new Passthrough Camera API—leveraging `WebCamTexture` and Camera2.
* Meta provides sample projects and documentation to help you get started.
* It's still labeled “experimental,” so consider performance and user privacy before going into production.

Let me know if you'd like steps or code examples for integrating it into your Unity project—happy to dive deeper!

[1]: https://developers.meta.com/horizon/blog/new-era-mixed-reality-passthrough-camera-api-machine-learning-computer-vision/?utm_source=chatgpt.com "Explore a New Era of Mixed Reality with the Passthrough Camera API"
[2]: https://www.uploadvr.com/quest-passthrough-camera-api-experimental-out-now/?utm_source=chatgpt.com "Quest Passthrough Camera API Out Now For Developers To Play With"
[3]: https://skarredghost.com/2025/03/17/how-to-camera-access-meta-quest-3-unity-6/?utm_source=chatgpt.com "How to start developing with camera access for Meta Quest 3 on ..."
[4]: https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples?utm_source=chatgpt.com "A Sample Project for Passthrough Camera API in Unity. - GitHub"
[5]: https://en.wikipedia.org/wiki/Meta_Horizon_OS_version_history?utm_source=chatgpt.com "Meta Horizon OS version history"
[6]: https://communityforums.atmeta.com/t5/Quest-Development/Passthrough-camera-data-is-not-available-on-Quest-3-developing/td-p/1105975/page/10?utm_source=chatgpt.com "Passthrough camera data is not available on Quest"
[7]: https://communityforums.atmeta.com/t5/Unity-Development/Can-I-access-Quest-3-s-front-camera/td-p/1126881?utm_source=chatgpt.com "Can I access Quest 3's front camera? - Meta Community Forums"
