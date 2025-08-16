using System.Numerics;
using System.Drawing;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Hexa.NET.ImGui;

namespace Project;

public class Particle
{
    public Vector2 position;
    public Vector2 velocity;
    public Vector2 force;
    public float density;
    public float pressure;

    public Particle(float x, float y)
    {
        position = new Vector2(x, y);
        velocity = new Vector2(0.0f, 0.0f);
        force = new Vector2(0.0f, 0.0f);
        density = 0;
        pressure = 0.0f;
    }
};

public static unsafe class Program
{
    public static GL gl;
    public static IWindow window;
    public static IInputContext input;
    public static ImGuiController igcontroller;

    // framerate
    static List<float> fps_history = [];
    static float fps_show_rate = 0.1f;
    static float fps_average;
    static float fps_timer;
    static int fps_max = 300;

    // opengl buffers
    static uint vao;
    static uint position_vbo;
    static uint pressure_vbo;
    static Shader shader;
    static Matrix4x4 projection;

    // solver parameters
    static float GRAVITY = -10;
    static float REST_DENSITY = 300;
    static float GAS_CONSTANT = 2000;
    static float KERNEL_RADIUS = 16;
    static float KERNEL_RADIUS_SQR = KERNEL_RADIUS * KERNEL_RADIUS;
    static float PARTICLE_MASS = 2.5f;
    static float VISCOSITY = 200;
    static float INTIGRATION_TIMESTEP = 0.0007f;

    // smoothing kernels and gradients
    static float POLY6 = 4.0f / (MathF.PI * MathF.Pow(KERNEL_RADIUS, 8.0f));
    static float SPIKY_GRAD = -10.0f / (MathF.PI * MathF.Pow(KERNEL_RADIUS, 5.0f));
    static float VISC_LAP = 40.0f / (MathF.PI * MathF.Pow(KERNEL_RADIUS, 5.0f));

    // simulation boundary
    static float BOUNDARY_EPSILON = KERNEL_RADIUS;
    static float BOUND_DAMPING = -0.5f;

    // particles
    static List<Particle> particles = [];
    static int MAX_PARTICLES = 16000;

    // projection
    static int POINT_SIZE = (int)(KERNEL_RADIUS / 2f);
    static int WINDOW_WIDTH = 1920;
    static int WINDOW_HEIGHT = 1080;

    // spatial hash grid
    static float CELL_SIZE = KERNEL_RADIUS;
    static Dictionary<int, List<Particle>> spatialHashGrid = [];

    static void Main()
    {
        var options = WindowOptions.Default;
        options.API = new(ContextAPI.OpenGL, new APIVersion(3, 3));
        options.Size = new Vector2D<int>(WINDOW_WIDTH, WINDOW_HEIGHT);
        options.Title = "Fluid Simulation";
        options.VSync = false;
        options.WindowBorder = WindowBorder.Fixed;
        options.FramesPerSecond = fps_max;
        window = Window.Create(options);
        window.Load += Load;
        window.Render += Render;
        window.Run();
        window.Dispose();
    }

    static void Load()
    {
        input = window.CreateInput();
        input.Keyboards[0].KeyDown += OnKeyDown;

        gl = GL.GetApi(window);
        gl.PointSize(POINT_SIZE);

        shader = new Shader("res/particle.vert", "res/particle.frag");
        projection = Matrix4x4.CreateOrthographicOffCenter(0, WINDOW_WIDTH, 0, WINDOW_HEIGHT, -1f, 1f);

        SetupBuffers();
        SpawnParticles();

        igcontroller = new ImGuiController(gl, window, input);
    }

    static void Render(double deltaTime)
    {
        igcontroller.Update((float)deltaTime);

        TrackFramesPerSecond((float)deltaTime);
        UpdateParticles();
        RenderParticles();

        ImGui.SetNextWindowSize(new Vector2(280, 0), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(16, 16), ImGuiCond.FirstUseEver);
        ImGui.Begin("settings");

        ImGui.Text("fps: " + fps_average.ToString("0"));
        ImGui.Text($"particles: {particles.Count} / {MAX_PARTICLES}");

        ImGui.SliderFloat("gravity", ref GRAVITY, 0, -40);
        ImGui.SliderFloat("viscosity", ref VISCOSITY, 200, 800);

        fps_max = (int)window.FramesPerSecond;
        ImGui.SliderInt("max fps", ref fps_max, 30, 800);
        window.FramesPerSecond = fps_max;

        if (ImGui.Button("spawn particles")) SpawnParticles();
        if (ImGui.Button("clear particles")) particles.Clear();

        ImGui.End();
        
        igcontroller.Render();
    }

    static void UpdateParticles()
    {
        BuildHashGrid();
        ComputeDensityPressure();
        ComputeForces();
        Integrate();
    }

