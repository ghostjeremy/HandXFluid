using UnityEngine;
using Unity.Mathematics;
using System;

public class FluidSimulator : MonoBehaviour
{

    // Event triggered when a simulation step is completed
    public event System.Action SimulationStepCompleted;

    [Header("Particles Settings")]
    public int countX = 40; // Number of particles in X direction
    public int countY = 40; // Number of particles in Y direction
    public int countZ = 30; // Number of particles in Z direction

    public float3 initialVelocity; // Initial velocity of particles
    public int SumNumberParticles; // Total number of particles

    [Header("SPH Settings")]
    public ComputeShader fluidModel; // Compute shader for fluid simulation

    public int iterationsPerFrame; // Number of iterations per frame
    [Range(-10, 0)] public float gravity = -2; // Gravity value
    [Range(0, 1)] public float collisionDamping = 0.8f; // Collision damping factor
    public float smoothingRadius = 0.01f; // Smoothing radius for SPH
    public float targetDensity = 500; // Target density of the fluid
    public float pressureMultiplier = 0.2f; // Multiplier for pressure calculation
    public float viscosityStrength = 0.8f; // Strength of viscosity


    [Header("Hand Setting")]
    public GameObject handObject; // Game object representing the hand
    public float handStrength = 50; // Strength of hand interaction
    public Transform[] collisionSpheres; // Array of collision spheres

    public ComputeBuffer positionBuffer { get; private set; } // Buffer for particle positions
    public ComputeBuffer velocityBuffer { get; private set; } // Buffer for particle velocities
    public ComputeBuffer densityBuffer { get; private set; } // Buffer for particle densities
    public ComputeBuffer predictedPositionsBuffer; // Buffer for predicted particle positions

    ComputeBuffer spatialIndices; // Buffer for spatial indices
    ComputeBuffer spatialOffsets; // Buffer for spatial offsets


    // Kernel indices for different compute shader stages
    const int externalForcesKernel = 0;
    const int spatialHashKernel = 1;
    const int densityKernel = 2;
    const int pressureKernel = 3;
    const int viscosityKernel = 4;
    const int updatePositionsKernel = 5;

    GPUSort gpuSort; // GPU sorting utility
    ParticlesData particlesData; // Data structure for particles

    Material mat; // Material for rendering
    ComputeBuffer argsBuffer; // Buffer for drawing arguments
    Bounds bounds; // Bounds for rendering



    [Header("Render Settings")]
    public Shader shader; // Shader for rendering
    public float scale = 4; // Scale for rendering
    public Mesh mesh; // Mesh for rendering particles
    public Color col; // Color for rendering particles
    public Gradient colourMap; // Gradient color map
    public int gradientResolution =64; // Resolution for gradient texture
    public float velocityDisplayMax =1; // Maximum velocity for display
    Texture2D gradientTexture; // Texture for gradient
    bool needsUpdate; // Flag indicating if rendering needs update





