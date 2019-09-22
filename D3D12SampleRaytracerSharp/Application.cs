// Copyright (c) Amer Koleci and contributors.
// Distributed under the MIT license. See the LICENSE file in the project root for more information.

using D3D12SampleRaytracerSharp.Interop;
using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace D3D12SampleRaytracerSharp
{
    public abstract partial class Application : IDisposable
    {
        private bool _paused;
        private bool _exitRequested;

        private D3D12GraphicsDevice _graphicsDevice;
        public Window MainWindow { get; private set; }

        protected Application()
        {
            PlatformConstruct();
        }

        public void Dispose()
        {
            _graphicsDevice.Dispose();
        }

        public void Tick()
        {
            _graphicsDevice.DrawFrame(OnDraw);
        }

        public void Run()
        {
            PlatformRun();
        }

        protected virtual void OnActivated()
        {
        }

        protected virtual void OnDeactivated()
        {
        }

        protected virtual void OnDraw(int width, int height)
        {

        }

        private void InitializeBeforeRun()
        {
            _graphicsDevice = new D3D12GraphicsDevice(MainWindow);
        }

        public static readonly string WndClassName = "D3D12SampleRaytracerSharpWindow";
        public readonly IntPtr HInstance = Kernel32.GetModuleHandle(null);
        private WNDPROC _wndProc;

        private void PlatformConstruct()
        {
            _wndProc = ProcessWindowMessage;
            var wndClassEx = new WNDCLASSEX
            {
                Size = Unsafe.SizeOf<WNDCLASSEX>(),
                Styles = WindowClassStyles.CS_HREDRAW | WindowClassStyles.CS_VREDRAW | WindowClassStyles.CS_OWNDC,
                WindowProc = _wndProc,
                InstanceHandle = HInstance,
                CursorHandle = User32.LoadCursor(IntPtr.Zero, SystemCursor.IDC_ARROW),
                BackgroundBrushHandle = IntPtr.Zero,
                IconHandle = IntPtr.Zero,
                ClassName = WndClassName,
            };

            var atom = User32.RegisterClassEx(ref wndClassEx);

            if (atom == 0)
            {
                throw new InvalidOperationException(
                    $"Failed to register window class. Error: {Marshal.GetLastWin32Error()}"
                    );
            }

            // Create main window.
            MainWindow = new Window("D3D12SampleRaytracerSharp", 1280, 720);
        }

        private void PlatformRun()
        {
            InitializeBeforeRun();

            while (!_exitRequested)
            {
                if (!_paused)
                {
                    const uint PM_REMOVE = 1;
                    if (User32.PeekMessage(out var msg, IntPtr.Zero, 0, 0, PM_REMOVE))
                    {
                        User32.TranslateMessage(ref msg);
                        User32.DispatchMessage(ref msg);

                        if (msg.Value == (uint)WindowMessage.Quit)
                        {
                            _exitRequested = true;
                            break;
                        }
                    }

                    Tick();
                }
                else
                {
                    var ret = User32.GetMessage(out var msg, IntPtr.Zero, 0, 0);
                    if (ret == 0)
                    {
                        _exitRequested = true;
                        break;
                    }
                    else if (ret == -1)
                    {
                        //Log.Error("[Win32] - Failed to get message");
                        _exitRequested = true;
                        break;
                    }
                    else
                    {
                        User32.TranslateMessage(ref msg);
                        User32.DispatchMessage(ref msg);
                    }
                }
            }
        }

        private IntPtr ProcessWindowMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == (uint)WindowMessage.ActivateApp)
            {
                _paused = IntPtrToInt32(wParam) == 0;
                if (IntPtrToInt32(wParam) != 0)
                {
                    OnActivated();
                }
                else
                {
                    OnDeactivated();
                }

                return User32.DefWindowProc(hWnd, msg, wParam, lParam);
            }

            switch ((WindowMessage)msg)
            {
                case WindowMessage.Destroy:
                    User32.PostQuitMessage(0);
                    break;
            }

            return User32.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private static int SignedLOWORD(int n)
        {
            return (short)(n & 0xFFFF);
        }

        private static int SignedHIWORD(int n)
        {
            return (short)(n >> 16 & 0xFFFF);
        }

        private static int SignedLOWORD(IntPtr intPtr)
        {
            return SignedLOWORD(IntPtrToInt32(intPtr));
        }

        private static int SignedHIWORD(IntPtr intPtr)
        {
            return SignedHIWORD(IntPtrToInt32(intPtr));
        }

        private static int IntPtrToInt32(IntPtr intPtr)
        {
            return (int)intPtr.ToInt64();
        }

        private static Point MakePoint(IntPtr lparam)
        {
            var lp = lparam.ToInt32();
            var x = lp & 0xff;
            var y = (lp >> 16) & 0xff;
            return new Point(x, y);
        }
    }
}