    static void RenderParticles()
    {
        // clear
        gl.ClearColor(Color.White);
        gl.Clear(ClearBufferMask.ColorBufferBit);

        if (particles.Count <= 0) return;

        // shader
        shader.Use();
        shader.SetMatrix4("projection", projection);

        // calculate min and max pressure
        float minPressure = particles.Min(p => p.pressure);
        float maxPressure = particles.Max(p => p.pressure);
        shader.SetFloat("minPressure", minPressure);
        shader.SetFloat("maxPressure", maxPressure);

        // particles
        gl.BindVertexArray(vao);
        
        // position buffer
        float[] position_buffer = PositionFloatArray();
        fixed (void* ptr = &position_buffer[0])
        {
            gl.BindBuffer(GLEnum.ArrayBuffer, position_vbo);
            gl.BufferSubData(GLEnum.ArrayBuffer, 0, (nuint)(position_buffer.Length * sizeof(float)), ptr);
        }

        // pressure buffer
        float[] pressure_buffer = PressureFloatArray();
        fixed (void* ptr = &pressure_buffer[0])
        {
            gl.BindBuffer(GLEnum.ArrayBuffer, pressure_vbo);
            gl.BufferSubData(GLEnum.ArrayBuffer, 0, (nuint)(pressure_buffer.Length * sizeof(float)), ptr);
        }

        gl.DrawArrays(GLEnum.Points, 0, (uint)particles.Count);

        gl.BindVertexArray(0);
    }

    static void TrackFramesPerSecond(float delta)
    {
        if (fps_timer < fps_show_rate)
        {
            fps_history.Add(1f / delta);
            fps_timer += delta;
        }
        else
        {
            fps_average = fps_history.Average();
            fps_history.Clear();
            fps_timer = 0f;
        }
    }

    static Vector2 CellFromParticle(Particle particle)
    {
        int x = (int)(particle.position.X / CELL_SIZE);
        int y = (int)(particle.position.Y / CELL_SIZE);
        return new Vector2(x, y);
    }

    static int HashFromCell(Vector2 cell)
    {
        const int PRIME1 = 73856093;
        const int PRIME2 = 19349663;
        return ((int)cell.X * PRIME1) ^ ((int)cell.Y * PRIME2);
    }

    static void BuildHashGrid()
    {
        spatialHashGrid.Clear();
        foreach (var particle in particles)
        {
            var cell = CellFromParticle(particle);
            int hash = HashFromCell(cell);
            if (!spatialHashGrid.ContainsKey(hash)) spatialHashGrid[hash] = [];
            spatialHashGrid[hash].Add(particle);
        }
    }

    static List<int> GetParticleNeighborHashes(Vector2 cell)
    {
        List<int> neighborHashes = [];
        for (int xo = -1; xo <= 1; xo++)
        {
            for (int yo = -1; yo <= 1; yo++)
            {
                var offset = new Vector2(xo, yo);
                var hash = HashFromCell(cell + offset);
                neighborHashes.Add(hash);
            }
        }
        return neighborHashes;
    }

    static void SetupBuffers()
    {
        vao = gl.GenVertexArray();
        gl.BindVertexArray(vao);

        // position buffer
        position_vbo = gl.GenBuffer();
        gl.BindBuffer(GLEnum.ArrayBuffer, position_vbo);
        gl.BufferData(GLEnum.ArrayBuffer, (nuint)(MAX_PARTICLES * 2 * sizeof(float)), (void*)0, GLEnum.DynamicDraw);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, GLEnum.Float, false, 2 * sizeof(float), 0);

