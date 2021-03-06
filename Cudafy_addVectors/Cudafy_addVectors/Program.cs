﻿/* 
 * This software is based upon the book CUDA By Example by Sanders and Kandrot
 * and source code provided by NVIDIA Corporation.
 * It is a good idea to read the book while studying the examples!
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Cudafy;
using Cudafy.Host;
using Cudafy.Translator;

namespace CudafyByExample
{
    class Program
    {

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                GPGPU gpu = InitializeGPU();
                gpu.FreeAll();

                int N = (int)Math.Pow(2, 25);

                // declare some arrays and allocate corresponding memory on GPU
                int[] a = new int[N];
                int[] b = new int[N];
                int[] c = new int[N];

                // fill the arrays 'a' and 'b' on the CPU and copy them to the device
                for (int j = 0; j < N; j++)
                {
                    a[j] = -j;
                    b[j] = 2 * j;
                }

                // repeat all 10 times (to measure runtime)
                for (int t = 0; t < 10; t++)
                {
                    gpu.StartTimer();
                    int[] dev_a = gpu.Allocate<int>(a);
                    int[] dev_b = gpu.Allocate<int>(b);
                    int[] dev_c = gpu.Allocate<int>(c);

                    gpu.CopyToDevice(a, dev_a);
                    gpu.CopyToDevice(b, dev_b);
                    elapsedTime = gpu.StopTimer();
                    Console.WriteLine("Allocation and copy to the GPU took {0} ms", elapsedTime);

                    // launch the kernel on the GPU!
                    gpu.StartTimer();
                    gpu.Launch(128, 512).addVectors(dev_a, dev_b, dev_c);
                    elapsedTime = gpu.StopTimer();
                    Console.WriteLine("Summing vectors took {0} ms.", elapsedTime);
                    
                    // copy the array 'c' back from the GPU to the CPU
                    gpu.StartTimer();
                    gpu.CopyFromDevice(dev_c, c);
                    elapsedTime = gpu.StopTimer();
                    Console.WriteLine("Copying data back took {0} ms.", elapsedTime);

                    // Verify that the GPU did the work we requested
                    bool success = true;
                    for (int i = 0; i < N; i++)
                    {
                        if ((a[i] + b[i]) != c[i])
                        {
                            Console.WriteLine("{0} + {1} != {2}", a[i], b[i], c[i]);
                            success = false;
                            break;
                        }
                    }
                    if (success)
                        Console.WriteLine("Check passed.\n");
                    gpu.FreeAll();
                }
            }
            catch (CudafyLanguageException cle)
            {
                HandleException(cle);
            }
            catch (CudafyCompileException cce)
            {
                HandleException(cce);
            }
            catch (CudafyHostException che)
            {
                HandleException(che);
            }

            Exit();
        }

        public static GPGPU InitializeGPU()
        {
            // Specify CUDAfy target and language
            CudafyModes.Target = eGPUType.OpenCL;
            CudafyTranslator.Language = eLanguage.OpenCL;

            // Look for suitable devices
            int deviceCount = CudafyHost.GetDeviceCount(CudafyModes.Target);
            if (deviceCount == 0)
            {
                Console.WriteLine("No suitable {0} devices found!", CudafyModes.Target);
                Exit();
            }

            // List devices and allow user to choose device to use
            Console.WriteLine("Listing OpenCL capable devices found:\n");
            int i = 0;
            foreach (GPGPUProperties prop in CudafyHost.GetDeviceProperties(eGPUType.OpenCL, false))
            {
                Console.WriteLine("   --- General Information for device {0} ---", i);
                Console.WriteLine("Device name:  {0}", prop.Name);
                Console.WriteLine("Platform Name:  {0}\n", prop.PlatformName);
                i++;
            }
            Console.WriteLine("Enter ID of device to use: ");
            int input = Int32.Parse(Console.ReadLine());
            if (input > i)
            {
                Console.WriteLine("Input {0} is invalid. Program will close.", input);
                Exit();
            }
            CudafyModes.DeviceId = input;
            GPGPU gpu = CudafyHost.GetDevice(CudafyModes.Target, CudafyModes.DeviceId);
            Console.WriteLine("\nYou chose to use device {0}: {1}", CudafyModes.DeviceId, gpu.GetDeviceProperties(false).Name);
            Console.WriteLine("Retreiving device properties...\n");
            System.Threading.Thread.Sleep(1000);

            GPGPUProperties gpuProperties = gpu.GetDeviceProperties(false);
            Console.WriteLine("   --- General Information for device {0} ---", CudafyModes.DeviceId);
            Console.WriteLine("Name:  {0}", gpuProperties.Name);
            Console.WriteLine("Platform Name:  {0}", gpuProperties.PlatformName);
            Console.WriteLine("Architecture:  {0}", gpu.GetArchitecture());
            //Console.WriteLine("Compute capability:  {0}.{1}", gpuProperties.Capability.Major, gpuProperties.Capability.Minor); // for CUDA
            Console.WriteLine("Clock rate: {0}", gpuProperties.ClockRate);
            Console.WriteLine("Simulated: {0}", gpuProperties.IsSimulated);
            Console.WriteLine();

            Console.WriteLine("   --- Memory Information for device {0} ---", CudafyModes.DeviceId);
            Console.WriteLine("Total global mem:  {0}", gpuProperties.TotalMemory);
            Console.WriteLine("Total constant Mem:  {0}", gpuProperties.TotalConstantMemory);
            Console.WriteLine("Max mem pitch:  {0}", gpuProperties.MemoryPitch);
            Console.WriteLine("Texture Alignment:  {0}", gpuProperties.TextureAlignment);
            Console.WriteLine();

            Console.WriteLine("   --- MP Information for device {0} ---", CudafyModes.DeviceId);
            Console.WriteLine("Shared mem per mp: {0}", gpuProperties.SharedMemoryPerBlock);
            Console.WriteLine("Registers per mp:  {0}", gpuProperties.RegistersPerBlock);
            Console.WriteLine("Threads in warp:  {0}", gpuProperties.WarpSize);
            Console.WriteLine("Max threads per block:  {0}", gpuProperties.MaxThreadsPerBlock);
            Console.WriteLine("Max thread dimensions:  ({0}, {1}, {2})", gpuProperties.MaxThreadsSize.x, gpuProperties.MaxThreadsSize.y, gpuProperties.MaxThreadsSize.z);
            Console.WriteLine("Max grid dimensions:  ({0}, {1}, {2})", gpuProperties.MaxGridSize.x, gpuProperties.MaxGridSize.y, gpuProperties.MaxGridSize.z);

            Console.WriteLine();
            Console.WriteLine("Attempting to load module...");
            System.Threading.Thread.Sleep(1000);

            // Get GPU arcitecture and load corresponding module
            //eArchitecture arch = gpu.GetArchitecture();
            //Console.WriteLine("Module: {0}", arch);
            CudafyModule km = CudafyTranslator.Cudafy();
            gpu.LoadModule(km);
            Console.WriteLine("Module successfully loaded.\n");
            System.Threading.Thread.Sleep(1000);

            return gpu;
        }


        [Cudafy]
        public static void addVectors(GThread thread, int[] a, int[] b, int[] c)
        {
            // Set "correct" id of the thread: id_within_current_block + id_of_the_block * block_dimension
            int threadID = thread.threadIdx.x + thread.blockIdx.x * thread.blockDim.x;
            // Make sure that the id is less than the length of the vectors
            while (threadID < a.Length)
            {
                c[threadID] = a[threadID] + b[threadID];
                // increment the thread id by block_dimension * number_of_blocks_in_the_grid
                threadID += thread.blockDim.x * thread.gridDim.x;
            }
        }

        private static void HandleException(Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        
        public static void Exit()
        {
            Console.WriteLine();
            Console.WriteLine("The program will now close. Press a key to exit...");
            Console.ReadKey();
        }
    }
}