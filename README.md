## Fluid Simulation
This is a smoothed particle hydrodynamics simulation
- It heavily uses multithreading
- It uses spatial hasing for fast performance
- The C# code is optimized and readable
- It can smoothly handle up to 20k particles

## Showcase
https://github.com/user-attachments/assets/9e318653-5ec1-4dc7-9071-c8a26ff5f471

## Building:

Download .NET 9: https://dotnet.microsoft.com/en-us/download

Windows: ``dotnet publish -o ./build/windows --sc true -r win-x64 -c release``

Linux: ``dotnet publish -o ./build/linux --sc true -r linux-x64 -c release``

## Resources
- https://www.cs.cornell.edu/~bindel/class/cs5220-f11/code/sph.pdf
- https://cg.informatik.uni-freiburg.de/publications/2014_EG_SPH_STAR.pdf
- https://sph-tutorial.physics-simulation.org/pdf/SPH_Tutorial.pdf
- https://lucasschuermann.com/writing/particle-based-fluid-simulation
- https://lucasschuermann.com/writing/implementing-sph-in-2d
