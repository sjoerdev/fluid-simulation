package fluids

import "core:math"
import "core:math/linalg"

Particle :: struct {
    position: [2]f32,
    velocity: [2]f32,
    force: [2]f32,
    density: f32,
    pressure: f32
}

// window variables
WIDTH :: 1280
HEIGHT :: 720
TITLE :: "fluid simulation"

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
spatialHashGrid: map[int][dynamic]Particle

main :: proc() {
    // todo
}

RenderParticles :: proc() {
    // todo
}

UpdateParticles :: proc() {
    BuildHashGrid()
    ComputeDensityPressure()
    ComputeForces()
    Integrate()
}

Integrate :: proc() {
    // todo
}

ComputeForces :: proc() {
    // todo
}

ComputeDensityPressure :: proc() {
    for &particle_a in particles {
        particle_a.density = 0
        cell := CellFromParticle(particle_a)
        neighborHashes := GetParticleNeighborHashes(cell)
        for neighborHash in neighborHashes {
            if neighborHash not_in spatialHashGrid do continue
            for particle_b in spatialHashGrid[neighborHash] {
                difference := particle_b.position - particle_a.position
                dotproduct := linalg.dot(difference, difference)
                diff := KERNEL_RADIUS_SQR - dotproduct
                pdiff := diff * diff * diff
                if dotproduct < KERNEL_RADIUS_SQR do particle_a.density += PARTICLE_MASS * POLY6 * pdiff
            }
        }
    }
}

BuildHashGrid :: proc() {
    clear(&spatialHashGrid)
    for particle in particles {
        hash := HashFromCell(CellFromParticle(particle))
        append(&spatialHashGrid[hash], particle)
    }
}

HashFromCell :: proc(cell: [2]int) -> int {
    PRIME1 := 73856093
    PRIME2 := 19349663
    return (cell.x * PRIME1) ~ (cell.y * PRIME2)
}

CellFromParticle :: proc(particle: Particle) -> [2]int {
    x := cast(int)(particle.position.x / CELL_SIZE)
    y := cast(int)(particle.position.y / CELL_SIZE)
    return {x, y}
}

GetParticleNeighborHashes :: proc(cell: [2]int) -> [dynamic]int {
    neighbourHashes: [dynamic]int
    for xo := -1; xo <= 1; xo += 1 {
        for yo := -1; yo <= 1; yo += 1 {
            offset: [2]int = {xo, yo}
            hash := HashFromCell(cell + offset)
            append(&neighbourHashes, hash)
        }
    }
    return neighbourHashes
}