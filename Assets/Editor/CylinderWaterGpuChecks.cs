using System;
using UnityEngine;
using UnityEngine.Rendering;

public static class CylinderWaterGpuChecks
{
    public static void CheckFoamDecay(ComputeShader source, float halfLife)
    {
        var compute=UnityEngine.Object.Instantiate(source);
        var buffers=new RenderTexture[3];
        var seed=new Texture2D(32,32,TextureFormat.RGBAFloat,false,true);
        try
        {
            for(int i=0;i<3;i++)
            {
                buffers[i]=new RenderTexture(32,32,0,RenderTextureFormat.ARGBHalf){enableRandomWrite=true};
                buffers[i].Create();
            }
            var pixels=new Color[1024];
            for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(.5f,1,0,0);
            seed.SetPixels(pixels);seed.Apply();
            Graphics.Blit(seed,buffers[0]);Graphics.Blit(seed,buffers[2]);
            int kernel=compute.FindKernel("CSWater");
            compute.SetInt("BUFFER_SIZE",32);compute.SetFloat("worldSize",32);compute.SetVector("worldPos",Vector4.zero);
            compute.SetFloat("deltaTime",1f/60);compute.SetFloat("foamDecayRate",Mathf.Log(2)/halfLife);
            compute.SetVectorArray("fsPos",new Vector4[4]);compute.SetVectorArray("fsPreviousPos",new Vector4[4]);
            compute.SetFloats("fsRadius",new float[16]);compute.SetFloats("fsAmp",new float[16]);
            compute.SetVectorArray("skimPositionRadiusAmplitude",new Vector4[4]);compute.SetVectorArray("skimVelocityFoam",new Vector4[4]);
            for(int step=0;step<60;step++)
            {
                Graphics.Blit(buffers[0],buffers[1]);
                compute.SetTexture(kernel,"WaterBuffer",buffers[0]);
                compute.SetTexture(kernel,"SourceBuffer",buffers[1]);compute.SetTexture(kernel,"OldBuffer",buffers[2]);
                compute.Dispatch(kernel,1,1,1);
                Graphics.Blit(buffers[1],buffers[2]);
            }
            var request=AsyncGPUReadback.Request(buffers[0],0,TextureFormat.RGBAFloat);request.WaitForCompletion();
            if(request.hasError)throw new InvalidOperationException("Foam decay readback failed.");
            float foam=0;
            foreach(var pixel in request.GetData<Color>())foam=Mathf.Max(foam,pixel.g);
            float expected=Mathf.Pow(.5f,1/halfLife);
            if(Mathf.Abs(foam-expected)>.006f)throw new InvalidOperationException("Old waves must not renew foam: "+foam+" expected "+expected);
            Debug.Log("CYLINDER_FOAM_DECAY_PASS remaining="+foam+" after1second");
        }
        finally
        {
            foreach(var buffer in buffers)if(buffer!=null){buffer.Release();UnityEngine.Object.DestroyImmediate(buffer);}
            UnityEngine.Object.DestroyImmediate(compute);UnityEngine.Object.DestroyImmediate(seed);
        }
    }
}
