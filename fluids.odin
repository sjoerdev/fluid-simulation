package fluids

import "core:fmt"
import "core:math"
import "core:os"
import "vendor:glfw"
import gl "vendor:OpenGL"

WIDTH :: 1280
HEIGHT :: 720
TITLE :: "fluid simulation"

GL_MAJOR_VERSION :: 3
GL_MINOR_VERSION :: 3

Particle :: struct {
    position: [2]f32,
    velocity: [2]f32,
    force: [2]f32,
    density: f32,
    pressure: f32
}

// ---------------------------------
// opengl buffers
vao: uint
position_vbo: uint
pressure_vbo: uint
shader: u32
projection: matrix[4, 4]f32

// solver parameters
GRAVITY: f32 = -9.8
REST_DENSITY: f32 = 300
GAS_CONSTANT: f32 = 1800
KERNEL_RADIUS: f32 = 16
KERNEL_RADIUS_SQR: f32 = KERNEL_RADIUS * KERNEL_RADIUS
PARTICLE_MASS: f32 = 2.5
VISCOSITY: f32 = 200
INTIGRATION_TIMESTEP: f32 = 0.0007

// smoothing kernels and gradients
POLY6: f32 = 4.0 / (math.PI * math.pow(KERNEL_RADIUS, 8.0))
PIKY_GRAD: f32 = -10.0 / (math.PI * math.pow(KERNEL_RADIUS, 5.0))
VISC_LAP: f32 = 40.0 / (math.PI * math.pow(KERNEL_RADIUS, 5.0))

// simulation boundary
BOUNDARY_EPSILON: f32 = KERNEL_RADIUS
BOUND_DAMPING: f32 = -0.5

// particles
particles: [dynamic]Particle
MAX_PARTICLES: int = 16000

// projection
POINT_SIZE: int = cast(int)(KERNEL_RADIUS / 2)
WINDOW_WIDTH: int = 1920
WINDOW_HEIGHT: int = 1080

// spatial hash grid
CELL_SIZE: f32 = KERNEL_RADIUS
spatialHashGrid: map[int][dynamic]Particle // Dictionary<int, List<Particle>>

// ---------------------------------

// ---------------------------------

use_shader :: proc(shader_handle: u32)
shader_set_bool :: proc(shader_handle: u32, name: string, value: bool)
shader_set_float :: proc(shader_handle: u32, name: string, value: f32)
shader_set_vec3 :: proc(shader_handle: u32, name: string, value: [3]f32)
shader_set_vec4 :: proc(shader_handle: u32, name: string, value: [4]f32)
shader_set_mat4 :: proc(shader_handle: u32, name: string, value: matrix[4, 4]f32)
shader_set_texture :: proc(shader_handle: u32, name: string, texture: uint, unit: int)

shader_compile_program :: proc(vertPath: string, fragPath: string) -> u32
{
    vertCode, _ := os.read_entire_file(vertPath, context.allocator)
    fragCode, _ := os.read_entire_file(fragPath, context.allocator)

    vertex := shader_compile(gl.GL_Enum.VERTEX_SHADER, cast(string)vertCode)
    fragment := shader_compile(gl.GL_Enum.FRAGMENT_SHADER, cast(string)fragCode)
    
    program := gl.CreateProgram()
    gl.AttachShader(program, vertex)
    gl.AttachShader(program, fragment)
    gl.LinkProgram(program)

    gl.DeleteShader(vertex)
    gl.DeleteShader(fragment)

    return program
}

shader_compile :: proc(shader_type: gl.GL_Enum, source: string) -> u32 {
    shader := gl.CreateShader(cast(u32)gl.GL_Enum.SHADER_TYPE)
    source_ptr := cast(cstring)raw_data(source)
    source_length := cast(i32)len(source)
    gl.ShaderSource(shader, 1, &source_ptr, &source_length)
    gl.CompileShader(shader)
    return shader
}

// ---------------------------------

main :: proc() {

    if !bool(glfw.Init()) {
        return
    }

    window_handle := glfw.CreateWindow(WIDTH, HEIGHT, TITLE, nil, nil)

    defer glfw.Terminate()
    defer glfw.DestroyWindow(window_handle)

    if window_handle == nil {
        return
    }

    glfw.MakeContextCurrent(window_handle)
    gl.load_up_to(GL_MAJOR_VERSION, GL_MINOR_VERSION, glfw.gl_set_proc_address)

    for !glfw.WindowShouldClose(window_handle) {
        glfw.PollEvents()

        gl.ClearColor(0.5, 0.0, 1.0, 1.0)
        gl.Clear(gl.COLOR_BUFFER_BIT)

        glfw.SwapBuffers(window_handle)
    }
}