﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using UAlbion.Core.Events;
using UAlbion.Core.Textures;
using ImGuiNET;
using UAlbion.Api;
using UAlbion.Core.Visual;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using Veldrid;

namespace UAlbion.Core
{
    public class Engine : Component, IDisposable
    {
        const int DefaultWidth = 720;
        const int DefaultHeight = 480;
        static readonly HandlerSet Handlers = new HandlerSet
        (
            H<Engine, LoadRenderDocEvent>((x, _) =>
            {
                if (_renderDoc == null && RenderDoc.Load(out _renderDoc))
                {
                    _renderDoc.OverlayEnabled = false;
                    x.ChangeBackend(x.GraphicsDevice.BackendType, true);
                }
            }),
            H<Engine, GarbageCollectionEvent>((x,_) => GC.Collect()),
            H<Engine, QuitEvent>((x, e) => x._done = true),
            H<Engine, RunRenderDocEvent>((x,_) => _renderDoc?.LaunchReplayUI()),
            H<Engine, SetCursorPositionEvent>((x, e) => x._pendingCursorUpdate = new Vector2(e.X, e.Y)),
            H<Engine, ToggleFullscreenEvent>((x, _) => x.ToggleFullscreenState()),
            H<Engine, ToggleHardwareCursorEvent>((x,_) => x.Window.CursorVisible = !x.Window.CursorVisible),
            H<Engine, ToggleResizableEvent>((x, _) => x.Window.Resizable = !x.Window.Resizable),
            H<Engine, ToggleVisibleBorderEvent>((x, _) => x.Window.BorderVisible = !x.Window.BorderVisible),
            H<Engine, SetMsaaLevelEvent>((x,e) => x._newSampleCount = e.SampleCount),
            H<Engine, SetVSyncEvent>((x, e) =>
            {
                if (x._vsync == e.Value) return;
                x._vsync = e.Value;
                x.ChangeBackend(x._backend);
            }),
            H<Engine, SetBackendEvent>((x, e) =>
            {
                if (x._backend == e.Value) return;
                x.ChangeBackend(e.Value);
            }));

        static RenderDoc _renderDoc;
        // public EventExchange GlobalExchange { get; }
        Sdl2Window Window => _windowManager.Window;

        readonly IDictionary<Type, IRenderer> _renderers = new Dictionary<Type, IRenderer>();
        readonly FrameTimeAverager _frameTimeAverager = new FrameTimeAverager(0.5);
        readonly SceneContext _sceneContext = new SceneContext();
        readonly WindowManager _windowManager = new WindowManager();

        CommandList _frameCommands;
        TextureSampleCount? _newSampleCount;
        bool _windowResized;
        bool _done;
        bool _recreateWindow = true;
        bool _vsync = true;
        Vector2? _pendingCursorUpdate;
        GraphicsBackend _backend;

        internal GraphicsDevice GraphicsDevice { get; private set; }
        internal RenderDoc RenderDoc => _renderDoc;

        internal string FrameTimeText => _frameTimeAverager.CurrentAverageFramesPerSecond.ToString("000.0 fps / ") +
                                         _frameTimeAverager.CurrentAverageFrameTimeMilliseconds.ToString("#00.00 ms");

        public Engine(GraphicsBackend backend, bool useRenderDoc) : base(Handlers)
        {
            _backend = backend;
            // GlobalExchange = new EventExchange("Global", logger);

            if (useRenderDoc)
                using(PerfTracker.InfrequentEvent("Loading renderdoc"))
                    RenderDoc.Load(out _renderDoc);
        }

        protected override void Subscribed()
        {
            var shaderCache = Resolve<IShaderCache>();
            if(shaderCache == null)
                throw new InvalidOperationException("An instance of IShaderCache must be registered.");
            shaderCache.ShadersUpdated += (sender, args) => ChangeBackend(_backend);

            Exchange
                .Register<IWindowManager>(_windowManager)
                //.Attach(new DebugMenus(this))
                ;
        }

        public Engine AddRenderer(IRenderer renderer)
        {
            _renderers.Add(renderer.GetType(), renderer);
            if (renderer is IComponent component)
            {
                Exchange?.Attach(component);
                Children.Add(component);
            }

            return this;
        }

