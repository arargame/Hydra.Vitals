using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hydra.Vitals.Core;
using Hydra.Vitals.Models;

namespace Hydra.Vitals.Data
{
    public static class VitalDataSeeder
    {
        public static async Task SeedAsync(JsonDatabaseContext context)
        {
            var payload = await context.LoadAsync();

            if (payload.Projects == null || !payload.Projects.Any())
            {
                var blockedProject = new AppProject("Blocked", "com.arargames.blocked", ProjectPlatformType.MobileAndroid, "Fast-paced block breaker & arcade game for Android and Steam/Desktop")
                {
                    TargetStore = "Google Play",
                    Technologies = new List<string> { "MonoGame", "C#", ".NET 9-android", "OpenAL", "GooglePlayGames", "AdMob", "AndroidX" }
                };

                var paintTrekProject = new AppProject("PaintTrek", "com.arargames.painttrek", ProjectPlatformType.MobileAndroid, "Creative puzzle drawing & coloring adventure game")
                {
                    TargetStore = "Google Play",
                    Technologies = new List<string> { "MonoGame", "C#", ".NET 9-android", "Shared Library Architecture", "AdMob" }
                };

                var panzerLabProject = new AppProject("PanzerLab", "com.arargames.panzerlab", ProjectPlatformType.MobileAndroid, "Tank combat tactical game")
                {
                    TargetStore = "Google Play",
                    Technologies = new List<string> { "MonoGame", "C#", ".NET 9-android", "Box2D/Physics" }
                };

                payload.Projects = new List<AppProject> { blockedProject, paintTrekProject, panzerLabProject };
            }

            var blocked = payload.Projects.FirstOrDefault(p => p.Name == "Blocked") ?? payload.Projects.First();

            if (payload.Issues == null || !payload.Issues.Any())
            {
                payload.Issues = new List<VitalIssue>
                {
                    new VitalIssue(
                        "crash-spritefont-glyph",
                        "SpriteFont.GetGlyphIndexOrDefault - character cannot be resolved",
                        blocked.Id,
                        blocked.Name ?? "Blocked",
                        VitalType.Crash,
                        "ArgumentException (JavaProxyThrowable)",
                        VitalSeverity.Critical,
                        VitalStatus.FixedVerified
                    )
                    {
                        DetectedDate = new DateTime(2026, 7, 25),
                        ReportedVersion = "607250232 / 1.0.2026.07.25",
                        AffectedUsers = 218,
                        EventCount = 413,
                        TechnologiesInvolved = new List<string> { "MonoGame", "C#", "SpriteFont" },
                        SignatureFrames = new List<string>
                        {
                            "Microsoft.Xna.Framework.Graphics.SpriteFont.GetGlyphIndexOrDefault",
                            "Microsoft.Xna.Framework.Graphics.SpriteFont.MeasureString",
                            "Blocked.Shared.Screens.*.Draw"
                        },
                        RootCause = "Number formatting used CultureInfo.CurrentCulture. On French, Russian, Polish, Swedish and German locales, 'N0' emits non-breaking space (U+00A0) or narrow no-break space (U+202F) as thousands separator. Those characters were missing from SpriteFont, and MonoGame throws when no DefaultCharacter is set.",
                        FixApproach = "Three layers: UIHelper.EnsureDefaultCharacter sets fallback glyph; UIHelper.FormatNumber formats with InvariantCulture; UIHelper.SanitizeText maps U+00A0 and U+202F to normal space. Draw loops guarded with try/catch.",
                        LessonsLearned = "Never format numbers using CurrentCulture on MonoGame SpriteFont without setting DefaultCharacter and sanitizing non-breaking spaces.",
                        RelatedDoc = "SpriteFont_Glyph_Crash.md"
                    },

                    new VitalIssue(
                        "crash-windowbackground-npe",
                        "NPE - DrawableContainer / windowBackground",
                        blocked.Id,
                        blocked.Name ?? "Blocked",
                        VitalType.Crash,
                        "NullPointerException",
                        VitalSeverity.Low,
                        VitalStatus.FixedVerified
                    )
                    {
                        DetectedDate = new DateTime(2026, 7, 25),
                        ReportedVersion = "607250232",
                        AffectedUsers = 2,
                        EventCount = 2,
                        TechnologiesInvolved = new List<string> { "Android Theme", "AOSP Framework" },
                        Devices = new List<VitalDevice>
                        {
                            new VitalDevice("Samsung A54x", "Samsung", "14", 34),
                            new VitalDevice("Samsung M13", "Samsung", "16", 36)
                        },
                        SignatureFrames = new List<string> { "crc648913c1ab01aecbdf.Activity1.n_onCreate" },
                        RootCause = "Theme.NoTitleBar.Fullscreen inherits windowBackground that is a <selector> (DrawableContainer). DrawableContainerState.mDrawableFutures is not thread-safe (AOSP bug); mutating from another thread drops field to null and throws NPE.",
                        FixApproach = "Custom BlockedTheme overriding android:windowBackground with a flat color (ColorDrawable). DrawableContainer is bypassed completely.",
                        LessonsLearned = "Theme windowBackground must never be a <selector>, <level-list> or <animation-list>. Flat color or <layer-list> must be used.",
                        RelatedDoc = "WindowBackground_Crash.md"
                    },

                    new VitalIssue(
                        "crash-content-container",
                        "Window couldn't find content container view",
                        blocked.Id,
                        blocked.Name ?? "Blocked",
                        VitalType.Crash,
                        "RuntimeException",
                        VitalSeverity.High,
                        VitalStatus.Mitigated
                    )
                    {
                        DetectedDate = new DateTime(2026, 7, 25),
                        ReportedVersion = "607250232",
                        AffectedUsers = 181,
                        EventCount = 209,
                        TechnologiesInvolved = new List<string> { "Android PhoneWindow", "OEM Theming/RRO" },
                        SignatureFrames = new List<string> { "android.view.Window.findViewById", "crc648913c1ab01aecbdf.Activity1.n_onCreate" },
                        RootCause = "Third-party theme / Samsung Theme Store / custom ROM overriding framework resources via RRO overlay causing PhoneWindow.generateLayout() to fail finding @android:id/content.",
                        FixApproach = "Staged recovery ladder in Activity1: in-place decor layout recovery first, then staged restart with progressively simpler fallback theme.",
                        LessonsLearned = "Verify mitigation in the field. When theme parent was made identical to fallback theme in a commit, mitigation silently broke until restored.",
                        RelatedDoc = "ContentContainer_Crash.md"
                    },

                    new VitalIssue(
                        "crash-touchpanel-teardown-npe",
                        "TouchPanel.AddEvent - NullReferenceException on Activity Teardown",
                        blocked.Id,
                        blocked.Name ?? "Blocked",
                        VitalType.Crash,
                        "NullReferenceException (JavaProxyThrowable)",
                        VitalSeverity.High,
                        VitalStatus.FixedAwaitingRelease
                    )
                    {
                        DetectedDate = new DateTime(2026, 8, 23),
                        ReportedVersion = "608220526",
                        AffectedUsers = 1,
                        EventCount = 1,
                        TechnologiesInvolved = new List<string> { "MonoGame TouchPanel", "Android ViewRootImpl", "Android 16" },
                        Devices = new List<VitalDevice>
                        {
                            new VitalDevice("Redmi Redmi A3", "Xiaomi/Redmi", "16", 36, "Android 16 Relaunch Teardown")
                        },
                        SignatureFrames = new List<string>
                        {
                            "Microsoft.Xna.Framework.Input.Touch.TouchPanel.AddEvent",
                            "Microsoft.Xna.Framework.Input.Touch.AndroidTouchEventManager.OnTouchEvent",
                            "Microsoft.Xna.Framework.MonoGameAndroidGameView.onTouch",
                            "android.view.ViewGroup.cancelAndClearTouchTargets",
                            "android.view.ViewRootImpl.die"
                        },
                        RootCause = "During activity destruction/relaunch, ViewRootImpl dispatches a final touch cancel event. MonoGame's TouchPanel processes it after game/touch subsystem disposal, throwing NullReferenceException.",
                        FixApproach = "Added DispatchTouchEvent override in Activity1 that consumes touches when IsDestroyed/IsFinishing is true with try/catch guard, and cleared touch listeners in OnDestroy.",
                        RelatedDoc = "TouchPanel_Teardown_Crash.md"
                    },

                    new VitalIssue(
                        "anr-mainthread-lock-contention",
                        "Main-thread native lock contention (MonoGame pause/resume handshake)",
                        blocked.Id,
                        blocked.Name ?? "Blocked",
                        VitalType.ANR,
                        "Native lock contention",
                        VitalSeverity.Low,
                        VitalStatus.FrameworkMonitored
                    )
                    {
                        DetectedDate = new DateTime(2026, 7, 25),
                        AffectedUsers = 5,
                        EventCount = 6,
                        TechnologiesInvolved = new List<string> { "MonoGame GameView", "PowerVR/Mali GPU", "Unisoc/MediaTek", ".NET LowLevelMonitor" },
                        Devices = new List<VitalDevice>
                        {
                            new VitalDevice("OPPO/Oplus MediaTek", "Oplus", "Android 13/14", null, "MediaTek + PowerVR"),
                            new VitalDevice("Infinix SMART", "Infinix", "11", 30, "MediaTek + PowerVR (libsrv_um.so)"),
                            new VitalDevice("realme C53", "realme", "15", 35, "Unisoc T612 + Mali-G57 (libGLES_mali.so)"),
                            new VitalDevice("Generic ARM32/ARM64", "PowerVR", "Android 10+", null, "PowerVR libsrv_um.so")
                        },
                        SignatureFrames = new List<string>
                        {
                            "SystemNative_LowLevelMonitor_TimedWait libSystem.Native.so",
                            "SystemNative_LowLevelMonitor_Signal_Release libSystem.Native.so",
                            "pthread_cond_timedwait / pthread_cond_signal"
                        },
                        RootCause = "MonoGameAndroidGameView calls WaitOne() / Monitor TimedWait on main thread during OnPause/OnResume waiting for game loop handshake. Game loop thread (.NET Long Running Task) stalls on mutex while budget GPU drivers (PowerVR libsrv_um.so / Mali libGLES_mali.so) handle surface sync/teardown.",
                        FixApproach = "Optimized OnPause main thread work (GameSettings.Load redundancy removed from 404ms to 2.2ms). Internal MonoGame handshake monitored.",
                        RelatedDoc = "MainThreadLock_ANR.md"
                    },

                    new VitalIssue(
                        "anr-input-lock-free-optimization",
                        "MessageQueue.nativePollOnce (Input dispatching timeout / Lock Contention)",
                        blocked.Id,
                        blocked.Name ?? "Blocked",
                        VitalType.ANR,
                        "Input dispatching timed out",
                        VitalSeverity.Critical,
                        VitalStatus.FixedAwaitingRelease
                    )
                    {
                        DetectedDate = new DateTime(2026, 8, 22),
                        ReportedVersion = "608190345",
                        AffectedUsers = 11,
                        EventCount = 12,
                        TechnologiesInvolved = new List<string> { "MonoGame Input", "Android UI Thread", "ConcurrentDictionary", "AdMob UMP" },
                        Devices = new List<VitalDevice>
                        {
                            new VitalDevice("All Android Devices", "Generic", "Android 10+", null, "UI Thread contention with MonoGame loop")
                        },
                        SignatureFrames = new List<string>
                        {
                            "android.os.MessageQueue.nativePollOnce",
                            "android.os.MessageQueue.next",
                            "Blocked.Android.Activity1.DispatchKeyEvent",
                            "Blocked.Shared.Managers.InputManager.AndroidKeysDown"
                        },
                        RootCause = "UI thread was frequently contending on C# 'lock (AndroidKeysDown)' with MonoGame update loop during key/touch inputs and lifecycle events. In addition, ConsentManager and MobileAds initialization in OnCreate delayed first frame rendering.",
                        FixApproach = "1. Converted InputManager.AndroidKeysDown to ConcurrentDictionary with lock-free atomic operations. 2. Made scroll delta and back-pressed tracking lock-free. 3. Offloaded OnPause audio suspend to Task.Run. 4. Postponed UMP Consent & MobileAds initialization past the first frame via PostDelayed(250ms).",
                        RelatedDoc = "play_vitals.json"
                    },

                    new VitalIssue(
                        "anr-egl-bufferqueue-lock-contention",
                        "[libIMGegl.so] IMGeglSwapBuffersWithDamageKHR (BLASTBufferQueue contention)",
                        blocked.Id,
                        blocked.Name ?? "Blocked",
                        VitalType.ANR,
                        "Graphic Buffer Lock Contention",
                        VitalSeverity.Low,
                        VitalStatus.FrameworkMonitored
                    )
                    {
                        DetectedDate = new DateTime(2026, 8, 22),
                        ReportedVersion = "608250351 (1.0.2026.08.25)",
                        AffectedUsers = 2,
                        EventCount = 2,
                        TechnologiesInvolved = new List<string> { "PowerVR GPU (libIMGegl.so)", "BufferQueue (libgui.so)", "MediaTek (libged.so)", "EGL" },
                        Devices = new List<VitalDevice>
                        {
                            new VitalDevice("Generic MediaTek + PowerVR", "PowerVR/MediaTek", "Android 13+", null, "PowerVR IMGegl + MediaTek GED")
                        },
                        SignatureFrames = new List<string>
                        {
                            "BLASTBufferQueue::onFrameAvailable libgui.so",
                            "Surface::queueBuffer / hook_queueBuffer libgui.so",
                            "IMGeglSwapBuffersWithDamageKHR libIMGegl.so",
                            "eglSwapBuffers libEGL.so",
                            "android::jni_eglSwapBuffers libandroid_runtime.so"
                        },
                        RootCause = "UNPROVEN. The production trace is in eglSwapBuffers -> IMGeglSwapBuffersWithDamageKHR -> libgui BLASTBufferQueue::onFrameAvailable. It demonstrates an Android graphics-buffer / IMG PowerVR driver stall, but does not prove whether the blocked frame was ordinary rendering or a surface transition.",
                        FixApproach = "Mitigation: Activity launch orientation changed from SensorLandscape to SensorPortrait on 2026-08-26, matching the product portrait default and removing the deterministic cold-start landscape-to-portrait SurfaceView/GraphicsDevice reset. Monitor the next production artifact; no managed code can directly repair a vendor EGL/BufferQueue deadlock.",
                        LessonsLearned = "A native graphics signature must not be labelled as an app rendering bug without timing evidence. Record the exact driver frames and remove reproducible surface transitions first.",
                        RelatedDoc = "play_vitals.json"
                    },

                    new VitalIssue(
                        "anr-powervr-gldrawelements-stall",
                        "[libGLESv2_powervr.so] glDrawElements (GPU Driver Execution Stall)",
                        blocked.Id,
                        blocked.Name ?? "Blocked",
                        VitalType.ANR,
                        "Input dispatching timed out / GPU Driver Stall",
                        VitalSeverity.Low,
                        VitalStatus.FrameworkMonitored
                    )
                    {
                        DetectedDate = new DateTime(2026, 8, 23),
                        ReportedVersion = "608220526",
                        AffectedUsers = 1,
                        EventCount = 1,
                        TechnologiesInvolved = new List<string> { "PowerVR GPU (libGLESv2_powervr.so)", "OpenGL ES", "MonoGame GraphicsDevice", "libsrv_um.so" },
                        Devices = new List<VitalDevice>
                        {
                            new VitalDevice("Lava Yuva Star", "Lava", "14", 34, "PowerVR GE8300 glDrawElements stall")
                        },
                        SignatureFrames = new List<string>
                        {
                            "glDrawElements /vendor/lib64/egl/libGLESv2_powervr.so",
                            "libGLESv2_powervr.so",
                            "libsrv_um.so (pthread_cond_timedwait)"
                        },
                        RootCause = "The main thread calls glDrawElements during rendering. On budget PowerVR GPUs (Lava Yuva Star), hardware command buffer execution / vertex buffer transfer stalled in the vendor driver (libsrv_um.so) for over 5 seconds, causing an input dispatch timeout.",
                        FixApproach = "Native GPU hardware/driver stall. SpriteBatch draw calls and texture batching are already optimized. Monitored for future platform updates.",
                        RelatedDoc = "play_vitals.json"
                    },

                    new VitalIssue(
                        "anr-quit-exit-abort",
                        "Environment.Exit(0) tearing down threads during exit",
                        blocked.Id,
                        blocked.Name ?? "Blocked",
                        VitalType.ANR,
                        "Input dispatching timed out / SIGABRT",
                        VitalSeverity.High,
                        VitalStatus.FixedAwaitingRelease
                    )
                    {
                        DetectedDate = new DateTime(2026, 8, 16),
                        ReportedVersion = "608150206",
                        AffectedUsers = 1,
                        EventCount = 1,
                        TechnologiesInvolved = new List<string> { "C# Environment.Exit", "OpenAL Native Thread", "Mono SGen" },
                        SignatureFrames = new List<string>
                        {
                            "mono.java.lang.RunnableImplementor.n_run",
                            "libc.so exit",
                            "std::__1::thread::~thread",
                            "abort"
                        },
                        RootCause = "Environment.Exit(0) called libc exit() while OpenAL alsoft-mixer, BlockedAudioWar and Mono SGen threads were active, triggering std::terminate() / abort(). Main thread blocked in wait4, triggering ANR and SIGABRT simultaneously.",
                        FixApproach = "IPlatformService.QuitGame() implemented. Desktop keeps Environment.Exit(0); Android overrides with MoveTaskToBack(true) to background gracefully without killing process destructors.",
                        LessonsLearned = "Never call Environment.Exit(0) or System.exit(0) in Android games with active native audio/rendering threads.",
                        RelatedDoc = "README.md"
                    },

                    new VitalIssue(
                        "anr-inflate-fast",
                        "[libz.so] inflate_fast - APK Asset Decompression on Main Thread",
                        blocked.Id,
                        blocked.Name ?? "Blocked",
                        VitalType.ANR,
                        "Heavy APK Decompression on Main Thread",
                        VitalSeverity.Medium,
                        VitalStatus.PartiallyMitigated
                    )
                    {
                        DetectedDate = new DateTime(2026, 8, 16),
                        ReportedVersion = "608150206",
                        AffectedUsers = 1,
                        EventCount = 1,
                        TechnologiesInvolved = new List<string> { "MonoGame Content.Load", "zlib/DEFLATE", "APK Assets" },
                        Devices = new List<VitalDevice>
                        {
                            new VitalDevice("Moto e13", "Motorola", "13", 33, "Unisoc T606 (2GB RAM)")
                        },
                        SignatureFrames = new List<string>
                        {
                            "inflate_fast /system/lib/libz.so",
                            "android::StreamingZipInflater::read",
                            "android.content.res.AssetManager$AssetInputStream.read",
                            "libmonosgen-2.0.so"
                        },
                        RootCause = "MonoGame Content.Load<Texture2D>() decompressing large XNB files out of APK via DEFLATE zlib on main thread on low-end CPUs.",
                        FixApproach = "1) Audio assets stored uncompressed (.m4a;.mp3;.ogg;.wav in AndroidStoreUncompressedFileExtensions). 2) Mobile texture set shrunk via Content.Mobile. 3) Ordered texture budget shrink list.",
                        LessonsLearned = "Store pre-compressed media (AAC/m4a) uncompressed in APK. Shrink high-resolution textures for mobile.",
                        RelatedDoc = "README.md"
                    },

                    new VitalIssue(
                        "crash-egl-not-initialized-hwui",
                        "[libc.so] abort - Failed to choose config, error = EGL_NOT_INITIALIZED (libhwui)",
                        blocked.Id,
                        blocked.Name ?? "Blocked",
                        VitalType.Crash,
                        "SIGABRT (Native Crash / __android_log_assert)",
                        VitalSeverity.Low,
                        VitalStatus.ClosedOsDriverBug
                    )
                    {
                        DetectedDate = new DateTime(2026, 8, 20),
                        ReportedVersion = "608190345 / 1.0.2026.08.19",
                        AffectedUsers = 1,
                        EventCount = 1,
                        TechnologiesInvolved = new List<string> { "Android HWUI", "EGL/OpenGL GPU Driver" },
                        Devices = new List<VitalDevice>
                        {
                            new VitalDevice("Vivo vivo Y85A", "Vivo", "8.1", 27, "Snapdragon 450 / Adreno 506")
                        },
                        SignatureFrames = new List<string>
                        {
                            "abort libc.so",
                            "__android_log_assert liblog.so",
                            "android::uirenderer::renderthread::EglManager::loadConfigs libhwui.so",
                            "android::uirenderer::renderthread::EglManager::initialize libhwui.so",
                            "android::uirenderer::renderthread::EglManager::createSurface libhwui.so"
                        },
                        RootCause = "AOSP / OEM GPU driver bug on Android 8.1 Oreo. During hardware UI initialization, libhwui failed to initialize EGL display, triggering fatal __android_log_assert in system RenderThread. Completely outside app/MonoGame code.",
                        FixApproach = "Closed as OS/driver level false positive. No app code changes needed or possible.",
                        LessonsLearned = "Crashes originating strictly in libhwui.so / EglManager::initialize on legacy Android 8.1 devices are OS/driver level failures.",
                        RelatedDoc = "play_vitals.json"
                    },

                    new VitalIssue(
                        "anr-nativepollonce-late-dump",
                        "Native method - android.os.MessageQueue.nativePollOnce (__epoll_pwait)",
                        blocked.Id,
                        blocked.Name ?? "Blocked",
                        VitalType.ANR,
                        "Input dispatching timed out / Late dump",
                        VitalSeverity.Low,
                        VitalStatus.ClosedNotActionable
                    )
                    {
                        DetectedDate = new DateTime(2026, 8, 20),
                        AffectedUsers = 1,
                        EventCount = 1,
                        TechnologiesInvolved = new List<string> { "Android MessageQueue", "OPlus/Oppo OS" },
                        Devices = new List<VitalDevice>
                        {
                            new VitalDevice("OPPO / OPlus Device", "OPPO", "13/14", 34, "OplusTheiaUIMonitor")
                        },
                        SignatureFrames = new List<string>
                        {
                            "__epoll_pwait libc.so",
                            "android::Looper::pollInner",
                            "android::android_os_MessageQueue_nativePollOnce",
                            "android.os.MessageQueue.next",
                            "android.app.ActivityThread.main"
                        },
                        RootCause = "Late Stack Dump / False Positive. Main thread was completely idle waiting for looper events when stack was taken. All engine and binder threads were normal and idle. Per Google ANR Guide Rule #0, nativePollOnce input dispatch clusters are non-actionable.",
                        FixApproach = "No code change required. Google ANR guidelines recommend ignoring nativePollOnce input dispatch clusters.",
                        LessonsLearned = "A main thread in nativePollOnce / __epoll_pwait indicates a late dump where app already recovered.",
                        RelatedDoc = "GOOGLE_ANR_GUIDE.md"
                    },

                    new VitalIssue(
                        "anr-art-gc-concurrent-copying",
                        "[libart.so] art::gc::collector::ConcurrentCopying::Copy (ART GC Heap Compacting Pause)",
                        blocked.Id,
                        blocked.Name ?? "Blocked",
                        VitalType.ANR,
                        "Input dispatching timed out / GC Mutator Stall",
                        VitalSeverity.Medium,
                        VitalStatus.FrameworkMonitored
                    )
                    {
                        DetectedDate = new DateTime(2026, 8, 20),
                        ReportedVersion = "608190345 / 1.0.2026.08.19",
                        AffectedUsers = 1,
                        EventCount = 1,
                        TechnologiesInvolved = new List<string> { "Android ART Runtime", "Concurrent Copying GC", "AdMob/WebView", "MonoGame" },
                        Devices = new List<VitalDevice>
                        {
                            new VitalDevice("Oppo CPH1909 (Oppo A5s)", "Oppo", "8.1", 27, "MediaTek Helio P35 (2GB RAM)")
                        },
                        SignatureFrames = new List<string>
                        {
                            "art::gc::collector::ConcurrentCopying::Copy libart.so",
                            "art::gc::collector::ConcurrentCopying::Copy libart.so"
                        },
                        RootCause = "ART GC Compacting Stall on Low-RAM Device. On Android 8.1 (Oppo A5s with 2GB RAM), the ART Concurrent Copying GC triggered a heavy heap compaction phase. When the main thread attempted object access/allocation, the read barrier or mutator pause stalled the main thread longer than 5 seconds due to high memory pressure from AdMob/Chromium WebView + MonoGame engine running simultaneously on a low-end MediaTek CPU.",
                        FixApproach = "Reducing overall texture/RAM footprint directly mitigates this: smaller textures = smaller Mono/Java heap = faster/fewer ART GC compaction cycles. Continue TEXTURE_BUDGET.md shrinking and mobile asset optimization.",
                        LessonsLearned = "On 2GB RAM Android 8.1 devices, concurrent ART GC compaction can stall UI thread if heap allocation pressure is high.",
                        RelatedDoc = "play_vitals.json"
                    }
                };
            }

            await context.SaveAsync();
        }
    }
}
