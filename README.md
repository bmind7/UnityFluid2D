# UnityFluid2D
 Various implementations of Jos Stam fluid sim paper, with performance comparison.

 ![Sim](demo.gif?raw=True)

## Original Stam implementation
Based on Jos Stam papaer "Real-Time Fluid Dynamics for Games"
https://pdfs.semanticscholar.org/847f/819a4ea14bd789aca8bc88e85e906cfc657c.pdf
This is close to original implementation, with only changes to boundaries calculations

## Struct version
Use Unity *Vector3* struct.
Added support of colored dye. 
Suffers from weak performance due to many *Vector3.ctor()* calls during maths operations.

## Struct optimized
Has better performance than previous one, but still worse than Stam's original code

## DOTS version
Due to implementation specifics the visual result is slightly different from Original Stam. It happens because **deltaTime** is significantly lower. 

## DOTS + Burst
There is a chance of even more increase of performance by using *Unity.Mathematics* float3 struct (could be tested down the line)

## Compute Shaders
There is noticable artifacts when **deltaTime** gets lower 10ms. Probably can be solved by algorithms parameters tweaking, but for the purpose of this comparison it's not important.

*Note: Tested on IL2CPP standalone build, x86_64, i7-8750H, GTX1050 MaxQ*
Implementation | frame time (ms)
---------------|--------------
Original|15ms
Struct|34ms
Struct Optimized|26ms
DOTS|7.2ms
DOTS + Burst|5.8ms
Compute Shaders|~3ms

