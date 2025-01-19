## Fluid Simulation
This is an (sph) fluid simulation.
- It heavily uses multithreading
- It uses spatial hasing for improved performance

## Building:

Download .NET 9: https://dotnet.microsoft.com/en-us/download

Windows: ``dotnet publish -o ./build/windows --sc true -r win-x64 -c release``

Linux: ``dotnet publish -o ./build/linux --sc true -r linux-x64 -c release``