        // pressure buffer
        pressure_vbo = gl.GenBuffer();
        gl.BindBuffer(GLEnum.ArrayBuffer, pressure_vbo);
        gl.BufferData(GLEnum.ArrayBuffer, (nuint)(MAX_PARTICLES * sizeof(float)), (void*)0, GLEnum.DynamicDraw);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 1, GLEnum.Float, false, sizeof(float), 0);

        gl.BindVertexArray(0);
    }

    static void SpawnParticles()
    {
        var radius = 200;
        var center = new Vector2(WINDOW_WIDTH, WINDOW_HEIGHT) / 2f;
        var random = new Random();
        var spacing = KERNEL_RADIUS;

        for (float y = center.Y - radius; y <= center.Y + radius; y += spacing)
        {
            for (float x = center.X - radius; x <= center.X + radius; x += spacing)
            {
                var ox = (random.NextSingle() - 0.5f) * spacing;
                var oy = (random.NextSingle() - 0.5f) * spacing;
                var position = new Vector2(x + ox, y + oy);

                var inside = Vector2.Distance(center, position) <= radius;
                var notmax = particles.Count < MAX_PARTICLES;

                if (inside && notmax) particles.Add(new Particle(position.X, position.Y));
            }
        }
    }

    static void ComputeDensityPressure()
    {
        Parallel.For(0, particles.Count, (i) =>
        {
            var particle_a = particles[i];
            particle_a.density = 0.0f;

            var cell = CellFromParticle(particle_a);
            List<int> neighborHashes = GetParticleNeighborHashes(cell);
            foreach (int neighborHash in neighborHashes)
            {
                if (!spatialHashGrid.ContainsKey(neighborHash)) continue;
                foreach (var particle_b in spatialHashGrid[neighborHash])
                {
                    Vector2 difference = particle_b.position - particle_a.position;
                    float dotproduct = Vector2.Dot(difference, difference);
                    float diff = KERNEL_RADIUS_SQR - dotproduct;
                    float pdiff = diff * diff * diff;
                    if (dotproduct < KERNEL_RADIUS_SQR) particle_a.density += PARTICLE_MASS * POLY6 * pdiff;
                }
            }

            particle_a.pressure = GAS_CONSTANT * (particle_a.density - REST_DENSITY);
        });
    }

    static void ComputeForces()
    {
        Parallel.For(0, particles.Count, (i) =>
        {
            var particle_a = particles[i];
            Vector2 pressure_force = new(0.0f, 0.0f);
            Vector2 viscosity_force = new(0.0f, 0.0f);

            var cell = CellFromParticle(particle_a);
            List<int> neighborHashes = GetParticleNeighborHashes(cell);
            foreach (int neighborHash in neighborHashes)
            {
                if (!spatialHashGrid.ContainsKey(neighborHash)) continue;
                foreach (var particle_b in spatialHashGrid[neighborHash])
                {
                    if (particle_a == particle_b) continue;
                    Vector2 difference = particle_b.position - particle_a.position;
                    float distance = Vector2.Distance(particle_a.position, particle_b.position);
                    if (distance < KERNEL_RADIUS)
                    {
                        float diff = KERNEL_RADIUS - distance;
                        float pdiff = diff * diff * diff;
                        pressure_force += -Vector2.Normalize(difference) * PARTICLE_MASS * (particle_a.pressure + particle_b.pressure) / (2.0f * particle_b.density) * SPIKY_GRAD * pdiff;
                        viscosity_force += VISCOSITY * PARTICLE_MASS * (particle_b.velocity - particle_a.velocity) / particle_b.density * VISC_LAP * (KERNEL_RADIUS - distance);
                    }
                }
            }

            Vector2 gravity_force = new Vector2(0, GRAVITY) * PARTICLE_MASS / particle_a.density;
            particle_a.force = pressure_force + viscosity_force + gravity_force;
        });
    }

    static void Integrate()
    {
        Parallel.For(0, particles.Count, (i) =>
        {
            var particle = particles[i];

            // forward Euler integration
            particle.velocity += INTIGRATION_TIMESTEP * particle.force / particle.density;
            particle.position += INTIGRATION_TIMESTEP * particle.velocity;

            // enforce boundary conditions
            if (particle.position.X - BOUNDARY_EPSILON < 0.0f)
            {
                particle.velocity.X *= BOUND_DAMPING;
                particle.position.X = BOUNDARY_EPSILON;
            }
            if (particle.position.X + BOUNDARY_EPSILON > WINDOW_WIDTH)
            {
                particle.velocity.X *= BOUND_DAMPING;
                particle.position.X = WINDOW_WIDTH - BOUNDARY_EPSILON;
            }
            if (particle.position.Y - BOUNDARY_EPSILON < 0.0f)
            {
                particle.velocity.Y *= BOUND_DAMPING;
                particle.position.Y = BOUNDARY_EPSILON;
            }
            if (particle.position.Y + BOUNDARY_EPSILON > WINDOW_HEIGHT)
            {
                particle.velocity.Y *= BOUND_DAMPING;
                particle.position.Y = WINDOW_HEIGHT - BOUNDARY_EPSILON;
            }
        });
    }

    static float[] PositionFloatArray()
    {
        int count = particles.Count;
        float[] result = new float[count * 2];
        for (int i = 0; i < count; i++)
        {
            result[i * 2] = particles[i].position.X;
            result[i * 2 + 1] = particles[i].position.Y;
        }
        return result;
    }

    static float[] PressureFloatArray()
    {
        float[] result = new float[particles.Count];
        for (int i = 0; i < particles.Count; i++) result[i] = particles[i].pressure;
        return result;
    }

    static void OnKeyDown(IKeyboard keyboard, Key key, int idk)
    {
        if (key == Key.Space) SpawnParticles();
        if (key == Key.R) particles.Clear();
    }
}