    void Start()
    {
        handObject = GameObject.Find("[BuildingBlock] Hand Interactions");
        FindSphereColliders(handObject.transform); 


        // Set fixed time step for simulation
        float deltaTime = 1 / 60f;
        Time.fixedDeltaTime = deltaTime;


        particlesData = this.GetParticlesData();

        int numParticles = particlesData.postions.Length;


        // Initialize compute buffers
       positionBuffer = Utility.StructuredBuffer<float3>(numParticles);
       predictedPositionsBuffer = Utility.StructuredBuffer<float3>(numParticles);
       velocityBuffer = Utility.StructuredBuffer<float3>(numParticles);
       densityBuffer = Utility.StructuredBuffer<float>(numParticles);
       spatialIndices = Utility.StructuredBuffer<uint3>(numParticles);
       spatialOffsets = Utility.StructuredBuffer<uint>(numParticles);


        InitialParticleData(particlesData);


        // Initialize compute shader buffers
        Utility.SetBuffer(fluidModel, positionBuffer, "Positions", externalForcesKernel, updatePositionsKernel);
        Utility.SetBuffer(fluidModel, predictedPositionsBuffer, "PredictedPositions", externalForcesKernel, spatialHashKernel, densityKernel, pressureKernel, viscosityKernel, updatePositionsKernel);
        Utility.SetBuffer(fluidModel, spatialIndices, "SpatialIndices", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        Utility.SetBuffer(fluidModel, spatialOffsets, "SpatialOffsets", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        Utility.SetBuffer(fluidModel, densityBuffer, "Densities", densityKernel, pressureKernel, viscosityKernel);
        Utility.SetBuffer(fluidModel, velocityBuffer, "Velocities", externalForcesKernel, pressureKernel, viscosityKernel, updatePositionsKernel);

        fluidModel.SetInt("numParticles", positionBuffer.count);

        gpuSort = new();
        gpuSort.SetBuffers(spatialIndices, spatialOffsets);


        InitialRender(); 


    }

    // Update is called once per frame
    void FixedUpdate()
    {

        RunSimulationFrame(Time.fixedDeltaTime);

    }


    // LateUpdate for render
    void LateUpdate()
    {


            UpdateRender();
            Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, bounds, argsBuffer);

       
    }

    // Generates particle data (positions and velocities)
    public ParticlesData GetParticlesData()
    {
        int numParticles = countX * countY * countZ;

        Vector3 gridSize = new Vector3(countX * smoothingRadius/2, countY * smoothingRadius/2, countZ * smoothingRadius/2);

        float stepX = gridSize.x / (countX - 1);
        float stepY = gridSize.y / (countY - 1);
        float stepZ = gridSize.z / (countZ - 1);


        float3[] postions = new float3[numParticles];
        float3[] velocities = new float3[numParticles];

        int i = 0;


        Vector3 center = transform.position;

        for (int x = 0; x < countX; x++)
        {
            for (int y = 0; y < countY; y++)
            {
                for (int z = 0; z < countZ; z++)
                {

                    float offsetX = x * stepX - (gridSize.x / 2);
                    float offsetY = y * stepY - (gridSize.y / 2);
                    float offsetZ = z * stepZ - (gridSize.z / 2);
                    float3 position = center + new Vector3(offsetX, offsetY, offsetZ);

                    postions[i] = position;
                    velocities[i] = initialVelocity;
                    i++;
                }
            }
        }

        return new ParticlesData() { postions = postions, velocities = velocities };
    }

    // Finds all sphere colliders in the hierarchy of a parent transform
    void FindSphereColliders(Transform parent)
    {
        foreach (Transform child in parent)
        {
            
            if (child.name.Contains("SphereCol"))
            {
                Debug.Log("Found Sphere Collider: " + child.name);

                
                AddToCollisionSpheres(child);
            }

            
            FindSphereColliders(child);
        }
    }

    // Adds a sphere collider to the collision spheres array
    void AddToCollisionSpheres(Transform sphere)
    {
       
        if (collisionSpheres == null)
        {
            collisionSpheres = new Transform[1];
            collisionSpheres[0] = sphere;
        }
        else
        {
       
            Transform[] tempArray = new Transform[collisionSpheres.Length + 1];
            collisionSpheres.CopyTo(tempArray, 0);
            tempArray[collisionSpheres.Length] = sphere;
            collisionSpheres = tempArray;
        }
    }

    // Runs the simulation for a single frame
    void RunSimulationFrame(float frameTime)
    {

            float timeStep = frameTime / iterationsPerFrame;

            UpdateSettings(timeStep);

            for (int i = 0; i < iterationsPerFrame; i++)
            {
            Utility.Dispatch(fluidModel, positionBuffer.count, kernelIndex: externalForcesKernel);
            Utility.Dispatch(fluidModel, positionBuffer.count, kernelIndex: spatialHashKernel);
            gpuSort.SortAndCalculateOffsets();
            Utility.Dispatch(fluidModel, positionBuffer.count, kernelIndex: densityKernel);
            Utility.Dispatch(fluidModel, positionBuffer.count, kernelIndex: pressureKernel);
            Utility.Dispatch(fluidModel, positionBuffer.count, kernelIndex: viscosityKernel);
            Utility.Dispatch(fluidModel, positionBuffer.count, kernelIndex: updatePositionsKernel);
            SimulationStepCompleted?.Invoke();
            }

    }

    // Updates settings for the simulation
    void UpdateSettings(float deltaTime)
    {

        UpdateSphere();

        fluidModel.SetFloat("deltaTime", deltaTime);
        fluidModel.SetFloat("gravity", gravity);
        fluidModel.SetFloat("collisionDamping", collisionDamping);
        fluidModel.SetFloat("smoothingRadius", smoothingRadius);
        fluidModel.SetFloat("targetDensity", targetDensity);
        fluidModel.SetFloat("pressureMultiplier", pressureMultiplier);
        fluidModel.SetFloat("viscosityStrength", viscosityStrength);
        fluidModel.SetFloat("handStrength", handStrength);
        fluidModel.SetMatrix("localToWorld", transform.localToWorldMatrix);
        fluidModel.SetMatrix("worldToLocal", transform.worldToLocalMatrix);
    }


    // Updates sphere collider's positions and radius
    void UpdateSphere() 
    {

        int numSpheres = collisionSpheres.Length;

        Vector4[] spherePositions = new Vector4[numSpheres];
        Vector4[] sphereRadius = new Vector4[numSpheres];

        for (int i = 0; i < numSpheres; i++)
        {
            spherePositions[i] = collisionSpheres[i].position; 
            sphereRadius[i] = Vector4.zero;
            sphereRadius[i].x = collisionSpheres[i].localScale.x / 2;
        }


        fluidModel.SetVectorArray("spherePosBuffer", spherePositions);
        fluidModel.SetVectorArray("sphereRaBuffer", sphereRadius);

    }

    // Initializes particle data in the buffers
    void InitialParticleData(ParticlesData particlesData)
    {
        float3[] allPoints = new float3[particlesData.postions.Length];

        System.Array.Copy(particlesData.postions, allPoints, particlesData.postions.Length);

        positionBuffer.SetData(allPoints);
        predictedPositionsBuffer.SetData(allPoints);
        velocityBuffer.SetData(particlesData.velocities);
    }

    // Initializes rendering settings
    public void InitialRender()
    {
        mat = new Material(shader);
        mat.SetBuffer("Positions", positionBuffer);
        mat.SetBuffer("Velocities", velocityBuffer);
      
        argsBuffer = Utility.CreateArgsBuffer(mesh, positionBuffer.count);
        bounds = new Bounds(Vector3.zero, Vector3.one * 10000);
    }

    // Updates rendering settings
    void UpdateRender()
    {
        if (needsUpdate)
        {
            needsUpdate = false;
            TextureFromGradient(ref gradientTexture, gradientResolution, colourMap);
            mat.SetTexture("ColourMap", gradientTexture);
        }
        mat.SetFloat("scale", scale);
        mat.SetColor("colour", col);
        mat.SetFloat("velocityMax", velocityDisplayMax);

        Vector3 s = transform.localScale;
        transform.localScale = Vector3.one;
        var localToWorld = transform.localToWorldMatrix;
        transform.localScale = s;

        mat.SetMatrix("localToWorld", localToWorld);
    }

    // Generates a texture from a gradient
    public static void TextureFromGradient(ref Texture2D texture, int width, Gradient gradient, FilterMode filterMode = FilterMode.Bilinear)
    {
        if (texture == null)
        {
            texture = new Texture2D(width, 1);
        }
        else if (texture.width != width)
        {
            texture.Reinitialize(width, 1);
        }
        if (gradient == null)
        {
            gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.black, 0), new GradientColorKey(Color.black, 1) },
                new GradientAlphaKey[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) }
            );
        }
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = filterMode;

        Color[] cols = new Color[width];
        for (int i = 0; i < cols.Length; i++)
        {
            float t = i / (cols.Length - 1f);
            cols[i] = gradient.Evaluate(t);
        }
        texture.SetPixels(cols);
        texture.Apply();
    }

    // Data structure for particles
    public struct ParticlesData
    {
        public float3[] postions;
        public float3[] velocities;
    }


    private void OnValidate()
    {
        needsUpdate = true;
        // Validates and updates the number of particles
        SumNumberParticles = countX * countY * countZ;
    }

    // Releases all compute buffers on destruction
    void OnDestroy()
    {
        Utility.Release(positionBuffer, predictedPositionsBuffer, velocityBuffer, densityBuffer, spatialIndices, spatialOffsets, argsBuffer);
    }

    // Draws gizmos in the editor for visualization
    void OnDrawGizmos()
    {
        // Draw Bounds
        var m = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0, 1, 0, 0.5f);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = m;

        Vector3 center = transform.position;
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawWireCube(center, new Vector3(countX * smoothingRadius/2, countY * smoothingRadius/2, countZ * smoothingRadius/2));


    }


}