        public void Run()
        {
            ChangeBackend(_backend);
            PerfTracker.StartupEvent("Set up backend");
            Sdl2Native.SDL_Init(SDLInitFlags.GameController);

            CreateAllObjects();
            PerfTracker.StartupEvent("Created objects");
            ImGui.StyleColorsClassic();
            Raise(new WindowResizedEvent(Window.Width, Window.Height));
            Raise(new BeginFrameEvent());

            var frameCounter = new FrameCounter();
            PerfTracker.StartupEvent("Startup done, rendering first frame");
            while (!_done)
            {
                PerfTracker.BeginFrame();
                double deltaSeconds = frameCounter.StartFrame();
                using(PerfTracker.FrameEvent("Raising begin frame"))
                    Raise(new BeginFrameEvent());

                InputSnapshot snapshot;
                using (PerfTracker.FrameEvent("Processing SDL events"))
                {
                    Sdl2Events.ProcessEvents();
                    snapshot = Window.PumpEvents();
                }

                if (!Window.Exists)
                    break;

                if (_pendingCursorUpdate.HasValue)
                {
                    using (PerfTracker.FrameEvent("Warping mouse"))
                    {
                        Sdl2Native.SDL_WarpMouseInWindow(
                            Window.SdlWindowHandle,
                            (int) _pendingCursorUpdate.Value.X,
                            (int) _pendingCursorUpdate.Value.Y);

                        _pendingCursorUpdate = null;
                    }
                }

                using (PerfTracker.FrameEvent("Raising input event"))
                    Raise(new InputEvent(deltaSeconds, snapshot, Window.MouseDelta));

                using (PerfTracker.FrameEvent("Performing update"))
                    Update((float)deltaSeconds);

                if (!Window.Exists)
                    break;

                using (PerfTracker.FrameEvent("Drawing"))
                    Draw();
            }

            DestroyAllObjects();
            GraphicsDevice.Dispose();
            Window.Close();
        }

        void Update(float deltaSeconds)
        {
            _frameTimeAverager.AddTime(deltaSeconds);
            Raise(new EngineUpdateEvent(deltaSeconds));
        }

        internal void RefreshDeviceObjects(int numTimes)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < numTimes; i++)
            {
                DestroyAllObjects();
                CreateAllObjects();
            }

