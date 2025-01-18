using System.Numerics;
using System.Drawing;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL.Legacy;
using Silk.NET.Windowing;

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
    static int MAX_PARTICLES = 16000;
    static int DAM_PARTICLES = 500;

    // projection
    static int POINT_SIZE = (int)(KERNEL_RADIUS / 4f);
    static int WINDOW_WIDTH = 1280;
    static int WINDOW_HEIGHT = 720;
    static float VIEWPORT_WIDTH = 2 * WINDOW_WIDTH;
    static float VIEWPORT_HEIGHT = 2 * WINDOW_HEIGHT;

    // spatial hash grid
    static float CELL_SIZE = KERNEL_RADIUS;
    static Dictionary<int, List<Particle>> spatialHashGrid = [];

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

    static void Main()
    {
        var options = WindowOptions.Default;
        options.API = new(ContextAPI.OpenGL, new APIVersion(2, 1));
        options.Size = new Vector2D<int>(WINDOW_WIDTH, WINDOW_HEIGHT);
        options.Title = "Fluid Simulation";
        options.VSync = true;
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
        gl.Enable(GLEnum.PointSmooth);
        gl.PointSize(POINT_SIZE);
        SpawnParticles();
    }

    static void Render(double deltaTime)
    {
        UpdateSimulation();
        RenderSimulation();
        Console.WriteLine(particles.Count + " - " + (1f / (float)deltaTime));
    }

    static void SpawnParticles()
    {
        for (float y = BOUNDARY_EPSILON; y < VIEWPORT_HEIGHT - BOUNDARY_EPSILON * 2.0f; y += KERNEL_RADIUS)
        {
            for (float x = VIEWPORT_WIDTH / 4; x <= VIEWPORT_WIDTH / 2; x += KERNEL_RADIUS)
            {
                if (particles.Count() < DAM_PARTICLES) particles.Add(new Particle(x, y));
                else return;
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
                    if (dotproduct < KERNEL_RADIUS_SQR) particle_a.density += PARTICLE_MASS * POLY6 * MathF.Pow(KERNEL_RADIUS_SQR - dotproduct, 3.0f);
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
                    if (particle_a.Equals(particle_b)) continue;
                    Vector2 difference = particle_b.position - particle_a.position;
                    float distance = Vector2.Distance(particle_a.position, particle_b.position);
                    if (distance < KERNEL_RADIUS)
                    {
                        pressure_force += -Vector2.Normalize(difference) * PARTICLE_MASS * (particle_a.pressure + particle_b.pressure) / (2.0f * particle_b.density) * SPIKY_GRAD * MathF.Pow(KERNEL_RADIUS - distance, 3.0f);
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
            if (particle.position.X + BOUNDARY_EPSILON > VIEWPORT_WIDTH)
            {
                particle.velocity.X *= BOUND_DAMPING;
                particle.position.X = VIEWPORT_WIDTH - BOUNDARY_EPSILON;
            }
            if (particle.position.Y - BOUNDARY_EPSILON < 0.0f)
            {
                particle.velocity.Y *= BOUND_DAMPING;
                particle.position.Y = BOUNDARY_EPSILON;
            }
            if (particle.position.Y + BOUNDARY_EPSILON > VIEWPORT_HEIGHT)
            {
                particle.velocity.Y *= BOUND_DAMPING;
                particle.position.Y = VIEWPORT_HEIGHT - BOUNDARY_EPSILON;
            }
        });
    }

    static void UpdateSimulation()
    {
        BuildHashGrid();
        ComputeDensityPressure();
        ComputeForces();
        Integrate();
    }

    static void RenderSimulation()
    {
        // clear screen
        gl.ClearColor(Color.CornflowerBlue);
        gl.Clear(ClearBufferMask.ColorBufferBit);
        
        // render particles as points
        gl.LoadIdentity();
        gl.Ortho(0, VIEWPORT_WIDTH, 0, VIEWPORT_HEIGHT, 0, 1);
        gl.Color4(1, 0, 0, 1);
        gl.Begin(GLEnum.Points);
        foreach (var particle in particles) gl.Vertex2(particle.position.X, particle.position.Y);
        gl.End();
    }

    static void OnKeyDown(IKeyboard keyboard, Key key, int idk)
    {
        if (key == Key.Space && particles.Count() < MAX_PARTICLES)
        {
            for (float y = VIEWPORT_HEIGHT / 1.5f - VIEWPORT_HEIGHT / 5.0f; y < VIEWPORT_HEIGHT / 1.5f + VIEWPORT_HEIGHT / 5.0f; y += KERNEL_RADIUS * 0.95f)
            {
                for (float x = VIEWPORT_WIDTH / 2.0f - VIEWPORT_HEIGHT / 5.0f; x <= VIEWPORT_WIDTH / 2.0f + VIEWPORT_HEIGHT / 5.0f; x += KERNEL_RADIUS * 0.95f)
                {
                    if (particles.Count() < MAX_PARTICLES)
                    {
                        particles.Add(new Particle(x, y));
                    }
                }
            }
        }
        
        if (key == Key.R)
        {
            particles.Clear();
            SpawnParticles();
        }
    }
}