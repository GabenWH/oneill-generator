"""Compile full Unity shader stages and execute their shared visual maths in Mesa.

This is a software shader check/preview, not a native Unity scene recording.
"""
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile

ROOT=Path(__file__).resolve().parents[2]
SHADERS=ROOT/'Assets/GraceVWaterOriginalTest/Shaders'
UNITY=Path(os.environ.get('UNITY_EDITOR_DIR','/home/Grace/Unity/Hub/Editor/2022.3.62f3/Editor'))/'Data/CGIncludes'


def compile_hlsl(path,stage,entry,out,extra=()):
    result=subprocess.run(['glslangValidator','-D','-V','-S',stage,'-e',entry,
        '--auto-map-bindings','--auto-map-locations','-I'+str(SHADERS),'-I'+str(UNITY),
        *extra,str(path),'-o',str(out)],capture_output=True,text=True)
    if result.returncode:
        raise RuntimeError(result.stdout+result.stderr)


def main():
    with tempfile.TemporaryDirectory(prefix='downwash-shaders-') as directory:
        temp=Path(directory)
        compiled=0
        for name in ['GraceDownwashMist.shader','GraceVisibleWater.shader']:
            programs=re.findall(r'CGPROGRAM(.*?)ENDCG',(SHADERS/name).read_text(),re.S)
            for index,program in enumerate(programs):
                for dynamics in [False,True] if name=='GraceVisibleWater.shader' else [False]:
                    header='#define SHADER_API_GLCORE 1\n#define SHADER_TARGET 40\n#define UNITY_PBS_USE_BRDF1 1\n#define DIRECTIONAL 1\n#include "UnityCG.cginc"\n'
                    if dynamics:header+='#define DYNAMICS_ENABLED 1\n#define REFRACTION_DEPTH_CORRECTION 1\n#define FOG_ENABLED 1\n#define FOG_LINEAR 1\n'
                    source=temp/'stage.hlsl';source.write_text(header+program)
                    for stage in ['vert','frag']:
                        compile_hlsl(source,stage,stage,temp/(stage+'.spv'));compiled+=1
        print('PASS:',compiled,'full shader stages compiled with Unity includes (OpenGL definitions, dynamics/fog variants).')
        # Reuse the existing software EGL setup, not a second graphics backend.
        spec=importlib.util.spec_from_file_location('water_probe',ROOT/'tests/vwater-skim/gpu_probe.py')
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        context=module.Probe()
        import numpy as np
        from OpenGL import GL
        from OpenGL.GL.ARB.gl_spirv import glSpecializeShaderARB
        source=temp/'visual.comp.hlsl'
        source.write_text('''
#include "DownwashWaves.cginc"
#include "DownwashMistShape.cginc"
RWTexture2D<float4> Result : register(u0);
[numthreads(8,1,1)]
void CSTests(uint3 id:SV_DispatchThreadID) {
    float4 wave=float4(0,0,0,.24),shape=float4(1.6,.35,.35,5);
    float value=0;
    if(id.x==0)value=GraceRingHeight(float2(1.6,0),wave,shape,1);
    if(id.x==1)value=GraceRingHeight(float2(3.2,0),wave,shape,1);
    if(id.x==2)value=GraceRingHeight(float2(3.2,0),wave,shape,2);
    if(id.x==3)value=GraceRingHeight(float2(1.6,0),wave,shape,2);
    if(id.x==4)value=GraceRingHeight(float2(0,0),wave,shape,-1);
    if(id.x==5)value=GraceRingHeight(float2(0,0),wave,shape,10);
    if(id.x==6)value=GraceMistAlpha(float2(.5,.5),1,0);
    if(id.x==7)value=GraceMistAlpha(float2(1,1),1,0);
    Result[id.xy]=float4(value,0,0,1);
}
[numthreads(8,1,1)]
void CSAngleTests(uint3 id:SV_DispatchThreadID) {
    float3 views[8]={float3(1,0,0),float3(0,1,0),float3(0,-1,0),float3(1,1,0),float3(0,0,1),float3(0,0,0),float3(1,.176327,0),float3(1,.700208,0)};
    float angle=GraceMistViewAngle(views[id.x]);
    Result[id.xy]=float4(angle,GraceMistViewLookupUV(angle).x,0,1);
}
[numthreads(16,16,1)]
void CSPreview(uint3 id:SV_DispatchThreadID) {
    float3 base=float3(.09,.20,.24);
    if(id.x<256) {
        float2 world=(float2(id.xy)/256-.5)*11;
        float height=0,offset=0;
        for(int j=0;j<4;j++) {
            float4 wave=float4(0,0,j*.65,.24),shape=float4(1.6,.35,.35,5);
            height+=GraceRingHeight(world,wave,shape,2.4);
            offset+=GraceRingHeight(world+float2(.04,0),wave,shape,2.4);
        }
        base*=clamp(1+(offset-height)*12,.5,1.5);
        base=lerp(base,float3(.82,.94,1),smoothstep(.025,.14,max(0,height))*.65);
    } else {
        float2 uv=float2(id.x-256,id.y)/256;
        float alpha=GraceMistAlpha(uv,1.0,0)*.8;
        base=lerp(base,float3(.9,.96,1),alpha);
    }
    Result[id.xy]=float4(base,1);
}
''')
        def execute(entry,width,height,groups):
            binary_path=temp/(entry+'.spv');compile_hlsl(source,'comp',entry,binary_path)
            shader=GL.glCreateShader(GL.GL_COMPUTE_SHADER);binary=binary_path.read_bytes()
            GL.glShaderBinary(1,[shader],GL.GL_SHADER_BINARY_FORMAT_SPIR_V,binary,len(binary))
            glSpecializeShaderARB(shader,entry.encode(),0,None,None)
            assert GL.glGetShaderiv(shader,GL.GL_COMPILE_STATUS),GL.glGetShaderInfoLog(shader)
            program=GL.glCreateProgram();GL.glAttachShader(program,shader);GL.glLinkProgram(program)
            assert GL.glGetProgramiv(program,GL.GL_LINK_STATUS),GL.glGetProgramInfoLog(program)
            GL.glUseProgram(program)
            texture=GL.glGenTextures(1);GL.glBindTexture(GL.GL_TEXTURE_2D,texture)
            GL.glTexStorage2D(GL.GL_TEXTURE_2D,1,GL.GL_RGBA32F,width,height)
            GL.glBindImageTexture(0,texture,0,False,0,GL.GL_WRITE_ONLY,GL.GL_RGBA32F)
            GL.glDispatchCompute(*groups);GL.glMemoryBarrier(GL.GL_ALL_BARRIER_BITS);GL.glFinish()
            pixels=np.frombuffer(GL.glGetTexImage(GL.GL_TEXTURE_2D,0,GL.GL_RGBA,GL.GL_FLOAT),np.float32).reshape(height,width,4).copy()
            GL.glDeleteTextures([texture]);GL.glDeleteProgram(program);GL.glDeleteShader(shader)
            return pixels
        values=execute('CSTests',8,1,(1,1,1))[0,:,0]
        assert np.isfinite(values).all()
        assert values[0]>.10 and abs(values[1])<.001,'Ring must reach 1.6 m before 3.2 m'
        assert values[2]>.05 and abs(values[3])<.001,'Ring must travel outward, not remain fixed'
        assert values[4]==0 and values[5]==0,'Unborn/expired ring must vanish'
        assert values[6]>.1 and values[7]==0,'Wisp must have a visible core and transparent edges'
        angles=execute('CSAngleTests',8,1,(1,1,1))[0]
        assert np.allclose(angles[:,0],[0,90,90,45,0,0,10,35],atol=.002),'View angles must use the water plane, not particle billboard normals'
        assert np.isfinite(angles).all()
        assert abs(angles[0,1]-.5/256)<1e-6 and abs(angles[1,1]-255.5/256)<1e-6,'Angle lookup must sample exact end texels'
        preview=execute('CSPreview',512,256,(32,16,1))
        assert np.isfinite(preview).all()
        from PIL import Image,ImageDraw
        image=Image.fromarray((np.clip(preview[...,:3],0,1)*255).astype('uint8')).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
        draw=ImageDraw.Draw(image);draw.text((8,8),'Wave shader - outward crests',fill='white');draw.text((265,8),'Mist shader shape',fill='white')
        folder=ROOT/'Diagnostics/SprayWaves_20261103';folder.mkdir(parents=True,exist_ok=True)
        image.save(folder/'shader-preview.png')
        (folder/'shader-verification.json').write_text(json.dumps({'scope':'Software shader checks, not native Unity rendering','renderer':context.renderer,'compiled_stages':compiled,'samples':values.tolist(),'view_angle_and_lookup_uv':angles[:,:2].tolist()},indent=2)+'\n')
        print('PASS: actual HLSL wave propagation, expiry, wisp core/edge, camera angles and opacity lookup endpoints; software preview saved.')


if __name__=='__main__':main()
