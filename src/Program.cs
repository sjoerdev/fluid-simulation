using System.Numerics;
using System.Drawing;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL.Legacy;
using Silk.NET.Windowing;

namespace Project;

public struct Particle
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

public static class Program
{
    public static GL gl;
    public static IWindow window;
    public static IInputContext input;

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
    static int MAX_PARTICLES = 2500;
    static int DAM_PARTICLES = 500;
    static int BLOCK_PARTICLES = 250;

    // projection
    static int WINDOW_WIDTH = 800;
    static int WINDOW_HEIGHT = 600;
    static float VIEW_WIDTH = 1.5f * 800.0f;
    static float VIEW_HEIGHT = 1.5f * 600.0f;

    static void Main()
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(1280, 720);
        options.Title = "Fluid Simulation";
        window = Window.Create(options);
        window.Load += WindowLoad;
        window.Render += WindowRender;
        window.Run();
        window.Dispose();
    }

    static void WindowLoad()
    {
        input = window.CreateInput();
        input.Keyboards[0].KeyDown += OnKeyDown;
        gl = GL.GetApi(window);
        gl.ClearColor(Color.White);
        gl.Enable(EnableCap.PointSmooth);

        // set onkeydown callback

        // spawn particles
    }

    static void WindowRender(double deltaTime)
    {
        // update
        // render
    }

    static void SpawnParticles()
    {
        for (float y = BOUNDARY_EPSILON; y < VIEW_HEIGHT - BOUNDARY_EPSILON * 2.0f; y += KERNEL_RADIUS)
        {
            for (float x = VIEW_WIDTH / 4; x <= VIEW_WIDTH / 2; x += KERNEL_RADIUS)
            {
                if (particles.Count() < DAM_PARTICLES) particles.Add(new Particle(x, y));
                else return;
            }
        }
    }

    static void ComputeDensityPressure()
    {
        for (int i = 0; i < particles.Count; i++)
        {
            var particle_a = particles[i];
            particle_a.density = 0.0f;
            for (int j = 0; j < particles.Count; j++)
            {
                var particle_b = particles[j];
                Vector2 rij = particle_b.position - particle_a.position;
                float r2 = Vector2.Dot(rij, rij);
                if (r2 < KERNEL_RADIUS_SQR) particle_a.density += PARTICLE_MASS * POLY6 * MathF.Pow(KERNEL_RADIUS_SQR - r2, 3.0f);
            }
            particle_a.pressure = GAS_CONSTANT * (particle_a.density - REST_DENSITY);
        }
    }

    static void ComputeForces()
    {
        for (int i = 0; i < particles.Count; i++)
        {
            var particle_a = particles[i];

            Vector2 pressure_force = new(0.0f, 0.0f);
            Vector2 viscosity_force = new(0.0f, 0.0f);
            for (int j = 0; j < particles.Count; j++)
            {
                var particle_b = particles[j];
                if (particle_a.Equals(particle_b)) continue;

                Vector2 difference = particle_b.position - particle_a.position;
                float distance = Vector2.Distance(particle_a.position, particle_b.position);
                if (distance < KERNEL_RADIUS)
                {
                    pressure_force += -Vector2.Normalize(difference) * PARTICLE_MASS * (particle_a.pressure + particle_b.pressure) / (2.0f * particle_b.density) * SPIKY_GRAD * MathF.Pow(KERNEL_RADIUS - distance, 3.0f);
                    viscosity_force += VISCOSITY * PARTICLE_MASS * (particle_b.velocity - particle_a.velocity) / particle_b.density * VISC_LAP * (KERNEL_RADIUS - distance);
                }
            }
            Vector2 gravity_force = new Vector2(0, GRAVITY) * PARTICLE_MASS / particle_a.density;
            particle_a.force = pressure_force + viscosity_force + gravity_force;
        }
    }

    static void Integrate()
    {
        for (int i = 0; i < particles.Count; i++)
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
            if (particle.position.X + BOUNDARY_EPSILON > VIEW_WIDTH)
            {
                particle.velocity.X *= BOUND_DAMPING;
                particle.position.X = VIEW_WIDTH - BOUNDARY_EPSILON;
            }
            if (particle.position.Y - BOUNDARY_EPSILON < 0.0f)
            {
                particle.velocity.Y *= BOUND_DAMPING;
                particle.position.Y = BOUNDARY_EPSILON;
            }
            if (particle.position.Y + BOUNDARY_EPSILON > VIEW_HEIGHT)
            {
                particle.velocity.Y *= BOUND_DAMPING;
                particle.position.Y = VIEW_HEIGHT - BOUNDARY_EPSILON;
            }
        }
    }

    static void Update()
    {
        ComputeDensityPressure();
        ComputeForces();
        Integrate();
    }

    static void Render()
    {
        gl.Clear(ClearBufferMask.ColorBufferBit);
        gl.LoadIdentity();
        gl.Ortho(0, VIEW_WIDTH, 0, VIEW_HEIGHT, 0, 1);
        gl.Color4(0.2f, 0.2f, 0.2f, 1);
        gl.Begin(GLEnum.Points);
        foreach (var particle in particles) gl.Vertex2(particle.position.X, particle.position.Y);
        gl.End();
        window.SwapBuffers();
    }

    static void OnKeyDown(IKeyboard keyboard, Key key, int idk)
    {
        if (key == Key.Space && particles.Count() < MAX_PARTICLES)
        {
            for (float y = VIEW_HEIGHT / 1.5f - VIEW_HEIGHT / 5.0f; y < VIEW_HEIGHT / 1.5f + VIEW_HEIGHT / 5.0f; y += KERNEL_RADIUS * 0.95f)
            {
                for (float x = VIEW_WIDTH / 2.0f - VIEW_HEIGHT / 5.0f; x <= VIEW_WIDTH / 2.0f + VIEW_HEIGHT / 5.0f; x += KERNEL_RADIUS * 0.95f)
                {
                    if (particles.Count() < MAX_PARTICLES)
                    {
                        particles.Add(new Particle(x, y));
                    }
                }
            }
        }
        else if (key == Key.R)
        {
            particles.Clear();
            SpawnParticles();
        }
    }
}