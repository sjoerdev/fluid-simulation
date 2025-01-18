using System.Numerics;
using System.Drawing;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL.Legacy;
using Silk.NET.Windowing;

namespace Project;

public static class Program
{
    public static GL opengl;
    public static IWindow window;
    public static IInputContext input;

    static void Main()
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(1280, 720);
        options.Title = "Fluid Simulation";
        window = Window.Create(options);
        window.Load += Load;
        window.Render += Render;
        window.Run();
        window.Dispose();
    }

    static void Load()
    {
        opengl = GL.GetApi(window);
        input = window.CreateInput();
    }

    static void Render(double deltaTime)
    {

    }
}