            sw.Stop();
            Console.WriteLine($"Refreshing resources {numTimes} times took {sw.Elapsed.TotalSeconds} seconds.");
        }

        void ToggleFullscreenState()
        {
            bool isFullscreen = Window.WindowState == WindowState.BorderlessFullScreen;
            Window.WindowState = isFullscreen ? WindowState.Normal : WindowState.BorderlessFullScreen;
        }

        void Draw()
        {
            Debug.Assert(Window.Exists);
            int width = Window.Width;
            int height = Window.Height;

            CoreTrace.Log.Info("Engine", "Start draw");
            if (_windowResized)
            {
                _windowResized = false;

                GraphicsDevice.ResizeMainWindow((uint)width, (uint)height);
                Raise(new WindowResizedEvent(width, height));
                CommandList cl = GraphicsDevice.ResourceFactory.CreateCommandList();
                cl.Begin();
                _sceneContext.RecreateWindowSizedResources(GraphicsDevice, cl);
                cl.End();
                GraphicsDevice.SubmitCommands(cl);
                cl.Dispose();
                CoreTrace.Log.Info("Engine", "Resize finished");
            }

            if (_newSampleCount != null)
            {
                _sceneContext.MainSceneSampleCount = _newSampleCount.Value;
                _newSampleCount = null;
                DestroyAllObjects();
                CreateAllObjects();
            }

            using (PerfTracker.FrameEvent("Render scenes"))
            {
                _frameCommands.Begin();

                var scenes = new List<Scene>();
                Raise(new CollectScenesEvent(scenes.Add));

                foreach (var scene in scenes)
                    scene.RenderAllStages(GraphicsDevice, _frameCommands, _sceneContext, _renderers);

                _frameCommands.End();
            }

            using (PerfTracker.FrameEvent("Submit commandlist"))
            {
                CoreTrace.Log.Info("Scene", "Submitting Commands");
                GraphicsDevice.SubmitCommands(_frameCommands);
                CoreTrace.Log.Info("Scene", "Submitted commands");
            }

            using (PerfTracker.FrameEvent("Swap buffers"))
            {
                CoreTrace.Log.Info("Engine", "Swapping buffers...");
                GraphicsDevice.SwapBuffers();
                CoreTrace.Log.Info("Engine", "Draw complete");
            }
        }

        void ChangeBackend(GraphicsBackend backend, bool forceRecreateWindow = false)
        {
            using (PerfTracker.InfrequentEvent($"change backend to {backend}"))
            {
                _backend = backend;
                if (GraphicsDevice != null)
                {
                    DestroyAllObjects();
                    GraphicsDevice.Dispose();
                }

                if (Window == null || _recreateWindow || forceRecreateWindow)
                {
                    Window?.Close();

                    WindowCreateInfo windowInfo = new WindowCreateInfo
                    {
                        X = Window?.X ?? 648,
                        Y = Window?.Y ?? 431,
                        WindowWidth = Window?.Width ?? DefaultWidth,
                        WindowHeight = Window?.Height ?? DefaultHeight,
                        WindowInitialState = Window?.WindowState ?? WindowState.Normal,
                        WindowTitle = "UAlbion"
                    };

                    _windowManager.Window = VeldridStartup.CreateWindow(ref windowInfo);
                    //Window.BorderVisible = false;
                    Window.CursorVisible = false;
                    Window.Resized += () => _windowResized = true;
                }

                GraphicsDeviceOptions gdOptions = new GraphicsDeviceOptions(
                    false, null, false,
                    ResourceBindingModel.Improved, true,
                    true, false)
                {
                    Debug = (_renderDoc != null),
                    SyncToVerticalBlank = _vsync,
                    // NoThreading = true
                };

                GraphicsDevice = VeldridStartup.CreateGraphicsDevice(Window, gdOptions, backend);
                GraphicsDevice.WaitForIdle();
                Window.Title = GraphicsDevice.BackendType.ToString();

                Raise(new BackendChangedEvent(GraphicsDevice));
                CreateAllObjects();
            }
        }

        void CreateAllObjects()
        {
            using(PerfTracker.InfrequentEvent("Create objects"))
            {
                _frameCommands = GraphicsDevice.ResourceFactory.CreateCommandList();
                _frameCommands.Name = "Frame Commands List";

                CommandList initCL = GraphicsDevice.ResourceFactory.CreateCommandList();
                initCL.Name = "Recreation Initialization Command List";
                initCL.Begin();
                _sceneContext.CreateDeviceObjects(GraphicsDevice, initCL, _sceneContext);

                foreach (var r in _renderers.Values)
                    r.CreateDeviceObjects(GraphicsDevice, initCL, _sceneContext);

                initCL.End();
                GraphicsDevice.SubmitCommands(initCL);
                GraphicsDevice.WaitForIdle();
                initCL.Dispose();
                GraphicsDevice.WaitForIdle();
            }
        }

        void DestroyAllObjects()
        {
            using (PerfTracker.InfrequentEvent("Destroying objects"))
            {
                GraphicsDevice.WaitForIdle();
                _frameCommands.Dispose();
                _sceneContext.DestroyDeviceObjects();
                foreach (var r in _renderers.Values)
                    r.DestroyDeviceObjects();

                Resolve<IShaderCache>().DestroyAllDeviceObjects();
                Resolve<ITextureManager>()?.DestroyDeviceObjects();
                GraphicsDevice.WaitForIdle();
            }
        }

        public void Dispose()
        {
            _frameCommands?.Dispose();
            foreach(var renderer in _renderers.Values)
                if (renderer is IDisposable disposable)
                    disposable.Dispose();

            //_graphicsDevice?.Dispose();
        }

        [Event("e:set_vsync", "Enables or disables VSync")]
        public class SetVSyncEvent : EngineEvent
        {
            public SetVSyncEvent(bool value) { Value = value; }
            [EventPart("value")] public bool Value { get; } 
        }

        [Event("e:set_backend", "Sets the current graphics backend to use")]
        public class SetBackendEvent : EngineEvent
        {
            public SetBackendEvent(GraphicsBackend value) { Value = value; }
            [EventPart("value", "Valid values: OpenGL, OpenGLES, Vulkan, Metal or Direct3D11")] public GraphicsBackend Value { get; } 
        }

        [Event("e:set_msaa", "Sets the multisample anti-asliasing level")]
        public class SetMsaaLevelEvent : IEvent
        {
            [EventPart("sample_count")]
            public TextureSampleCount SampleCount { get; }

            public SetMsaaLevelEvent(TextureSampleCount msaaOption)
            {
                SampleCount = msaaOption;
            }
        }

        [Event("e:toggle_fullscreen")] public class ToggleFullscreenEvent : EngineEvent { }
        [Event("e:toggle_hw_cursor", "Toggles displaying the default windows cursor")] public class ToggleHardwareCursorEvent : EngineEvent { }
        [Event("e:toggle_resizable")] public class ToggleResizableEvent : EngineEvent { }
        [Event("e:run_renderdoc")] public class RunRenderDocEvent : EngineEvent { }
        [Event("e:load_renderdoc")] public class LoadRenderDocEvent : EngineEvent { }
        [Event("e:toggle_visible_border")] public class ToggleVisibleBorderEvent : EngineEvent { }
    }
}

