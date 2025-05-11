## Sim

Create a reverse proxy:

```bash
adb reverse tcp:5810 tcp:5810
```

Remove the reverse proxy:

```bash
adb reverse --remove tcp:5810
```

Test with curl:

```bash
adb push curl /data/local/tmp/
adb shell chmod 755 /data/local/tmp/curl
adb shell /data/local/tmp/curl 127.0.0.1:5810
```



Test with websocat:

```bash
adb push websocat /data/local/tmp/websocat
adb shell chmod 755 /data/local/tmp/websocat
adb shell /data/local/tmp/websocat ws://127.0.0.1:5810
```