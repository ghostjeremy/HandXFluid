using UnityEngine;
using static UnityEngine.Mathf;

public class GPUSort
{
    // Kernel indices for different compute shader stages
    const int sortKernel = 0;
    const int calculateOffsetsKernel = 1;
    // Reference to the compute shader for sorting
    readonly ComputeShader sortCompute;
    ComputeBuffer indexBuffer;
    // Constructor that loads the compute shader resource
    public GPUSort()
    {
        sortCompute = Resources.Load<ComputeShader>("BitonicMergeSort");
    }
    // Sets the buffers for the compute shader
    public void SetBuffers(ComputeBuffer indexBuffer, ComputeBuffer offsetBuffer)
    {
        this.indexBuffer = indexBuffer;

        sortCompute.SetBuffer(sortKernel, "Entries", indexBuffer);
        Utility.SetBuffer(sortCompute, offsetBuffer, "Offsets", calculateOffsetsKernel);
        Utility.SetBuffer(sortCompute, indexBuffer, "Entries", calculateOffsetsKernel);
    }

    // Sorts the data using the Bitonic merge sort algorithm
    public void Sort()
    {
        sortCompute.SetInt("numEntries", indexBuffer.count);

        int numStages = (int)Log(NextPowerOfTwo(indexBuffer.count), 2);

        for (int stageIndex = 0; stageIndex < numStages; stageIndex++)
        {
            for (int stepIndex = 0; stepIndex < stageIndex + 1; stepIndex++)
            {
                // Calculate parameters for the sorting step
                int groupWidth = 1 << (stageIndex - stepIndex);
                int groupHeight = 2 * groupWidth - 1;
                sortCompute.SetInt("groupWidth", groupWidth);
                sortCompute.SetInt("groupHeight", groupHeight);
                sortCompute.SetInt("stepIndex", stepIndex);
                // Run the sorting step on the GPU
                Utility.Dispatch(sortCompute, NextPowerOfTwo(indexBuffer.count) / 2);
            }
        }
    }

    // Sorts the data and calculates offsets
    public void SortAndCalculateOffsets()
    {
        Sort();

        Utility.Dispatch(sortCompute, indexBuffer.count, kernelIndex: calculateOffsetsKernel);
    }

}