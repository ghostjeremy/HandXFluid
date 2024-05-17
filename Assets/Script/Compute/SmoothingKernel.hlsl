static const float PI = 3.1415926535897931; // Definition of PI constant

// Poly6 smoothing kernel function
// dist: distance between particles
// h: smoothing radius
float Poly6SmoothingKernel(float dist, float h)
{
    // Check if distance is within the smoothing radius
    if (dist < h)
    {
        // Compute the scaling factor for the kernel
        float kernelScale = 315 / (64 * PI * pow(abs(h), 9));
        // Compute the value (h^2 - dist^2)
        float tempValue = h * h - dist * dist;
        // Return the kernel value (tempValue^3 * kernelScale)
        return tempValue * tempValue * tempValue * kernelScale;
    }
    return 0; // Return 0 if distance is outside the radius
}

// Spiky smoothing kernel function
// dist: distance between particles
// h: smoothing radius
float SpikySmoothingKernel(float dist, float h)
{
    // Check if distance is within or equal to the smoothing radius
    if (dist <= h)
    {
        // Compute the scaling factor for the kernel
        float kernelScale = 45 / (pow(h, 6) * PI);
        // Compute the value (h - dist)
        float tempValue = h - dist;
        // Return the kernel value (-(tempValue^2) * kernelScale)
        return -tempValue * tempValue * kernelScale;
    }
    return 0; // Return 0 if distance is outside the radius
}

// Viscosity smoothing kernel function
// dist: distance between particles
// h: smoothing radius
float ViscositySmoothingKernel(float dist, float h)
{
    // Check if distance is within or equal to the smoothing radius
    if (dist <= h)
    {
        // Compute the scaling factor for the kernel
        float kernelScale = 45 / (2 * pow(h, 6) * PI);
        // Compute the value (h - dist)
        float tempValue = h - dist;
        // Return the kernel value (-(tempValue) * kernelScale)
        return -tempValue * kernelScale;
    }
    return 0; // Return 0 if distance is outside the radius
}

