## Fluid Simulation
This is an (sph) fluid simulation.
- It heavily uses multithreading
- It uses spatial hasing for improved performance

## Showcase
https://github.com/user-attachments/assets/9e318653-5ec1-4dc7-9071-c8a26ff5f471

## Building:

Download .NET 9: https://dotnet.microsoft.com/en-us/download

Windows: ``dotnet publish -o ./build/windows --sc true -r win-x64 -c release``

Linux: ``dotnet publish -o ./build/linux --sc true -r linux-x64 -c release``
