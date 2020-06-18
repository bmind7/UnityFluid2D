# UnityFluid2D
 Various implementations of Jos Stam fluid sim paper, with performance comparison.

 ![Sim](demo.gif?raw=True)

## Original Stam implementation
Based on Jos Stam papaer "Real-Time Fluid Dynamics for Games"
https://pdfs.semanticscholar.org/847f/819a4ea14bd789aca8bc88e85e906cfc657c.pdf
This is close to original implementation, with only changes to boundaries calculations

## OOP version
Added support of colored dye. 
Suffers from weak performance due to many *Vector3.ctor()* calls during maths operations.

## OOP optimized
Has better performance than previous one, but still worse than Stam's original code

## DOTS version
Due to implementation specifics the visual result is slightly different from Original Stam. It happens because **deltaTime** is significantly lower. 

## DOTS + Burst
SOON

## Compute Shaders
SOON

*Note: Tested on IL2CPP standalone build*
Implementation | frame time (ms)
---------------|--------------
Original|23ms
OOP|44ms
OOP Optimized|39ms
DOTS|9ms
DOTS + Burst|-
Compute Shaders|-
